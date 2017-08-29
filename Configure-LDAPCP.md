# Configure LDAPCP

LDAPCP has a default mapping between claim types and LDAP attributes:

| Claim type                                                                 | LDAP attribute name        | LDAP object class |
|----------------------------------------------------------------------------|----------------------------|-------------------|
| http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress         | mail                       | user              |
| http://schemas.microsoft.com/ws/2008/06/identity/claims/windowsaccountname | sAMAccountName             | user              |
| http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn                  | userPrincipalName          | user              |
| http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname            | givenName                  | user              |
| http://schemas.microsoft.com/ws/2008/06/identity/claims/role               | sAMAccountName             | group             |
| linked to identity claim                                                   | displayName                | user              |
| linked to identity claim                                                   | cn                         | user              |
| linked to identity claim                                                   | sn                         | user              |
| linked to identity claim                                                   | displayName                | group             |

<br />
This list can be customized to fit your environment in Central Administration > Security > "Claims mapping" page.

- The identity claim type is displayed in bold green, it is mandatory since it uniquely identifies trusted users.
- Claim types in green are defined in the SPTrustedLoginProvider set with LDAPCP.
- Claim types in red are not defined in the SPTrustedLoginProvider set with LDAPCP, so they can be safely deleted from LDAPCP.
- "linked to identity claim": To enhance search experience, LDAPCP also queries common LDAP attributes such as the display name (displayName) and the common name (cn).

If possible, only use indexed LDAP attributes to guarantee best performance during LDAP queries. For Active Directory, indexed attribute are listed in [this page](https://msdn.microsoft.com/en-us/library/ms675095.aspx).
