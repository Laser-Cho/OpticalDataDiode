# PowerShell Build Script for OpticalDataDiode
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location -LiteralPath $scriptDir

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host " OpticalDataDiode Build Script" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

# 1. Check if global dotnet SDK is available
$hasGlobalDotnet = $false
try {
    $sdkList = dotnet --list-sdks 2>$null
    if ($LASTEXITCODE -eq 0 -and $sdkList) {
        $hasGlobalDotnet = $true
        Write-Host "[INFO] Global .NET SDK detected." -ForegroundColor Green
    }
} catch {}

$dotnetExe = "dotnet"

if (-not $hasGlobalDotnet) {
    $localDotnet = Join-Path $scriptDir ".dotnet\dotnet.exe"
    if (Test-Path -LiteralPath $localDotnet) {
        $dotnetExe = $localDotnet
        Write-Host "[INFO] Local .NET SDK detected at: $localDotnet" -ForegroundColor Green
    } else {
        Write-Host "[INFO] .NET SDK not detected. Installing .NET 8 SDK locally into .dotnet directory..." -ForegroundColor Yellow
        $installScript = Join-Path $env:TEMP "dotnet-install.ps1"
        try {
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $installScript -UseBasicParsing
            & $installScript -Channel 8.0 -InstallDir (Join-Path $scriptDir ".dotnet")
            $dotnetExe = $localDotnet
        } catch {
            Write-Host "[ERROR] Failed to download or install .NET SDK: $_" -ForegroundColor Red
            Write-Host "Please download and install .NET 8 SDK manually from: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
            Exit 1
        }
    }
}

# 2. Restore NuGet Packages
Write-Host "`n[1/2] Restoring NuGet Packages..." -ForegroundColor Cyan
$projPath = Join-Path $scriptDir "OpticalDataDiode.csproj"
& $dotnetExe restore $projPath

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] NuGet restore failed." -ForegroundColor Red
    Exit $LASTEXITCODE
}

# 3. Build Project
Write-Host "`n[2/2] Building Release Binary..." -ForegroundColor Cyan
$outDir = Join-Path $scriptDir "bin\Release\net8.0-windows"
& $dotnetExe build $projPath -c Release -o $outDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Build failed." -ForegroundColor Red
    Exit $LASTEXITCODE
}

$exePath = Join-Path $outDir "OpticalDataDiode.exe"
Write-Host "`n========================================================" -ForegroundColor Green
Write-Host " Build Succeeded!" -ForegroundColor Green
Write-Host " Output: $exePath" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Green
