Sometimes, install or uninstall of LDAPCP fails. It has nothing to do with LDAPCP solution itself.
It may happen because the SharePoint PowerShell console opened to install LDAPCP was opened for a long time, and for some reason is not synced with some recent changes in the farm, or because some configuration changed but the local persisted object cache did not get it and that causes a conflict.

When this happens, LDAPCP features must be properly uninstalled before you can either reinstall of completely uninstall LDAPCP.
Perform all the step below on the server **running central administration**:

 1. Identify LDAPCP features to delete:
```powershell
# Identify all LDAPCP features that are installed on the farm
Get-SPFeature| ?{$_.DisplayName -like 'LDAPCP*'}| fl DisplayName, Scope, Id, RootDirectory
```
Usually, only LDAPCP farm feature is listed:
```
DisplayName   : LDAPCP
Scope         :
Id            : b37e0696-f48c-47ab-aa30-834d78033ba8
RootDirectory : C:\Program Files\Common Files\Microsoft Shared\Web Server Extensions\16\Template\Features\LDAPCP
```

 2. For each feature listed, check if its RootDirectory actually exists in the file system:
If it does not exist:
* Create the RootDirectory (e.g. LDAPCP in Features folder)
* Use [7-zip](http://www.7-zip.org/) to unzip LDAPCP.wsp and copy the feature.xml of the corresponding feature into the RootDirectory that you just created

 3. Deactivate and remove the features:
```powershell
# Deactivate LDAPCP features
Get-SPFeature| ?{$_.DisplayName -like 'LDAPCP*'}| Disable-SPFeature -Confirm:$false
# Uninstall LDAPCP features
Get-SPFeature| ?{$_.DisplayName -like 'LDAPCP*'}| Uninstall-SPFeature -Confirm:$false
```

 4. If desired, LDAPCP solution can now be safely removed:
```powershell
Remove-SPSolution "LDAPCP.wsp" -Confirm:$false
```
