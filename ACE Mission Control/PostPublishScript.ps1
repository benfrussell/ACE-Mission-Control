$PackagesPath = [IO.Path]::Combine($PSScriptRoot, 'AppPackages')

$NewestPackagePath = [IO.Path]::Combine($PackagesPath, (Get-ChildItem -Directory $PackagesPath | sort CreationTime)[-1].Name)

$ACEServiceExe = [IO.Path]::Combine(
    (Get-Item $PSScriptRoot).Parent.FullName, 
    'ACE Drone Dashboard Service\bin\Release\net6.0-windows10.0.19041.0\publish\win-x86\ACE Drone Dashboard Service.exe')

# Remove the developer license check and add call to post-install script
$AppDevPackageScript = [IO.File]::ReadAllText("$NewestPackagePath\Add-AppDevPackage.ps1")
$ModifiedScript = $AppDevPackageScript.Replace('$NeedDeveloperLicense = CheckIfNeedDeveloperLicense', '$NeedDeveloperLicense = $false')
[IO.File]::WriteAllText("$NewestPackagePath\Add-AppDevPackage.ps1", $ModifiedScript)

# Copy in service exe
Copy-Item $ACEServiceExe -Destination $NewestPackagePath

# Copy in new install script
Copy-Item (Join-Path $PSScriptRoot 'Install.ps1') -Destination $NewestPackagePath
