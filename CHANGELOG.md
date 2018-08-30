# Change log for LDAPCP

## LDAPCP v11 enhancements & bug-fixes - Published in August 30, 2018

* Fixed no result returned under high load, caused by a thread safety issue where the same filter was used in all threads regardless of the actual input
* Fixed the augmentation that randomly failed under high load, caused by a thread safety issue on list ILDAPCPConfiguration.LDAPConnectionsProp
* Added handling of special characters for LDAP filters as documented in https://ldap.com/ldap-filters/
* Added the first nae (givenName) in the list of attributes queried by default
* Improved validation of changes made to ClaimTypes collection
* Added method ClaimTypeConfigCollection.GetByClaimType()
* Implemented unit tests
* Explicitely encode HTML messages shown in admin pages and renderred from server side code to comply with tools scanning code to detect security vulnerabilities
* Deactivating farm-scoped feature "LDAPCP" removes the claims provider from the farm, but it does not delete its configuration anymore. Configuration is now deleted when feature is uninstalled (typically when retracting the solution)
* Added user identifier properties in global configuration page

## LDAPCP v10 enhancements & bug-fixes - Published in June 12, 2018

* LDAPCP can be entirely [configured with PowerShell](https://ldapcp.com/Configure-LDAPCP.html), including claim types configuration
* LDAPCP administration pages were updated to be easier to udnerstand, especially the page that configures claim types.
* LDAPCP administration pages can now be easily reused by developers.
* Augmentation can now handle multiple group claim types, and uses the credentials set when LDAP connection was added.
* Number of results returned by LDAP servers is now limited to improve performance of LDAP servers.
* Logging is more relevant and generates less messages.
* **Beaking change**: Due to the amount of changes in this area, the claim types configuration will be reset if you update from an earlier version.
* Many bug fixes and optimizations
