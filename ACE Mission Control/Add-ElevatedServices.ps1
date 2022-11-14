# Add the certificate
$currLocation = Get-Location
Set-Location $PSScriptRoot
$Certificate = (Get-ChildItem -Filter "*.cer")[0].FullName
Invoke-Expression ".\Add-AppDevPackage.ps1 -SkipLoggingTelemetry -CertificatePath '$Certificate' -Force"
Set-Location $currLocation

# Install service exe
$Service = Get-Service -Name "ACE Drone Dashboard Service" -ErrorAction SilentlyContinue
$ServiceInstallPath = Join-Path $env:LOCALAPPDATA "ACE Mission Control\Service\ACE Drone Dashboard Service.exe"

if ($Service -ne $null)
{
    sc.exe stop "ACE Drone Dashboard Service"
    $Service.WaitForStatus('Stopped')
}

New-Item -ItemType File -Path $ServiceInstallPath -Force
Copy-Item (Join-Path $PSScriptRoot 'ACE Drone Dashboard Service.exe') $ServiceInstallPath -Force
sc.exe create "ACE Drone Dashboard Service" binpath=$ServiceInstallPath start=delayed-auto error=ignore
sc.exe description "ACE Drone Dashboard Service" "This service automatically provides drone position information to the connected database when the database is reachable and UgCS is running."
sc.exe start "ACE Drone Dashboard Service"