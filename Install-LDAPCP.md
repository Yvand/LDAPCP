# How to install LDAPCP

> **Important:**  
> Start a **new PowerShell console** to ensure you use up to date persisted objects, this avoids concurrency update errors.  
> If something goes wrong, [check this page](Fix-setup-issues.html) to fix issues.

- Download LDAPCP.wsp.
- Install and deploy the solution:

```powershell
Add-SPSolution -LiteralPath "F:\Data\Dev\LDAPCP.wsp"
Install-SPSolution -Identity "LDAPCP.wsp" -GACDeployment
```

- Associate LDAPCP with a SPTrustedIdentityTokenIssuer:

```powershell
$trust = Get-SPTrustedIdentityTokenIssuer "SPTRUST NAME"
$trust.ClaimProviderName = "LDAPCP"
$trust.Update()
```

## Important - Limitations

- Due to limitations of SharePoint API, do not associate LDAPCP with more than 1 SPTrustedIdentityTokenIssuer. Developers can bypass this limitation, [check this page](For-Developers.html) for more information.

- You must manually install ldapcp.dll in the GAC of SharePoint servers that do not run SharePoint service "Microsoft SharePoint Foundation Web Application".

You can extract ldapcp.dll from LDAPCP.wsp using [7-zip](https://www.7-zip.org/), and install it in the GAC using this PowerShell script:

```powershell
[System.Reflection.Assembly]::Load("System.EnterpriseServices, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")
$publish = New-Object System.EnterpriseServices.Internal.Publish
$publish.GacInstall("F:\Data\Dev\ldapcp.dll")
```
