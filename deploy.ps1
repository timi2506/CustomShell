param (
    [string]$TargetDir = "C:\CustomShell"
)

$Architectures = @("win-arm64", "win-x64", "win-x86")

Write-Host "Creating deployment directory $TargetDir..." -ForegroundColor Cyan
if (-not (Test-Path $TargetDir)) {
    New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null
}

foreach ($arch in $Architectures) {
    Write-Host "Compiling CustomShell for $arch..." -ForegroundColor Cyan
    dotnet publish -c Release -r $arch -p:PublishSingleFile=true --self-contained true -p:IncludeNativeLibrariesForSelfExtract=true

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed for $arch!" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    $ExePath = ".\bin\Release\net8.0-windows\$arch\publish\CustomShell.exe"
    $DestExe = Join-Path $TargetDir "CustomShell-$arch.exe"

    Write-Host "Copying $arch executable to $DestExe..." -ForegroundColor Cyan
    Copy-Item -Path $ExePath -Destination $DestExe -Force
}

Write-Host "Registering Auto-Start for win-arm64 (your machine)..." -ForegroundColor Cyan
$PrimaryExe = Join-Path $TargetDir "CustomShell-win-arm64.exe"
$RegPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
Set-ItemProperty -Path $RegPath -Name "CustomShell" -Value "`"$PrimaryExe`""

Write-Host "Deployment Complete!" -ForegroundColor Green
Write-Host "All 3 architectures are ready in $TargetDir."
