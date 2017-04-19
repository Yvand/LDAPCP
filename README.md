 # LDAPCP for SharePoint 2013 and 2016
This claims provider queries Active Directory and LDAP servers to enhance people picker with a great search experience in trusted authentication (typically ADFS).
<br>It was formerly hosted on [Codeplex](https://ldapcp.codeplex.com/).

## Features
- Easy to configure with administration pages added in Central administration > Security. 
- Queries multiple servers in parallel (multi-threaded connections). 
- Populates properties (e.g. email, SIP, display name) upon permission creation. 
- Supports rehydration for provider-hosted add-ins. 
- Supports dynamics tokens "{domain}" and "{fqdn}" to add domain information on permissions to create. 
- Implements SharePoint logging infrastructure and logs messages in Area/Product "LDAPCP". 
- Ensures thread safety. 
- Implements augmentation to add group membership to security tokens.

## Customization capabilities
- Customize list of claim types, and their mapping with LDAP objects. 
- Enable/disable augmentation globally or per LDAP connection. 
- Customize display of permissions. 
- Customize LDAP filter per claim type, e.g. to only return users member of a specific security group. 
- Set a keyword to bypass LDAP lookup. e.g. input "extuser:partner@contoso.com" directly creates permission "partner@contoso.com" on claim type set for this. 
- Set a prefix to add to LDAP results, e.g. add "domain\" to groups returned by LDAP. 
- Hide disabled users and distribution lists. 
- Developers can easily do a lot more by inheriting base class.

## Important - Limitations
Due to limitations of SharePoint API, do not associate LDAPCP with more than 1 SPTrustedIdentityTokenIssuer. Developers can bypass this limitation by inheriting LDAPCP to create new claims providers (with different names). Read “Developers section” below for further information.

You must manually deploy ldapcp.dll on SharePoint servers that do not have SharePoint service "Microsoft SharePoint Foundation Web Application" started. You can use this PowerShell script:
```powershell
[System.Reflection.Assembly]::Load("System.EnterpriseServices, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")
$publish = New-Object System.EnterpriseServices.Internal.Publish
$publish.GacInstall("C:\Data\Dev\ldapcp.dll")
```

## How to install LDAPCP
- Download [latest release of LDAPCP.wsp](https://github.com/Yvand/LDAPCP/releases).
- Install and deploy the solution (that will automatically activate the "LDAPCP" farm-scoped feature):
```powershell
Add-SPSolution -LiteralPath "PATH TO WSP FILE"
Install-SPSolution -Identity "LDAPCP.wsp" -GACDeployment
```
- At this point claim provider is inactive and it must be associated to an SPTrustedIdentityTokenIssuer to work:
```powershell
$trust = Get-SPTrustedIdentityTokenIssuer "SPTRUST NAME"
$trust.ClaimProviderName = "LDAPCP"
$trust.Update()
```

## How to update LDAPCP
Run Update-SPSolution cmdlet to start a timer job that that will deploy the update:
```powershell
Update-SPSolution -GACDeployment -Identity "LDAPCP.wsp" -LiteralPath "C:\Data\Dev\LDAPCP.wsp"
```
You can monitor the progress in farm solutions page in central administration.

## How to remove LDAPCP
Randomly, SharePoint doesn’t uninstall the solution correctly: it removes the assembly too early and fails to call the feature receiver... When this happens, the claims provider is not removed and that causes issues when you re-install it.
To uninstall safely, **deactivate the farm feature before retracting the solution**:
```powershell
Disable-SPFeature -identity "LDAPCP"
Uninstall-SPSolution -Identity "LDAPCP.wsp"
# Wait for the timer job to complete
Remove-SPSolution -Identity "LDAPCP.wsp"
```
Validate that claims provider was removed:
```powershell
Get-SPClaimProvider| ft DisplayName
# If LDAPCP appears in cmdlet above, remove it:
Remove-SPClaimProvider LDAPCP
```

## Claims supported
LDAPCP has a default mapping between claim types and LDAP attributes, but this can be customized in “Claims table” page available in Central Administration/Security.

Default list:

| Claim type                                                                 | LDAP attribute name        | LDAP object class |
|----------------------------------------------------------------------------|----------------------------|-------------------|
| http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress         | mail                       | user              |
| http://schemas.microsoft.com/ws/2008/06/identity/claims/windowsaccountname | sAMAccountName             | user              |
| http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn                  | userPrincipalName          | user              |
| http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname            | givenName                  | user              |
| http://schemas.xmlsoap.org/ws/2005/05/identity/claims/locality             | physicalDeliveryOfficeName | user              |
| http://schemas.microsoft.com/ws/2008/06/identity/claims/role               | sAMAccountName             | group             |
| linked to identity claim                                                   | displayName                | user              |
| linked to identity claim                                                   | cn                         | user              |
| linked to identity claim                                                   | sn                         | user              |

None of the claim types above is mandatory in the SPTrust, but the identity claim must either be one of them, or added through LDAPCP admin pages.

To enhance search experience, LDAPCP also queries user input against common LDAP attributes such as the display name (displayName) and the common name (cn).

## Developers corner
Project has evolved a lot since it started, and now most of the customizations can be made with LDAPCP administration pages, but some still require custom development:
- Use LDAPCP with multiple SPTrustedIdentityTokenIssuer objects. 
- Fully customize the display text or the value of permissions created by LDAPCP.

For that, it is required to create a custom class that inherits LDAPCP class. "LDAPCP for Developers.zip" contains a Visual Studio project with sample classes that cover various scenarios. Only 1 inherited claim provider is installed at a time, you need to edit the feature event receiver to install the claim provider you want to test.
Common mistakes to avoid: 
- **Always deactivate the farm feature before retracting the solution** (see "how to remove" above to understand why). 
- If you create your own SharePoint solution, **DO NOT forget to include the ldapcp.dll assembly in the wsp package**.

If you did any of the mistakes above, you will likely experience issues when you try to redeploy the solution because the feature was already installed. All features in the solution must be uninstalled before it can be redeployed.

In any case, do not directly edit LDAPCP class, it has been designed to be inherited so that you can customize it to fit your needs. If a scenario that you need is not covered, please submit it in the discussions tab.
