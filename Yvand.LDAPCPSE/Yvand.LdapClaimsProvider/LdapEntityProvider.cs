using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
using Microsoft.SharePoint.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Yvand.LdapClaimsProvider.Configuration;
using Yvand.LdapClaimsProvider.Logging;
using WIF4_5 = System.Security.Claims;

namespace Yvand.LdapClaimsProvider
{
    public class LdapEntityProvider : EntityProviderBase
    {
        private IClaimsProviderSettings Settings { get; }
        public LdapEntityProvider(string claimsProviderName, IClaimsProviderSettings settings) : base(claimsProviderName)
        {
            this.Settings = settings;
        }

        public override List<string> GetEntityGroups(OperationContext currentContext)
        {
            Stopwatch timer = new Stopwatch();
            timer.Start();
            List<string> groups = new List<string>();
            object lockResults = new object();

            // Creates 1 synchronous action per DirectoryConnection
            Parallel.ForEach(currentContext.LdapConnections, ldapConnection =>
            {
                List<string> directoryGroups = new List<string>();
                if (ldapConnection.GetGroupMembershipUsingDotNetHelpers)
                {
                    directoryGroups = GetGroupsFromActiveDirectory(ldapConnection, currentContext);
                }
                else
                {
                    directoryGroups = GetGroupsFromLDAPDirectory(ldapConnection, currentContext);
                }

                lock (lockResults)
                {
                    groups.AddRange(directoryGroups);
                }
            });
            return groups;
        }

        /// <summary>
        /// Get group membership using UserPrincipal.GetAuthorizationGroups(), which works only with AD
        /// UserPrincipal.GetAuthorizationGroups() gets groups using Kerberos protocol transition (preferred way), and falls back to LDAP queries otherwise.
        /// </summary>
        /// <param name="ldapConnection"></param>
        /// <param name="currentContext"></param>
        /// <returns></returns>
        protected virtual List<string> GetGroupsFromActiveDirectory(DirectoryConnection ldapConnection, OperationContext currentContext)
        {
            // Convert AuthenticationTypes to ContextOptions. Mapping updated based on https://github.com/Yvand/LDAPCP/issues/232
            // AuthenticationTypes Enum: https://learn.microsoft.com/en-us/dotnet/api/system.directoryservices.authenticationtypes?view=netframework-4.8.1
            // ContextOptions Enum: https://learn.microsoft.com/en-us/dotnet/api/system.directoryservices.accountmanagement.contextoptions?view=netframework-4.8.1
            ContextOptions contextOptions = new ContextOptions();
            // Step 1: set the authentication protocol
            if ((ldapConnection.AuthenticationType & AuthenticationTypes.Anonymous) == AuthenticationTypes.Anonymous)
            {
                contextOptions = 0;
            }
            else if ((ldapConnection.AuthenticationType & AuthenticationTypes.Secure) == AuthenticationTypes.Secure)
            {
                contextOptions = ContextOptions.Negotiate;
            }
            else
            {
                contextOptions = ContextOptions.SimpleBind;
            }

            // Step 2: set the authentication options
            if ((ldapConnection.AuthenticationType & AuthenticationTypes.SecureSocketsLayer) == AuthenticationTypes.SecureSocketsLayer) { contextOptions |= ContextOptions.SecureSocketLayer; }
            if ((ldapConnection.AuthenticationType & AuthenticationTypes.Sealing) == AuthenticationTypes.Sealing) { contextOptions |= ContextOptions.Sealing; }
            if ((ldapConnection.AuthenticationType & AuthenticationTypes.ServerBind) == AuthenticationTypes.ServerBind) { contextOptions |= ContextOptions.ServerBind; }
            if ((ldapConnection.AuthenticationType & AuthenticationTypes.Signing) == AuthenticationTypes.Signing) { contextOptions |= ContextOptions.Signing; }

            List<string> groups = new List<string>();
            string logMessageCredentials = ldapConnection.UseDefaultADConnection ? "process identity" : ldapConnection.Username;
            string directoryDetails = $"from AD domain \"{ldapConnection.DomainFQDN}\" (authenticate as \"{logMessageCredentials}\" with AuthenticationType \"{contextOptions}\").";
            Logger.Log($"[{ClaimsProviderName}] Getting AD groups of user \"{currentContext.IncomingEntity.Value}\" {directoryDetails}", TraceSeverity.Verbose, TraceCategory.Augmentation);
            using (new SPMonitoredScope($"[{ClaimsProviderName}] Get AD groups of user \"{currentContext.IncomingEntity.Value}\" {directoryDetails}", 2000))
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();
                UserPrincipal adUser = null;
                PrincipalContext principalContext = null;
                try
                {
                    // Fix https://github.com/Yvand/LDAPCP/issues/34 : GetAuthorizationGroups() must be encapsulated in RunWithElevatedPrivileges to avoid PrincipalOperationException: While trying to retrieve the authorization groups, an error (5) occurred.
                    // It also fixes this access denied in the constructor of PrincipalContext(ContextType, String): DirectoryServicesCOMException (0x80072020): An operations error occurred.
                    SPSecurity.RunWithElevatedPrivileges(delegate ()
                    {
                        using (new SPMonitoredScope($"[{ClaimsProviderName}] Get AD Principal of user {currentContext.IncomingEntity.Value} " + directoryDetails, 1000))
                        {
                            // Constructor of PrincipalContext does connect to LDAP server and may throw an exception if it fails, so it should be in try/catch
                            // To use ContextOptions in constructor of PrincipalContext, "container" must also be set, but it can be null as per tests in https://stackoverflow.com/questions/2538064/active-directory-services-principalcontext-what-is-the-dn-of-a-container-o
                            // Tests: if "container" is null, it always fails in PowerShell (tested only with AD) but somehow it works fine here
                            if (ldapConnection.UseDefaultADConnection)
                            {
                                principalContext = new PrincipalContext(ContextType.Domain, ldapConnection.DomainFQDN, ldapConnection.DomaindistinguishedName, contextOptions);
                            }
                            else
                            {
                                principalContext = new PrincipalContext(ContextType.Domain, ldapConnection.DomainFQDN, ldapConnection.DomaindistinguishedName, contextOptions, ldapConnection.Username, ldapConnection.Password);
                            }

                            // https://github.com/Yvand/LDAPCP/issues/22: UserPrincipal.FindByIdentity() doesn't support emails, so if IncomingEntity is an email, user needs to be retrieved in a different way
                            if (SPClaimTypes.Equals(currentContext.IncomingEntity.ClaimType, WIF4_5.ClaimTypes.Email))
                            {
                                using (UserPrincipal userEmailPrincipal = new UserPrincipal(principalContext) { Enabled = true, EmailAddress = currentContext.IncomingEntity.Value })
                                {
                                    using (PrincipalSearcher userEmailSearcher = new PrincipalSearcher(userEmailPrincipal))
                                    {
                                        adUser = userEmailSearcher.FindOne() as UserPrincipal;
                                    }
                                }
                            }
                            else
                            {
                                adUser = UserPrincipal.FindByIdentity(principalContext, currentContext.IncomingEntity.Value);
                            }
                        }

                        if (adUser == null)
                        {
                            stopWatch.Stop();
                            return;
                        }

                        IEnumerable<Principal> adGroups = null;
                        using (new SPMonitoredScope($"[{ClaimsProviderName}] Get group membership of \"{currentContext.IncomingEntity.Value}\" " + directoryDetails, 1000))
                        {
                            adGroups = adUser.GetAuthorizationGroups();
                        }

                        if (adGroups == null)
                        {
                            stopWatch.Stop();
                            return;
                        }

                        using (new SPMonitoredScope($"[{ClaimsProviderName}] Process {adGroups.Count()} AD groups returned for user \"{currentContext.IncomingEntity.Value}\" " + directoryDetails + " Eeach AD group triggers a specific LDAP operation.", 1000))
                        {
                            // https://docs.microsoft.com/en-us/dotnet/api/system.directoryservices.accountmanagement.userprincipal.getauthorizationgroups?view=netframework-4.7.1#System_DirectoryServices_AccountManagement_UserPrincipal_GetAuthorizationGroups
                            // UserPrincipal.GetAuthorizationGroups() only returns security groups, and includes nested groups and special groups like "Domain Users".
                            // The foreach calls AccountManagement.FindResultEnumerator`1.get_Current() that does LDAP binds (call to DirectoryEntry.Bind()) for each group
                            // It may impact performance if there are many groups and/or if DC is slow
                            foreach (Principal adGroup in adGroups)
                            {
                                string groupDomainName, groupDomainFqdn;

                                // https://github.com/Yvand/LDAPCP/issues/148 - the group property used for the group groupValueDistinguishedName should be based on the LDAPCP configuration
                                // By default it should be the SamAccountName, since it's also the default attribute set in LDAPCP configuration
                                string claimValue = adGroup.SamAccountName;
                                switch (this.Settings.GroupIdentifierClaimTypeConfig.DirectoryObjectAttribute.ToLower())
                                {
                                    case "name":
                                        claimValue = adGroup.Name;
                                        break;

                                    case "distinguishedname":
                                        claimValue = adGroup.DistinguishedName;
                                        break;

                                    case "samaccountname":
                                        claimValue = adGroup.SamAccountName;
                                        break;

                                    case "objectsid":
                                        claimValue = adGroup.Sid.Value;
                                        break;
                                }

                                if (!String.IsNullOrEmpty(this.Settings.GroupIdentifierClaimTypeConfig.ClaimValueLeadingToken))
                                {
                                    // Principal.DistinguishedName is used to build the domain name / FQDN of the current group. Example of groupValueDistinguishedName: CN=group1,CN=Users,DC=contoso,DC=local
                                    string groupDN = adGroup.DistinguishedName;
                                    if (String.IsNullOrEmpty(groupDN)) { continue; }

                                    Utils.GetDomainInformation(groupDN, out groupDomainName, out groupDomainFqdn);
                                    if (this.Settings.GroupIdentifierClaimTypeConfig.ClaimValueLeadingToken.Contains(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME))
                                    {
                                        claimValue = this.Settings.GroupIdentifierClaimTypeConfig.ClaimValueLeadingToken.Replace(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME, groupDomainName) + claimValue;
                                    }
                                    else if (this.Settings.GroupIdentifierClaimTypeConfig.ClaimValueLeadingToken.Contains(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN))
                                    {
                                        claimValue = this.Settings.GroupIdentifierClaimTypeConfig.ClaimValueLeadingToken.Replace(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN, groupDomainFqdn) + claimValue;
                                    }
                                }
                                groups.Add(claimValue);
                            }
                        }
                    });
                }
                catch (PrincipalOperationException ex)
                {
                    Logger.LogException(ClaimsProviderName, $"while getting AD groups of user \"{currentContext.IncomingEntity.Value}\" using UserPrincipal.GetAuthorizationGroups() {directoryDetails} This is likely due to a bug in .NET framework in UserPrincipal.GetAuthorizationGroups (as of v4.6.1), especially if user is member (directly or not) of a group either in a child domain that was migrated, or a group that has special (deny) entities.", TraceCategory.Augmentation, ex);
                    // In this case, fallback to LDAP method to get group membership.
                    return GetGroupsFromLDAPDirectory(ldapConnection, currentContext);
                }
                catch (PrincipalServerDownException ex)
                {
                    Logger.LogException(ClaimsProviderName, $"while getting AD groups of user \"{currentContext.IncomingEntity.Value}\" using UserPrincipal.GetAuthorizationGroups() {directoryDetails} Is this server an Active LdapEntry server?", TraceCategory.Augmentation, ex);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ClaimsProviderName, $"while getting AD groups of user \"{currentContext.IncomingEntity.Value}\" using UserPrincipal.GetAuthorizationGroups() {directoryDetails}", TraceCategory.Augmentation, ex);
                }
                finally
                {
                    if (principalContext != null) { principalContext.Dispose(); }
                    if (adUser != null) { adUser.Dispose(); }

                    stopWatch.Stop();
                    Logger.Log($"[{ClaimsProviderName}] Got and processed {groups.Count} group(s) for user \"{currentContext.IncomingEntity.Value}\" in {stopWatch.ElapsedMilliseconds.ToString()} ms {directoryDetails}",
                        TraceSeverity.Medium, TraceCategory.Augmentation);
                }
            }
            return groups;
        }

        /// <summary>
        /// Returns the name of the groups the user is directly member of.
        /// </summary>
        /// <param name="ldapConnection">LDAP server to query</param>
        /// <param name="currentContext">Information about current context and operation</param>
        /// <returns></returns>
        protected virtual List<string> GetGroupsFromLDAPDirectory(DirectoryConnection ldapConnection, OperationContext currentContext)
        {
            List<string> groups = new List<string>();
            string ldapFilter = string.Format("(&(ObjectClass={0}) ({1}={2}){3})", this.Settings.UserIdentifierClaimTypeConfig.DirectoryObjectClass, this.Settings.UserIdentifierClaimTypeConfig.DirectoryObjectAttribute, currentContext.IncomingEntity.Value, this.Settings.UserIdentifierClaimTypeConfig.DirectoryObjectAdditionalFilter);
            string logMessageCredentials = String.IsNullOrWhiteSpace(ldapConnection.LdapEntry.Username) ? "process identity" : ldapConnection.LdapEntry.Username;
            string directoryDetails = $"from LDAP server \"{ldapConnection.LdapEntry.Path}\" with LDAP filter \"{ldapFilter}\" (authenticate as \"{logMessageCredentials}\" with AuthenticationType \"{ldapConnection.LdapEntry.AuthenticationType}\").";
            Logger.Log($"[{ClaimsProviderName}] Getting LDAP groups of user \"{currentContext.IncomingEntity.Value}\" {directoryDetails}", TraceSeverity.Verbose, TraceCategory.Augmentation);
            using (new SPMonitoredScope($"[{ClaimsProviderName}] Get LDAP groups of user \"{currentContext.IncomingEntity.Value}\" {directoryDetails}", 1000))
            {
                SPSecurity.RunWithElevatedPrivileges(delegate ()
                {
                    Stopwatch stopWatch = new Stopwatch();
                    stopWatch.Start();
                    try
                    {
                        using (DirectoryEntry directory = ldapConnection.LdapEntry)
                        {
                            using (DirectorySearcher searcher = new DirectorySearcher(directory))
                            {
                                searcher.ClientTimeout = new TimeSpan(0, 0, this.Settings.Timeout);
                                searcher.Filter = ldapFilter;
                                foreach (string memberOfPropertyName in ldapConnection.GroupMembershipLdapAttributes)
                                {
                                    searcher.PropertiesToLoad.Add(memberOfPropertyName);
                                }
                                searcher.PropertiesToLoad.Add(this.Settings.GroupIdentifierClaimTypeConfig.DirectoryObjectAttribute);

                                SearchResult ldapResult;
                                using (new SPMonitoredScope($"[{ClaimsProviderName}] Get group membership of user \"{currentContext.IncomingEntity.Value}\" {directoryDetails}", 1000))
                                {
                                    ldapResult = searcher.FindOne();
                                }

                                if (ldapResult == null)
                                {
                                    stopWatch.Stop();
                                    return;  // User was not found in this LDAP server
                                }

                                using (new SPMonitoredScope($"[{ClaimsProviderName}] Process LDAP groups of user \"{currentContext.IncomingEntity.Value}\" returned {directoryDetails}", 1000))
                                {
                                    // Verify if memberOf attribte is present, and how many values it has
                                    int memberOfValuesCount = 0;
                                    ResultPropertyValueCollection groupValueDistinguishedNameList = null;
                                    foreach (string groupMembershipAttributes in ldapConnection.GroupMembershipLdapAttributes)
                                    {
                                        if (ldapResult.Properties.Contains(groupMembershipAttributes))
                                        {
                                            memberOfValuesCount = ldapResult.Properties[groupMembershipAttributes].Count;
                                            groupValueDistinguishedNameList = ldapResult.Properties[groupMembershipAttributes];
                                            break;
                                        }
                                    }
                                    if (groupValueDistinguishedNameList == null) { return; } // No memberof attribute found

                                    // Go through each memberOf value
                                    List<string> groupsProcessed = new List<string>();
                                    string memberGroupDistinguishedName;
                                    for (int idx = 0; idx < memberOfValuesCount; idx++)
                                    {
                                        memberGroupDistinguishedName = groupValueDistinguishedNameList[idx].ToString();
                                        groups.AddRange(ProcessGroupAndGetMembersGroupsRecursive(ldapConnection, currentContext, memberGroupDistinguishedName, groupsProcessed));
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ClaimsProviderName, $"while getting LDAP groups of user \"{currentContext.IncomingEntity.Value}\" {directoryDetails}", TraceCategory.Augmentation, ex);
                    }
                    finally
                    {
                        if (ldapConnection.LdapEntry != null)
                        {
                            ldapConnection.LdapEntry.Dispose();
                        }
                        stopWatch.Stop();
                        Logger.Log($"[{ClaimsProviderName}] Got {groups.Count} group(s) for user \"{currentContext.IncomingEntity.Value}\" in {stopWatch.ElapsedMilliseconds.ToString()} ms {directoryDetails}",
                            TraceSeverity.Medium, TraceCategory.Augmentation);
                    }
                });
            }
            return groups;
        }

        private List<string> ProcessGroupAndGetMembersGroupsRecursive(DirectoryConnection ldapConnection, OperationContext currentContext, string groupValueDistinguishedName, List<string> groupsProcessed)
        {
            List<string> memberGroups = new List<string>();
            if (groupsProcessed.Contains(groupValueDistinguishedName))
            {
                return memberGroups;
            }
            else
            {
                groupsProcessed.Add(groupValueDistinguishedName);
            }

            try
            {
                string groupDnPath = $"{ldapConnection.LdapEntryServerAndPort}/{groupValueDistinguishedName}";
                using (DirectoryEntry deCurrentGroup = ldapConnection.GetDirectoryEntry(groupDnPath))
                {
                    using (DirectorySearcher searcher = new DirectorySearcher())
                    {
                        searcher.SearchRoot = deCurrentGroup;
                        searcher.Filter = "(&(distinguishedName=" + groupValueDistinguishedName + "))";
                        searcher.SearchScope = SearchScope.Base;
                        foreach (string memberOfPropertyName in ldapConnection.GroupMembershipLdapAttributes)
                        {
                            searcher.PropertiesToLoad.Add(memberOfPropertyName);
                        }
                        searcher.PropertiesToLoad.Add(this.Settings.GroupIdentifierClaimTypeConfig.DirectoryObjectAttribute);
                        SearchResult ldapGroupResult = searcher.FindOne();
                        if (null == ldapGroupResult)
                        {
                            return memberGroups;
                        }

                        // Extract the value to use for the permission
                        if (ldapGroupResult.Properties.Contains(this.Settings.GroupIdentifierClaimTypeConfig.DirectoryObjectAttribute))
                        {
                            object ldapPermissionValueRaw = ldapGroupResult.Properties[this.Settings.GroupIdentifierClaimTypeConfig.DirectoryObjectAttribute][0];
                            string ldapPermissionValue = Utils.GetLdapValueAsString(ldapPermissionValueRaw, this.Settings.GroupIdentifierClaimTypeConfig.DirectoryObjectAttribute);
                            if (!String.IsNullOrEmpty(ldapPermissionValue))
                            {
                                string memberGroupDomainName, memberGroupDomainFqdn;
                                Utils.GetDomainInformation(groupValueDistinguishedName, out memberGroupDomainName, out memberGroupDomainFqdn);
                                if (!String.IsNullOrEmpty(this.Settings.GroupIdentifierClaimTypeConfig.ClaimValueLeadingToken) && this.Settings.GroupIdentifierClaimTypeConfig.ClaimValueLeadingToken.Contains(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME))
                                {
                                    ldapPermissionValue = this.Settings.GroupIdentifierClaimTypeConfig.ClaimValueLeadingToken.Replace(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME, memberGroupDomainName) + ldapPermissionValue;
                                }
                                else if (!String.IsNullOrEmpty(this.Settings.GroupIdentifierClaimTypeConfig.ClaimValueLeadingToken) && this.Settings.GroupIdentifierClaimTypeConfig.ClaimValueLeadingToken.Contains(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN))
                                {
                                    ldapPermissionValue = this.Settings.GroupIdentifierClaimTypeConfig.ClaimValueLeadingToken.Replace(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN, memberGroupDomainFqdn) + ldapPermissionValue;
                                }
                                memberGroups.Add(ldapPermissionValue);
                            }
                        }

                        // Search the memberOf property in the LDAP attributes of the group returned by LDAP
                        // And count how many values this attribute contains
                        int idx = 0;
                        ResultPropertyValueCollection groupValueDistinguishedNameList = null;
                        foreach (string groupMembershipAttributes in ldapConnection.GroupMembershipLdapAttributes)
                        {
                            if (ldapGroupResult.Properties.Contains(groupMembershipAttributes))
                            {
                                idx = ldapGroupResult.Properties[groupMembershipAttributes].Count;
                                groupValueDistinguishedNameList = ldapGroupResult.Properties[groupMembershipAttributes];
                                break;
                            }
                        }
                        if (groupValueDistinguishedNameList == null) { return memberGroups; } // No memberof attribute found, this is not a group

                        // For each value in the memberOf property
                        string memberGroupDistinguishedName;
                        for (int propertyCounter = 0; propertyCounter < idx; propertyCounter++)
                        {
                            memberGroupDistinguishedName = groupValueDistinguishedNameList[propertyCounter].ToString();
                            memberGroups.AddRange(ProcessGroupAndGetMembersGroupsRecursive(ldapConnection, currentContext, memberGroupDistinguishedName, groupsProcessed));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ClaimsProviderName, $"while processing LDAP group \"{groupValueDistinguishedName}\" of user \"{currentContext.IncomingEntity.Value}\"", TraceCategory.Augmentation, ex);
            }
            return memberGroups;
        }

        public override List<LdapEntityProviderResult> SearchOrValidateEntities(OperationContext currentContext)
        {
            if (String.IsNullOrWhiteSpace(currentContext.Input))
            {
                return new List<LdapEntityProviderResult>(0);
            }

            //string ldapFilter = this.BuildFilter(currentContext);
            List<LdapEntityProviderResult> LdapSearchResult = null;
            SPSecurity.RunWithElevatedPrivileges(delegate ()
            {
                LdapSearchResult = this.QueryLDAPServers(currentContext);
            });
            return LdapSearchResult;
        }

        protected string BuildFilter(List<ClaimTypeConfig> claimTypeConfigList, string inputText, bool exactSearch, DirectoryConnection ldapConnection)
        {
            if (ldapConnection != null && String.IsNullOrWhiteSpace(ldapConnection.CustomFilter))
            {  // In this case, the generic LDAP filter can be used
                return String.Empty;
            }

            StringBuilder filter = new StringBuilder();
            if (this.Settings.FilterEnabledUsersOnly)
            {
                filter.Append(ClaimsProviderConstants.LDAPFilterEnabledUsersOnly);
            }

            // A LDAP connection may have a custom filter
            if (!String.IsNullOrWhiteSpace(ldapConnection?.CustomFilter))
            {
                filter.Append(ldapConnection.CustomFilter);
            }

            filter.Append("(| ");   // START OR

            // Fix bug https://github.com/Yvand/LDAPCP/issues/53 by escaping special characters with their hex representation as documented in https://ldap.com/ldap-filters/
            string input = Utils.EscapeSpecialCharacters(inputText);

            foreach (var ctConfig in claimTypeConfigList)
            {
                filter.Append(AddLdapAttributeToFilter(exactSearch, input, ctConfig));
            }

            if (this.Settings.FilterEnabledUsersOnly)
            {
                filter.Append(")");
            }
            filter.Append(")");     // END OR

            return filter.ToString();
        }

        protected string AddLdapAttributeToFilter(bool exactSearch, string input, ClaimTypeConfig attributeConfig)
        {
            // Prevent use of wildcard for LDAP attributes which do not support it
            if (String.Equals(attributeConfig.DirectoryObjectAttribute, "objectSid", StringComparison.InvariantCultureIgnoreCase))
            {
                attributeConfig.DirectoryObjectAttributeSupportsWildcard = false; // For objectSid, no wildcard possible
            }
            else if (String.Equals(attributeConfig.DirectoryObjectAttribute, "primaryGroupID", StringComparison.InvariantCultureIgnoreCase))
            {
                attributeConfig.DirectoryObjectAttributeSupportsWildcard = false; // For primaryGroupID, no wildcard possible
            }

            // Test if wildcard(s) should be added to the input
            string inputFormatted;
            if (exactSearch || !attributeConfig.DirectoryObjectAttributeSupportsWildcard)
            {
                inputFormatted = input;
            }
            else
            {
                inputFormatted = this.Settings.AddWildcardAsPrefixOfInput ? "*" + input + "*" : input + "*";
            }

            // Append an additional LDAP filter if needed
            string additionalFilter = String.Empty;
            if (this.Settings.FilterSecurityGroupsOnly && String.Equals(attributeConfig.DirectoryObjectClass, "group", StringComparison.OrdinalIgnoreCase))
            {
                additionalFilter = ClaimsProviderConstants.LDAPFilterADSecurityGroupsOnly;
            }
            if (!String.IsNullOrWhiteSpace(attributeConfig.DirectoryObjectAdditionalFilter))
            {
                additionalFilter += attributeConfig.DirectoryObjectAdditionalFilter;
            }

            string filter = String.Format(ClaimsProviderConstants.LDAPFilter, attributeConfig.DirectoryObjectAttribute, inputFormatted, attributeConfig.DirectoryObjectClass, additionalFilter);
            return filter;
        }

        protected List<LdapEntityProviderResult> QueryLDAPServers(OperationContext currentContext)
        {
            if (currentContext.LdapConnections == null || currentContext.LdapConnections.Count == 0) { return null; }
            object lockResults = new object();
            List<LdapEntityProviderResult> results = new List<LdapEntityProviderResult>();
            Stopwatch globalStopWatch = new Stopwatch();
            globalStopWatch.Start();

            string ldapFilter = this.BuildFilter(currentContext.CurrentClaimTypeConfigList, currentContext.Input, currentContext.ExactSearch, null);
            //foreach (var ldapConnection in currentContext.LdapConnections.Where(x => x.LdapEntry != null))
            Parallel.ForEach(currentContext.LdapConnections.Where(x => x.LdapEntry != null), ldapConnection =>
            {
                if (!String.IsNullOrWhiteSpace(ldapConnection.CustomFilter))
                {
                    // The LDAP filter needs to be entirely rewritten to include the filter specified in current connection
                    ldapFilter = this.BuildFilter(currentContext.CurrentClaimTypeConfigList, currentContext.Input, currentContext.ExactSearch, ldapConnection);
                }
                Debug.WriteLine($"ldapConnection: Path: {ldapConnection.LdapEntry.Path}, UseDefaultADConnection: {ldapConnection.UseDefaultADConnection}");
                Logger.LogDebug($"ldapConnection: Path: {ldapConnection.LdapEntry.Path}, UseDefaultADConnection: {ldapConnection.UseDefaultADConnection}");
                using (DirectoryEntry directory = ldapConnection.LdapEntry)
                {
                    using (DirectorySearcher ds = new DirectorySearcher(ldapFilter))
                    {
                        ds.SearchRoot = directory;
                        ds.SizeLimit = currentContext.MaxCount; // Property SizeLimit is ignored if it is set to 0 (tested), and ArgumentException an exception is < 0 - https://learn.microsoft.com/en-us/dotnet/api/system.directoryservices.directorysearcher.sizelimit?view=netframework-4.8.1
                        ds.ClientTimeout = new TimeSpan(0, 0, this.Settings.Timeout); // Set the timeout in seconds
                        ds.PropertiesToLoad.Add("objectclass");
                        ds.PropertiesToLoad.Add("nETBIOSName");
                        foreach (var ldapAttribute in currentContext.CurrentClaimTypeConfigList.Where(x => !String.IsNullOrWhiteSpace(x.DirectoryObjectAttribute)))
                        {
                            ds.PropertiesToLoad.Add(ldapAttribute.DirectoryObjectAttribute);
                            if (!String.IsNullOrEmpty(ldapAttribute.DirectoryObjectAttributeForDisplayText))
                            {
                                ds.PropertiesToLoad.Add(ldapAttribute.DirectoryObjectAttributeForDisplayText);
                            }
                        }
                        // Populate additional attributes that are not part of the filter but are requested in the result
                        foreach (var metadataAttribute in this.Settings.RuntimeMetadataConfig)
                        {
                            if (!ds.PropertiesToLoad.Contains(metadataAttribute.DirectoryObjectAttribute))
                            {
                                ds.PropertiesToLoad.Add(metadataAttribute.DirectoryObjectAttribute);
                            }
                        }

                        string loggMessage = $"[{ClaimsProviderName}] Connecting to \"{ldapConnection.LdapEntry.Path}\" with AuthenticationType \"{ldapConnection.LdapEntry.AuthenticationType}\", authenticating ";
                        loggMessage += String.IsNullOrWhiteSpace(ldapConnection.LdapEntry.Username) ? "as process identity" : $"with credentials \"{ldapConnection.LdapEntry.Username}\"";
                        loggMessage += $" and sending a query with filter \"{ds.Filter}\"...";
                        using (new SPMonitoredScope(loggMessage, 3000)) // threshold of 3 seconds before it's considered too much. If exceeded it is recorded in a higher logging level
                        {
                            try
                            {
                                Logger.Log(loggMessage, TraceSeverity.Verbose, TraceCategory.Ldap_Request);
                                Stopwatch stopWatch = new Stopwatch();
                                stopWatch.Start();
                                using (SearchResultCollection directoryResults = ds.FindAll())
                                {
                                    stopWatch.Stop();
                                    Logger.Log($"[{ClaimsProviderName}] Got {directoryResults.Count} result(s) in {stopWatch.ElapsedMilliseconds} ms from \"{directory.Path}\" with input \"{currentContext.Input}\" and LDAP filter \"{ds.Filter}\"", TraceSeverity.Medium, TraceCategory.Ldap_Request);
                                    if (directoryResults.Count > 0)
                                    {
                                        lock (lockResults)
                                        {
                                            foreach (SearchResult item in directoryResults)
                                            {
                                                results.Add(new LdapEntityProviderResult(item.Properties, ldapConnection));
                                            }
                                        }
                                    }
                                }
                            }
                            catch (DirectoryServicesCOMException ex)
                            {
                                if (ldapConnection.UseDefaultADConnection)
                                {
                                    // A DirectoryServicesCOMException is frequently thrown here when:
                                    // - Use DirectoryEntry returned by Domain.GetComputerDomain().GetDirectoryEntry()
                                    // - In "check permissions" dialog
                                    // - During Validation (Search works fine)
                                    // And despite that, the validation definitely fails, "check permissions" still works normally
                                    // Anyway, record a custom message to recommend to use a custom LDAP connection instead
                                    Logger.Log($"[{ClaimsProviderName}] A DirectoryServicesCOMException occured while connecting using the default AD connection. It may be resolved by replacing it with a custom LDAP connection with explicit credentials. Error details: \"{ex.ExtendedErrorMessage}\"", TraceSeverity.Unexpected, TraceCategory.Ldap_Request);
                                }
                                else
                                {
                                    Logger.LogException(ClaimsProviderName, $"while connecting to \"{directory.Path}\" with LDAP filter \"{ds.Filter}\"", TraceCategory.Ldap_Request, ex);
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogException(ClaimsProviderName, $"while connecting to \"{directory.Path}\" with LDAP filter \"{ds.Filter}\"", TraceCategory.Ldap_Request, ex);
                            }
                        }
                    }
                }
            });
            globalStopWatch.Stop();
            Logger.Log(String.Format("[{0}] Got {1} result(s) in {2} ms from all directories with input \"{3}\" and LDAP filter \"{4}\"", ClaimsProviderName, results.Count, globalStopWatch.ElapsedMilliseconds, currentContext.Input, ldapFilter), TraceSeverity.Verbose, TraceCategory.Ldap_Request);
            return results;
        }
    }
}
