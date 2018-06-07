# Troubleshoot LDAPCP

If LDAPCP doesn't work as expected, here are some troubleshooting tips.

## Check SharePoint logs

LDAPCP records all its activity in SharePoint logs, including the performance, queries and number of results returned per LDAP servers.
They can be returned by filtering on Product/Area "LDAPCP":

```powershell
Merge-SPLogFile -Path "C:\Temp\LDAPCP_logging.log" -Overwrite -Area "LDAPCP" -StartTime (Get-Date).AddDays(-1)
```

## Replay LDAP queries

If people picker doesn't return expected results, it ca be helpful to replay LDAP queries executed by LDAPCP (which are recorded in the logs) outside of SharePoint:

```powershell
$filter = "(&(objectClass=user)(|(sAMAccountName=yvand*)(cn=yvand*)))"
$ldapServer = "contoso.local"
$ldapBase = "DC=contoso,DC=local"
$ldapUser = "contoso\spfarm"
$ldapPassword = Read-Host "Enter the password (will appear in clear text)"
$ldapAuth = [System.DirectoryServices.AuthenticationTypes] "Secure, Signing"

$directoryEntry = New-Object System.DirectoryServices.DirectoryEntry("LDAP://$ldapServer/$ldapBase" , $ldapUser, $ldapPassword, $ldapAuth)
$objSearcher = New-Object System.DirectoryServices.DirectorySearcher ($directoryEntry, $filter)
# Uncomment line below to restrict properties returned by LDAP server
#$objSearcher.PropertiesToLoad.AddRange(@("cn"))

$results = $objSearcher.FindAll() 
Write-Host "Found $($results.Count) result(s)":
foreach ($objResult in $results)    {$objItem = $objResult.Properties; $objItem}
```

## Troubleshoot augmentation

If augmentation is enabled, LDAPCP gets group membership of federated users to populate 2 kind of tokens:

- SharePoint SAML token of users with their group membership when they sign-in
- Non-interactive SharePoint token: this internal token is used by some features like email alerts, "check permissions", incoming email, etc...

If augmentation does not work, here is how to troubleshoot it:

### Ensure augmentation is enabled in LDAPCP configuration page

Go to central administration > Security > LDAPCP global configuration page and validate that:

- Augmentation is enabled.
- A claim type for the groups is selected
- The LDAP/AD server is enabled for augmentation

### "Check permissions" feature is not working

This usually means that the non-interactive token doesn't contain the group membership. By default, it is valid for 1 day and it is refreshed only if it expired, so the 1st step is to lower its lifetime:

```powershell
$cs = [Microsoft.SharePoint.Administration.SPWebService]::ContentService
$cs.TokenTimeout = New-TimeSpan -Minutes 1
$cs.Update()
```

Then, open a new PowerShell console and call SPWeb.DoesUserHavePermissions(), which will trigger a refresh of the non-interactive if it is expired:

```powershell
$web = Get-SPWeb "http://spsites/"
$logon = "i:05.t|contoso.local|yvand@contoso.local"
# SPWeb.DoesUserHavePermissions() uses the non-interactive token, and will refresh it in PowerShell process if it is expired
$web.DoesUserHavePermissions($logon, [Microsoft.SharePoint.SPBasePermissions]::EditListItems)
```

### Check SharePoint logs

Filter SharePoint logs on Product/Area "LDAPCP" and Category "Augmentation". You can visualize logs in real time with [ULS Viewer](https://www.microsoft.com/en-us/download/details.aspx?id=44020) or use Merge-SPLogFile cmdlet:

```powershell
Merge-SPLogFile -Path "C:\Temp\LDAPCP_logging.log" -Overwrite -Area "LDAPCP" -Category "Augmentation" -StartTime (Get-Date).AddDays(-1)
```

The output of a successful augmentation looks like this (trimmed for visibility):

```text
LDAPCP  Augmentation    1337    Medium  [LDAPCP] Domain contoso.local returned 3 groups for user yvand@contoso.local. Lookup took 575ms on AD server 'LDAP://contoso.local/DC=contoso,DC=local'
LDAPCP  Augmentation    1337    Medium  [LDAPCP] Domain MyLDS.local returned 1 groups for user yvand@contoso.local. Lookup took 6ms on LDAP server 'LDAP://DC:50500/CN=Partition1,DC=MyLDS,DC=local'
LDAPCP  Augmentation    1337    Verbose [LDAPCP] LDAP queries to get group membership on all servers completed in 2391ms
LDAPCP  Augmentation    1337    Verbose [LDAPCP] Added group "contoso.local\Domain Users" to user "yvand@contoso.local"
LDAPCP  Augmentation    1337    Verbose [LDAPCP] Added group "contoso.local\Users" to user "yvand@contoso.local"
LDAPCP  Augmentation    1337    Verbose [LDAPCP] Added group "contoso.local\group1" to user "yvand@contoso.local"
LDAPCP  Augmentation    1337    Verbose [LDAPCP] Added group "MyLDS.local\ldsGroup1" to user "yvand@contoso.local"
LDAPCP  Augmentation    1337    Medium  [LDAPCP] User 'yvand@contoso.local' was augmented with 4 groups of claim type 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'
```
