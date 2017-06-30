# Configure LDAPCP
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
