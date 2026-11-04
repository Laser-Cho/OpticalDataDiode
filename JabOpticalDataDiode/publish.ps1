# PowerShell Single-File Publish Script for JabOpticalDataDiode
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location -LiteralPath $scriptDir

$dotnetRoot = "D:\ppt 원격전송\OpticalDataDiode\.dotnet"
if (Test-Path $dotnetRoot) {
    $env:DOTNET_ROOT = $dotnetRoot
    $env:PATH = "$dotnetRoot;" + $env:PATH
}

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host " Publishing Single-File Exe: JabOpticalDataDiode" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

$proj = Join-Path $scriptDir "JabOpticalDataDiode.csproj"
$out = Join-Path $scriptDir "publish"

dotnet publish $proj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $out

Remove-Item "$out\*.pdb" -Force -ErrorAction SilentlyContinue

Write-Host "`nDone! All-in-one Single File Exe: $out\JabOpticalDataDiode.exe" -ForegroundColor Green
