# PowerShell Single-File Publish Script
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location -LiteralPath $scriptDir

$dotnet = Join-Path $scriptDir ".dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) {
    $dotnet = "dotnet"
}

Write-Host "Publishing single-file all-in-one executable..." -ForegroundColor Cyan

$proj = Join-Path $scriptDir "OpticalDataDiode.csproj"
$out = Join-Path $scriptDir "..\publish"

& $dotnet publish $proj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $out

Remove-Item "$out\*.pdb" -Force -ErrorAction SilentlyContinue

Write-Host "`nDone! Single file executable: $out\OpticalDataDiode.exe" -ForegroundColor Green
