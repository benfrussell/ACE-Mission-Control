$script = Join-Path $PSScriptRoot 'Install.ps1'

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]'Administrator')) {
    Start-Process powershell.exe -Verb runas -ArgumentList "-file $script"
    exit
}

$Certificate = (Get-ChildItem -Filter "*.cer")[0].FullName


# Run the Add-AppDevPackage
$currLocation = Get-Location
Set-Location $PSScriptRoot
#Invoke-Expression ".\Add-AppDevPackage.ps1 -SkipLoggingTelemetry -Force -CertificatePath $Certificate"
Set-Location $currLocation

# Enable network loopback
checknetisolation loopbackexempt -a -n=5F8736BE-4C03-49F7-974D-7CE6A963E3E2_chaze3gkcrgtt

# Install service exe
$ServiceInstallPath = Join-Path $env:LOCALAPPDATA "ACE Mission Control\Service\ACE Drone Dashboard Service.exe"
New-Item -ItemType File -Path $ServiceInstallPath -Force
Copy-Item (Join-Path $PSScriptRoot 'ACE Drone Dashboard Service.exe') $ServiceInstallPath -Force
sc.exe stop "ACE Drone Dashboard Service"
sc.exe create "ACE Drone Dashboard Service" binpath=$ServiceInstallPath start=delayed-auto error=ignore
sc.exe description "ACE Drone Dashboard Service" "This service automatically provides drone position information to the connected database when the database is reachable and UgCS is running."
sc.exe start "ACE Drone Dashboard Service"