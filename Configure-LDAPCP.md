# Configure LDAPCP with administration pages

LDAPCP comes with 2 administration pages added in central administration > Security:

- Global configuration: Add/remove LDAP servers and configure general settings
- Claim types configuration: Define the claim types, and their mapping with LDAP attributes

# Configure LDAPCP with PowerShell

Starting with v10, LDAPCP can be configured with PowerShell:

```powershell
Add-Type -AssemblyName "ldapcp, Version=1.0.0.0, Culture=neutral, PublicKeyToken=80be731bc1a1a740"
$config = [ldapcp.LDAPCPConfig]::GetConfiguration("LDAPCPConfig")

# To view current configuration
$config
$config.ClaimTypes

# Update some settings, e.g. configure augmentation:
$config.EnableAugmentation = $true
$config.MainGroupClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
$config.Update()

# Reset claim types configuration list to default
$config.ResetClaimTypesList()
$config.Update()

# Reset the whole configuration to default
$config.ResetCurrentConfiguration()
$config.Update()

# Add a new entry to the claim types configuration list
$newCTConfig = New-Object ldapcp.ClaimTypeConfig
$newCTConfig.ClaimType = "ClaimTypeValue"
$newCTConfig.EntityType = [ldapcp.DirectoryObjectType]::User
$newCTConfig.LDAPClass = "LDAPClassVALUE"
$newCTConfig.LDAPAttribute = "LDAPAttributeVALUE"
$config.ClaimTypes.Add($newCTConfig)
$config.Update()

# Remove a claim type from the claim types configuration list
$config.ClaimTypes.Remove("ClaimTypeValue")
$config.Update()
```

LDAPCP configuration is stored as a persisted object in SharePoint configuration database, and it can be returned with this SQL command:

```sql
SELECT Id, Name, cast (properties as xml) AS XMLProps FROM Objects WHERE Name = 'LdapcpConfig'
```
