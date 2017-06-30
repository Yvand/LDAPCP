# How to install LDAPCP
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

## Important - Limitations
Due to limitations of SharePoint API, do not associate LDAPCP with more than 1 SPTrustedIdentityTokenIssuer. Developers can bypass this limitation by inheriting LDAPCP to create new claims providers (with different names). Read “Developers section” below for further information.

You must manually deploy ldapcp.dll on SharePoint servers that do not have SharePoint service "Microsoft SharePoint Foundation Web Application" started. You can use this PowerShell script:
```powershell
[System.Reflection.Assembly]::Load("System.EnterpriseServices, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")
$publish = New-Object System.EnterpriseServices.Internal.Publish
$publish.GacInstall("C:\Data\Dev\ldapcp.dll")
```

> **Note:** If something goes wrong, [check this page](Fix-setup-issues.html) to resolve problems.

