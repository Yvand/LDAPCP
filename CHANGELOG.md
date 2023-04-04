# Change log for LDAPCP

## Unreleased

* Augmentation is now enabled by default on a new LDAP connection, when it is added through the UI
* Augment groups with the same attribute as the one set in the LDAPCP configuration. https://github.com/Yvand/LDAPCP/issues/148
* Fix: In claims configurfation page, the values in the list of "PickerEntity metadata" was not populated correctly, which caused an issue with the "Title" (and a few others)
* Fix: During augmentation, connection to LDAP always used SimpleBind, regardless of the authentication settings
* Update NuGet package NUnit3TestAdapter to v3.16.1
* Update NuGet package Newtonsoft.Json to 12.0.3

## LDAPCP 14.1.20191007.981 enhancements & bug-fixes - Published in October 7, 2019

* Fix regression: after installing v14, users are stuck in SharePoint just after sign-in to ADFS. https://github.com/Yvand/LDAPCP/issues/99

## LDAPCP 14.0.20190821.952 enhancements & bug-fixes - Published in August 21, 2019

* Add method LDAPCPConfig.CreateDefaultConfiguration
* Fix bug: randomly, LDAPCP returned results that were missing their domain name. In SharePoint logs, a DirectoryServicesCOMException error was recorded. https://github.com/Yvand/LDAPCP/issues/87

## LDAPCP 13.0.20190621.905 enhancements & bug-fixes - Published in June 21, 2019

* Add a default mapping to populate the email of groups
* Update text in claims mapping page to better explain settings
* Fix bug: During augmentation, PrincipalContext is not built with expected ContextOptions if LDAPConnection.UseSPServerConnectionToAD is true
* Improve logging during augmentation
* Improve logging when testing augmentation doesn't return expected result
* Update DevOps build pipelines
* Improve code quality as per Codacy's static code analysis
* Update NuGet package NUnit from 3.11 to 3.12
* Make most of public members privates and replace them with public properties, to meet best practices
* Use reflection to copy configuration objects, whenever possible, to avoid misses when new properties are added

## LDAPCP 12.0.20190321.770 enhancements & bug-fixes - Published in March 21, 2019

* Add more strict checks on the claim type passed during augmentation and validation, to record a more meaningful error if needed
* Add test to ensure that LDAPCP augments only entities issued from the TrustedProvider it is associated with
* Fix sign-in of users failing if LDAPCP configuration does not exist
* Handle potential exception during augmentation if connection to LDAP server fails
* Improve managemend of special LDAP characters
* Add property CustomData to ILDAPCPConfiguration
* Fix msbuild warnings
* Improve tests
* Use Azure DevOps to build LDAPCP
* Cache result returned by FileVersionInfo.GetVersionInfo() to avoid potential hangs
* Add property AzureCPConfig.MaxSearchResultsCount to set max number of results returned to SharePoint during a search
* Cache domain name and domain FQDN of each LDAP Connection, to avoid repetitive and potentially slow queries to LDAP servers
* Deprecate method LDAPCP.SetLDAPConnection called each time a LDAP operation is about to occur. Instead, added a separate method LDAPCP.SetLDAPConnection, called only during initialization of the configuration. Domain name, domain FQDN and distinguishedName are retrieved and cached here.
* Improve global performance by caching domain name and domain FQDN of each LDAP Connection, to avoid repetitive and potentially slow queries to LDAP servers
* Update logging during augmentation, and split various LDAP operations into different SPMonitoredScope
* Update augmentatikon by getting, using and caching RootContainer for each DirectoryEntry object
* Do more fine-grained test when excluding a LDAP user missing the identity attribute
* Update NuGet package NUnit to v3.11
* Update NuGet package NUnit3TestAdapter to v3.13
* Update NuGet package CsvTools to v1.0.12

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
