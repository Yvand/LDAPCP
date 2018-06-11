## Fix setup issues

Sometimes, install/uninstall/update of LDAPCP solution fails. Most of the time, it occurs when cmdlets were executed in an old PowerShell console that had stale persisted objects. This caused concurrency update errors and SharePoint cancelled operation in the middle of the process.  
When this happens, some LDAPCP features are in an inconsistent state, this page will walk you through the steps to clean this.

> **Important:**  
> Start a **new PowerShell console** to ensure the use of up to date persisted objects, which avoids concurrency update errors.  
> Make all operations in the server **running central administration**, in this order.

### Remove LDAPCP claims provider

```powershell
Get-SPClaimProvider| ?{$_.DisplayName -like "LDAPCP"}| Remove-SPClaimProvider
```

### Identify LDAPCP features still installed

```powershell
# Identify all LDAPCP features still installed on the farm, and that need to be manually uninstalled
Get-SPFeature| ?{$_.DisplayName -like 'LDAPCP*'}| fl DisplayName, Scope, Id, RootDirectory
```

Usually, only LDAPCP farm feature is listed:

```text
DisplayName   : LDAPCP
Scope         : Farm
Id            : b37e0696-f48c-47ab-aa30-834d78033ba8
RootDirectory : C:\Program Files\Common Files\Microsoft Shared\Web Server Extensions\16\Template\Features\LDAPCP
```

### Recreate missing feature folders and add feature.xml

For each feature listed, check if its "RootDirectory" actually exists in the file system of the current server. If it does not exist:

* Create the "RootDirectory" (e.g. "LDAPCP" in "Features" folder)
* Use [7-zip](http://www.7-zip.org/) to open LDAPCP.wsp and extract the feature.xml of the corresponding feature
* Copy the feature.xml into the "RootDirectory"

### Deactivate and remove the features

```powershell
# Deactivate LDAPCP features (it may thrown an error if feature is already deactivated)
Get-SPFeature| ?{$_.DisplayName -like 'LDAPCP*'}| Disable-SPFeature -Confirm:$false
# Uninstall LDAPCP features
Get-SPFeature| ?{$_.DisplayName -like 'LDAPCP*'}| Uninstall-SPFeature -Confirm:$false
```

Once all features are uninstalled, delete the "RootDirectory" folder(s) (in "16\Template\Features").

### Delete the LDAPCP persisted object

LDAPCP stores its configuration is its own persisted object. This stsadm command will delete it if it was not already deleted:

```
stsadm -o deleteconfigurationobject -id 5D306A02-A262-48AC-8C44-BDB927620227
```

### If desired, LDAPCP solution can now be safely removed

```powershell
Remove-SPSolution "LDAPCP.wsp" -Confirm:$false
```
