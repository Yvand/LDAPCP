# How to remove LDAPCP
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

> **Note:** If something goes wrong, [check this page](Fix-setup-issues.html) to resolve problems.
