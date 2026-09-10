[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",

    [string] $RuntimeIdentifier = "win-x64",

    [string] $OutputDirectory,
    [ValidateSet('Major', 'Minor', 'Patch', 'Revision', 'Current')]
    [string] $VersionLevel,
    [switch] $Yes,
    [string] $SigningCertificateThumbprint,
    [string] $TimestampServer,
    [switch] $Production
)

$ErrorActionPreference = "Stop"
# Windows PowerShell 5.1 does not load this assembly automatically (unlike PowerShell 7).
# Load before prompting/changing the version so a missing dependency fails during preflight.
Add-Type -AssemblyName System.IO.Compression -ErrorAction Stop
$repositoryRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Release-Version.ps1')
$gitExecutable = Find-PublishGit
$sourceRevision = Get-PublishSourceRevision -RepositoryRoot $repositoryRoot -GitPath $gitExecutable -Production:$Production
[xml] $releaseProperties = Get-Content -LiteralPath (Join-Path $repositoryRoot 'Release.props') -Raw
$releaseVersion = [string] $releaseProperties.Project.PropertyGroup.ReleaseVersion
$versionSelection = @{ Current = $releaseVersion; Yes = $Yes }
if ($VersionLevel) { $versionSelection.Level = $VersionLevel }
$releaseVersion = Select-ReleaseVersion @versionSelection
Set-ReleaseVersion -Path (Join-Path $repositoryRoot 'Release.props') -Version $releaseVersion
if ($gitExecutable) {
    # Recheck after saving the version; production must not publish an uncommitted version bump.
    $sourceRevision = Get-PublishSourceRevision -RepositoryRoot $repositoryRoot -GitPath $gitExecutable -Production:$Production
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot "artifacts\deployment-release"
}

$workingDirectory = Join-Path $repositoryRoot ("artifacts\deployment-release-work-" + [Guid]::NewGuid().ToString('N'))
$adapterPublishDirectory = Join-Path $workingDirectory "adapter-publish"
$payloadArchive = Join-Path $workingDirectory "adapter-payload.zip"
$packageDeployerAssembly = Join-Path $workingDirectory "PackageDeployer.dll"
$destinationDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$installerPublishDirectory = Join-Path $workingDirectory 'setup'
$existingRelease = Join-Path $destinationDirectory 'release.json'
if (Test-Path -LiteralPath $existingRelease) {
    $previousRelease = Get-Content -LiteralPath $existingRelease -Raw | ConvertFrom-Json
    if ([version](ConvertTo-ReleaseVersion ([string]$previousRelease.version)) -ge [version]$releaseVersion) {
        throw 'This output already contains the same or a newer release. Increment Release.props; existing published binaries must not be replaced under the same version.'
    }
}
$artifactsDirectory = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts"))
$artifactsPrefix = $artifactsDirectory.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (-not $destinationDirectory.StartsWith($artifactsPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must be a child of '$artifactsDirectory'."
}
$adapterProject = Join-Path $repositoryRoot "src\SubiektNexoConnector.Api\SubiektNexoConnector.Api.csproj"
$installerProject = Join-Path $repositoryRoot "src\SubiektNexoConnector.Deployment\SubiektNexoConnector.Deployment.csproj"
$payloadBuilderProject = Join-Path $repositoryRoot "tools\SubiektNexoConnector.Deployment.PayloadBuilder\SubiektNexoConnector.Deployment.PayloadBuilder.csproj"
$quickInstallerWizardAssembly = if ([string]::IsNullOrWhiteSpace($env:NEXO_SDK_PATH)) {
    $null
} else {
    Join-Path $env:NEXO_SDK_PATH "Narzedzia\QuickInstallerWizard.dll"
}

if ([string]::IsNullOrWhiteSpace($quickInstallerWizardAssembly) -or -not (Test-Path -LiteralPath $quickInstallerWizardAssembly)) {
    throw "QuickInstallerWizard.dll was not found. Set NEXO_SDK_PATH to the InsERT nexo SDK directory."
}

if ($Production -and ([string]::IsNullOrWhiteSpace($SigningCertificateThumbprint) -or [string]::IsNullOrWhiteSpace($TimestampServer))) {
    throw 'Production builds require a code-signing certificate thumbprint and timestamp server.'
}
$signingCertificate = $null
if (-not [string]::IsNullOrWhiteSpace($SigningCertificateThumbprint)) {
    $signingCertificate = Get-Item -LiteralPath ("Cert:\CurrentUser\My\" + $SigningCertificateThumbprint)
    if (-not $signingCertificate.HasPrivateKey -or $signingCertificate.NotAfter -le (Get-Date)) { throw 'The signing certificate is not usable.' }
}
foreach ($directory in @($workingDirectory, $installerPublishDirectory)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
Write-Host 'Running the solution tests...'
& (Join-Path $PSScriptRoot 'Test-ReleaseVersion.ps1') 6>$null
Write-Host 'Version selection tests passed.'
& dotnet test (Join-Path $repositoryRoot 'SubiektNexoConnector.sln') --no-restore --nologo --verbosity quiet -p:NoWarn=MSB3277
if ($LASTEXITCODE -ne 0) { throw 'Tests failed; the previous installer has not been changed.' }

Write-Host "Extracting the official Nexo package deployer..."
$quickInstallerAssembly = [System.Reflection.Assembly]::LoadFrom($quickInstallerWizardAssembly)
$packageDeployerResource = "InsERT.Mox.QuickInstallerWizard.QuickInstallerTemplate.zip"
$templateStream = $quickInstallerAssembly.GetManifestResourceStream($packageDeployerResource)
if ($null -eq $templateStream) {
    throw "QuickInstallerWizard does not contain resource '$packageDeployerResource'."
}
$templateArchive = [System.IO.Compression.ZipArchive]::new(
    $templateStream,
    [System.IO.Compression.ZipArchiveMode]::Read,
    $false)
try {
    $packageDeployerEntry = $templateArchive.GetEntry("Resources/PackageDeployer.dll")
    if ($null -eq $packageDeployerEntry) {
        throw "QuickInstaller template does not contain Resources/PackageDeployer.dll."
    }
    $entryStream = $packageDeployerEntry.Open()
    $outputStream = [System.IO.File]::Create($packageDeployerAssembly)
    try {
        $entryStream.CopyTo($outputStream)
    } finally {
        $outputStream.Dispose()
        $entryStream.Dispose()
    }
} finally {
    $templateArchive.Dispose()
}

Write-Host "Publishing adapter..."
& dotnet publish $adapterProject `
    --configuration $Configuration `
    --runtime $RuntimeIdentifier `
    --self-contained false `
    --output $adapterPublishDirectory `
    --nologo `
    --verbosity quiet `
    -p:SourceRevisionId=$sourceRevision `
    -p:NoWarn=MSB3277 `
    -p:DebugSymbols=false `
    -p:DebugType=None
if ($LASTEXITCODE -ne 0) { throw "Adapter publish failed with exit code $LASTEXITCODE." }

Write-Host "Preparing adapter payload..."
& dotnet run `
    --project $payloadBuilderProject `
    --configuration $Configuration `
    --runtime $RuntimeIdentifier `
    --no-launch-profile `
    --verbosity quiet `
    -- `
    $adapterPublishDirectory `
    $payloadArchive
if ($LASTEXITCODE -ne 0) { throw "Adapter payload build failed with exit code $LASTEXITCODE." }

Write-Host "Publishing single-file installer..."
& dotnet publish $installerProject `
    --configuration $Configuration `
    --runtime $RuntimeIdentifier `
    --self-contained false `
    --output $installerPublishDirectory `
    --nologo `
    --verbosity quiet `
    -p:AssemblyName=SubiektNexoConnector.Setup `
    -p:SourceRevisionId=$sourceRevision `
    -p:AdapterPayloadPath=$payloadArchive `
    -p:EmbedAdapterPayload=true `
    -p:PackageDeployerPath=$packageDeployerAssembly `
    -p:EmbedPackageDeployer=true `
    -p:PublishSingleFile=true `
    -p:PublishTrimmed=false `
    -p:DebugSymbols=false `
    -p:DebugType=None
if ($LASTEXITCODE -ne 0) { throw "Installer publish failed with exit code $LASTEXITCODE." }

$installerPath = Join-Path $installerPublishDirectory "SubiektNexoConnector.Setup.exe"
if (-not (Test-Path -LiteralPath $installerPath)) {
    throw "Single-file installer was not created at '$installerPath'."
}

$unexpectedFiles = @(Get-ChildItem -LiteralPath $installerPublishDirectory -File | Where-Object FullName -ne $installerPath)
if ($unexpectedFiles.Count -gt 0) {
    $names = $unexpectedFiles.Name -join ", "
    throw "Installer publish produced unexpected sidecar files: $names"
}

if ($null -ne $signingCertificate) {
    $signingParameters = @{ FilePath = $installerPath; Certificate = $signingCertificate; HashAlgorithm = 'SHA256' }
    if (-not [string]::IsNullOrWhiteSpace($TimestampServer)) { $signingParameters.TimestampServer = $TimestampServer }
    $signature = Set-AuthenticodeSignature @signingParameters
    if ($signature.Status -ne 'Valid') { throw "Signing failed: $($signature.Status)" }
}
# Replace the distributable only after every build/check has passed. Keep the previous EXE as a backup.
New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
$destination = Join-Path $destinationDirectory 'SubiektNexoConnector.Setup.exe'
$newFile = Join-Path $destinationDirectory ('setup-' + [Guid]::NewGuid().ToString('N') + '.tmp')
Copy-Item -LiteralPath $installerPath -Destination $newFile
if (Test-Path -LiteralPath $destination) {
    [System.IO.File]::Replace($newFile, $destination, $destination + '.' + (Get-Date -Format 'yyyyMMddHHmmss') + '.bak')
} else { [System.IO.File]::Move($newFile, $destination) }
$hash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
$metadata = [ordered]@{ version = $releaseVersion; sourceRevision = $sourceRevision; sha256 = $hash; signed = ($null -ne $signingCertificate); payloadSha256 = (Get-FileHash $payloadArchive -Algorithm SHA256).Hash; packageDeployerSha256 = (Get-FileHash $packageDeployerAssembly -Algorithm SHA256).Hash; requiredRuntime = '.NET 8 Windows Desktop x64'; qualification = 'Requires Windows/Nexo acceptance tests' }
[System.IO.File]::WriteAllText((Join-Path $destinationDirectory 'release.json'), ($metadata | ConvertTo-Json))
[System.IO.File]::WriteAllText(($destination + '.sha256'), "$hash  SubiektNexoConnector.Setup.exe")
$sizeMiB = [Math]::Round((Get-Item -LiteralPath $destination).Length / 1MB, 1)
Write-Host "Ready: $destination ($sizeMiB MiB). Signed: $($null -ne $signingCertificate)"
