# How to update LDAPCP
Run Update-SPSolution cmdlet to start a timer job that that will deploy the update:
```powershell
Update-SPSolution -GACDeployment -Identity "LDAPCP.wsp" -LiteralPath "C:\Data\Dev\LDAPCP.wsp"
```
You can monitor the progress in farm solutions page in central administration.

> **Note:** If something goes wrong, [check this page](Fix-setup-issues.html) to resolve problems.
