$elevatedservices = Join-Path $PSScriptRoot 'Add-ElevatedServices.ps1'

# Do the elevated operations: 
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]'Administrator')) {
    Start-Process powershell.exe -Verb runas -ArgumentList "-ExecutionPolicy Unrestricted -file `"$($elevatedservices)`"" -Wait
} else {
    Set-Location $PSScriptRoot
    Invoke-Expression ".\Add-ElevatedServices.ps1"
}

# Enable network loopback
checknetisolation loopbackexempt -a -n=5F8736BE-4C03-49F7-974D-7CE6A963E3E2_chaze3gkcrgtt

# Run the Add-AppDevPackage. Once to add the certificate and again to install
$currLocation = Get-Location
Set-Location $PSScriptRoot
Invoke-Expression ".\Add-AppDevPackage.ps1 -SkipLoggingTelemetry"
Set-Location $currLocation