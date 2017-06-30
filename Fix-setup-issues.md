# Fix setup issues
Sometimes, install, uninstall or update of LDAPCP solution fails. It has nothing to do with LDAPCP solution itself, but it is caused by various reasons in SharePoint.

When this happens, most of the time LDAPCP features are in a half installed state which must be fixed.
Perform all the step below on the server **running central administration**:

## Identify LDAPCP features installed
```powershell
# Identify all LDAPCP features installed on the farm
Get-SPFeature| ?{$_.DisplayName -like 'LDAPCP*'}| fl DisplayName, Scope, Id, RootDirectory
```
Usually, only LDAPCP farm feature is listed:
```
DisplayName   : LDAPCP
Scope         :
Id            : b37e0696-f48c-47ab-aa30-834d78033ba8
RootDirectory : C:\Program Files\Common Files\Microsoft Shared\Web Server Extensions\16\Template\Features\LDAPCP
```
 
## Identify LDAPCP features to fix
For each feature listed, check if its "RootDirectory" actually exists in the file system of the server.
If it does not exist:
* Create the RootDirectory (e.g. LDAPCP in Features folder)
* Use [7-zip](http://www.7-zip.org/) to unzip LDAPCP.wsp
* Copy the feature.xml of the corresponding feature from the LDAPCP.wsp unzipped
* Paste it into the "RootDirectory" that you just created
 
## Deactivate and remove the features
```powershell
# Deactivate LDAPCP features
Get-SPFeature| ?{$_.DisplayName -like 'LDAPCP*'}| Disable-SPFeature -Confirm:$false
# Uninstall LDAPCP features
Get-SPFeature| ?{$_.DisplayName -like 'LDAPCP*'}| Uninstall-SPFeature -Confirm:$false
```
 
## If desired, LDAPCP solution can now be safely removed
```powershell
Remove-SPSolution "LDAPCP.wsp" -Confirm:$false
```
