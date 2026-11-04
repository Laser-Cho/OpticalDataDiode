# PowerShell Build Script for JabOpticalDataDiode
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location -LiteralPath $scriptDir

$dotnetRoot = "D:\ppt 원격전송\OpticalDataDiode\.dotnet"
if (Test-Path $dotnetRoot) {
    $env:DOTNET_ROOT = $dotnetRoot
    $dotnet = Join-Path $dotnetRoot "dotnet.exe"
} else {
    $dotnet = "dotnet"
}

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host " JabOpticalDataDiode Build Script" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

$proj = Join-Path $scriptDir "JabOpticalDataDiode.csproj"
& $dotnet restore $proj

$out = Join-Path $scriptDir "bin\Release\net8.0-windows"
& $dotnet build $proj -c Release -o $out

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nBuild Successful! Output: $out\JabOpticalDataDiode.exe" -ForegroundColor Green
} else {
    Write-Host "`n[ERROR] Build Failed." -ForegroundColor Red
}
