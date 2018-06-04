# How to remove LDAPCP

> **Important:**  
> Start a **new PowerShell console** to ensure you use up to date persisted objects, this avoids concurrency update errors.  
> If something goes wrong, [check this page](Fix-setup-issues.html) to fix issues.

## Step 1: Reset property ClaimProviderName in the SPTrustedIdentityTokenIssuer

Unfortunately, the only supported way to reset property ClaimProviderName is to remove and recreate the SPTrustedIdentityTokenIssuer object, which requires to remove the trust from all the zones where it is used first, which is time consuming.  
Alternatively, it's possible to use reflection to reset this property, but it is not supported and you do this at your own risks. Here is the script:

```powershell
$trust = Get-SPTrustedIdentityTokenIssuer "SPTRUST NAME"
$trust.GetType().GetField("m_ClaimProviderName", "NonPublic, Instance").SetValue($trust, $null)
$trust.Update()
```

## Step 2: Uninstall LDAPCP

Randomly, SharePoint doesn’t uninstall the solution correctly: it removes the assembly too early and fails to call the feature receiver... When this happens, the claims provider is not removed and that causes issues when you re-install it.

To uninstall safely, **deactivate the farm feature before retracting the solution**:

```powershell
Disable-SPFeature -identity "LDAPCP"
Uninstall-SPSolution -Identity "LDAPCP.wsp"
# Wait for the timer job to complete
Remove-SPSolution -Identity "LDAPCP.wsp"
```
