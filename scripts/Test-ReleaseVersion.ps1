$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Release-Version.ps1')
$checks = 0
function Assert-Equal($expected, $actual) {
    if ($expected -cne $actual) { throw "Expected '$expected', got '$actual'." }
    $script:checks++
}
function Assert-Throws([scriptblock] $action) {
    $thrown = $false
    try { & $action | Out-Null } catch { $thrown = $true }
    if (!$thrown) { throw 'Expected an exception.' }
    $script:checks++
}
Assert-Equal '2.0.0.0' (Get-NextReleaseVersion '1.2.3.4' Major)
Assert-Equal '1.3.0.0' (Get-NextReleaseVersion '1.2.3.4' Minor)
Assert-Equal '1.2.4.0' (Get-NextReleaseVersion '1.2.3.4' Patch)
Assert-Equal '1.2.3.5' (Get-NextReleaseVersion '1.2.3.4' Revision)
Assert-Equal '1.2.3.4' (Get-NextReleaseVersion '1.2.3.4' Current)
Assert-Equal '1.2.3.0' (Get-NextReleaseVersion '1.2.3' Current)
Assert-Equal '2.0.0.0' (Get-NextReleaseVersion '1.65534.65534.65534' Major)
Assert-Equal '1.2.3.65534' (Get-NextReleaseVersion '1.2.3.65533' Revision)
Assert-Throws { Get-NextReleaseVersion '1.2.3.65534' Revision }
Assert-Throws { Get-NextReleaseVersion '1.2.3.65535' Major }
Assert-Throws { Get-NextReleaseVersion '1.2.bad.0' Major }
Assert-Throws { Get-NextReleaseVersion '1.2.3.4.5' Major }
Assert-Throws { Get-NextReleaseVersion '1.-2.3.4' Major }
Assert-Equal '1.2.3.5' (Select-ReleaseVersion -Current '1.2.3.4' -Level Revision -Yes)

# Mock console input, never prompt the developer running the tests.
$answers = [System.Collections.Generic.Queue[string]]::new()
function Read-Host([string] $Prompt) {
    if ($script:answers.Count -eq 0) { throw 'Unexpected console prompt.' }
    return $script:answers.Dequeue()
}
$answers.Enqueue('bad'); $answers.Enqueue('1'); $answers.Enqueue('T')
Assert-Equal '2.0.0.0' (Select-ReleaseVersion -Current '1.2.3.4')
$answers.Enqueue('4'); $answers.Enqueue('N')
Assert-Throws { Select-ReleaseVersion -Current '1.2.3.4' }
$answers.Enqueue('Q')
Assert-Throws { Select-ReleaseVersion -Current '1.2.3.4' }
$answers.Enqueue('5')
Assert-Equal '1.2.3.4' (Select-ReleaseVersion -Current '1.2.3.4')
Assert-Equal 0 $answers.Count

$withoutGit = Get-PublishSourceRevision -RepositoryRoot $PSScriptRoot -GitPath '' -WarningAction SilentlyContinue
Assert-Equal $true ($withoutGit -match '^nogit\.[a-f0-9]{32}$')
Assert-Throws { Get-PublishSourceRevision -RepositoryRoot $PSScriptRoot -GitPath '' -Production }
$missingGit = Join-Path $PSScriptRoot 'nonexistent-git-for-test.exe'
$withoutGit = Get-PublishSourceRevision -RepositoryRoot $PSScriptRoot -GitPath $missingGit -WarningAction SilentlyContinue
Assert-Equal $true ($withoutGit -match '^nogit\.[a-f0-9]{32}$')
Assert-Throws { Get-PublishSourceRevision -RepositoryRoot $PSScriptRoot -GitPath $missingGit -Production }

$testDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ('connector-version-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDirectory | Out-Null
$testFile = Join-Path $testDirectory 'Release.props'
try {
    Copy-Item -LiteralPath (Join-Path (Split-Path $PSScriptRoot -Parent) 'Release.props') -Destination $testFile
    Set-ReleaseVersion $testFile '2.0.0.0'
    [xml] $saved = Get-Content -LiteralPath $testFile -Raw
    Assert-Equal '2.0.0.0' ([string]$saved.Project.PropertyGroup.ReleaseVersion)
    Assert-Equal '$(ReleaseVersion)' ([string]$saved.Project.PropertyGroup.AssemblyVersion)
    $before = (Get-FileHash -LiteralPath $testFile).Hash
    Set-ReleaseVersion $testFile '2.0.0.0'
    Assert-Equal $before (Get-FileHash -LiteralPath $testFile).Hash
    Assert-Throws { Set-ReleaseVersion $testFile 'broken' }
    Assert-Equal $before (Get-FileHash -LiteralPath $testFile).Hash
} finally {
    # Only our exact temporary file/directory; no recursive removal.
    Remove-Item -LiteralPath $testFile -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $testDirectory
}
Write-Host "Release version tests passed: $checks"
