# How to update LDAPCP

> **Important:**  
> Start a **new PowerShell console** to ensure it uses up to date persisted objects, this avoids concurrency update errors.  
> Version 10 has breaking changes. If you update from an earlier version, claim type configuration list will be reset.  
> If something goes wrong, [check this page](Fix-setup-issues.html) to fix issues.

1. Update solution

Run Update-SPSolution cmdlet to start a timer job that that will deploy the update:

```powershell
Update-SPSolution -GACDeployment -Identity "LDAPCP.wsp" -LiteralPath "F:\Data\Dev\LDAPCP.wsp"
```

2. Restart IIS service on each SharePoint server

If some SharePoint servers do not run SharePoint service "Microsoft SharePoint Foundation Web Application", ldapcp.dll must be manually updated in their GAC as [documented in install page](Install-LDAPCP.html).
