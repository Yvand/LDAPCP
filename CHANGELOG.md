# Change log for LDAPCP

## LDAPCP v11 enhancements & bug-fixes

* Fixed no result returned under high load, caused by a thread safety issue where the same filter was used in all threads regardless of the actual input
* Added handling of special characters for LDAP filters as documented in https://ldap.com/ldap-filters/
* Improved validation of changes made to ClaimTypes collection
* Added method ClaimTypeConfigCollection.GetByClaimType()
* Implemented unit tests
* Explicitely encode HTML messages shown in admin pages and renderred from server side code to comply with tools scanning code to detect security vulnerabilities