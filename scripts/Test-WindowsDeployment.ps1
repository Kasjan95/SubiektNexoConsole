[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $repository
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an administrator PowerShell window. No tests were executed.'
}
$env:CONNECTOR_WINDOWS_INTEGRATION_TESTS = '1'
& dotnet test tests/SubiektNexoConnector.Deployment.Tests/SubiektNexoConnector.Deployment.Tests.csproj `
    --no-build --no-restore --nologo --filter FullyQualifiedName~WindowsIntegrationTests `
    --logger 'trx;LogFileName=windows-integration.trx' --results-directory artifacts/windows-integration -v normal
exit $LASTEXITCODE
