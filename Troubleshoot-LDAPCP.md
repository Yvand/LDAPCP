# Troubleshoot LDAPCP
If LDAPCP doesn't work as expected, here are some steps that can be used for troubleshooting.

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
if ($ldapPassword -eq $null) { $ldapPassword = Read-Host "Enter the password (will appear in clear text)" }
$ldapAuth = [System.DirectoryServices.AuthenticationTypes] "Secure, Signing"

$directoryEntry = New-Object System.DirectoryServices.DirectoryEntry("LDAP://$ldapServer/$ldapBase" , $ldapUser, $ldapPassword, $ldapAuth)
$objSearcher = New-Object System.DirectoryServices.DirectorySearcher ($directoryEntry, $filter)
$objSearcher.PropertiesToLoad.AddRange(@("cn"))

$results = $objSearcher.FindAll() 
Write-Host "Found $($results.Count) result(s)":
foreach ($objResult in $results)    {$objItem = $objResult.Properties; $objItem}
```
