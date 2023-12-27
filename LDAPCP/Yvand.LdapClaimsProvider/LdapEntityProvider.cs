using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
using Microsoft.SharePoint.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.Protocols;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;
using Yvand.LdapClaimsProvider.Configuration;
using WIF4_5 = System.Security.Claims;

namespace Yvand.LdapClaimsProvider
{
    internal class LdapEntityProvider : EntityProviderBase
    {
        public LdapEntityProvider(string claimsProviderName) : base(claimsProviderName) { }

        public override List<SPClaim> GetEntityGroups(OperationContext currentContext, ClaimTypeConfig groupClaimTypeConfig)
        {
            Stopwatch timer = new Stopwatch();
            timer.Start();
            List<SPClaim> groups = new List<SPClaim>();
            object lockResults = new object();

            Parallel.ForEach(currentContext.LdapConnections, ldapConnection =>
            {
                List<SPClaim> directoryGroups;
                if (ldapConnection.GetGroupMembershipUsingDotNetHelpers)
                {
                    directoryGroups = GetGroupsFromActiveDirectory(ldapConnection, currentContext, groupClaimTypeConfig);
                    //directoryGroups.AddRange(GetGroupsFromLDAPDirectory(ldapConnection, currentContext, allGroupsCTConfig.Where(x => !SPClaimTypes.Equals(x.ClaimType, this.CurrentConfiguration.MainGroupClaimType))));
                }
                else
                {
                    //directoryGroups = GetGroupsFromLDAPDirectory(ldapConnection, currentContext, allGroupsCTConfig);
                }

                lock (lockResults)
                {
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
        /// <param name="groupCTConfig"></param>
        /// <returns></returns>
        protected virtual List<SPClaim> GetGroupsFromActiveDirectory(Configuration.LdapConnection ldapConnection, OperationContext currentContext, ClaimTypeConfig groupCTConfig)
        {
            // Convert AuthenticationTypes to ContextOptions, slightly inspired by https://stackoverflow.com/questions/17451277/what-equivalent-of-authenticationtypes-secure-in-principalcontexts-contextoptio
            // AuthenticationTypes Enum: https://learn.microsoft.com/en-us/dotnet/api/system.directoryservices.authenticationtypes?view=netframework-4.8.1
            // ContextOptions Enum: https://learn.microsoft.com/en-us/dotnet/api/system.directoryservices.accountmanagement.contextoptions?view=netframework-4.8.1
            ContextOptions contextOptions = new ContextOptions();
            if (ldapConnection.AuthenticationSettings == AuthenticationTypes.None)
            {
                contextOptions |= ContextOptions.SimpleBind;
            }
            else
            {
                if ((ldapConnection.AuthenticationSettings & AuthenticationTypes.Sealing) == AuthenticationTypes.Sealing) { contextOptions |= ContextOptions.Sealing; }
                if (
                    (ldapConnection.AuthenticationSettings & AuthenticationTypes.Encryption) == AuthenticationTypes.Encryption ||
                    (ldapConnection.AuthenticationSettings & AuthenticationTypes.SecureSocketsLayer) == AuthenticationTypes.SecureSocketsLayer
                ) { contextOptions |= ContextOptions.SecureSocketLayer; }
                if ((ldapConnection.AuthenticationSettings & AuthenticationTypes.ServerBind) == AuthenticationTypes.ServerBind) { contextOptions |= ContextOptions.ServerBind; }
                if ((ldapConnection.AuthenticationSettings & AuthenticationTypes.Signing) == AuthenticationTypes.Signing) { contextOptions |= ContextOptions.Signing; }
                if ((ldapConnection.AuthenticationSettings & AuthenticationTypes.Secure) == AuthenticationTypes.Secure) { contextOptions |= ContextOptions.Negotiate; }
            }

            List<SPClaim> groups = new List<SPClaim>();
            string logMessageCredentials = ldapConnection.UseDefaultADConnection ? "process identity" : ldapConnection.Username;
            string directoryDetails = $"from AD domain \"{ldapConnection.DomainFQDN}\" (authenticate as \"{logMessageCredentials}\" with AuthenticationType \"{contextOptions}\").";
            Logger.Log($"[{ClaimsProviderName}] Getting AD groups of user \"{currentContext.IncomingEntity.Value}\" {directoryDetails}", TraceSeverity.Verbose, EventSeverity.Information, TraceCategory.Augmentation);
            using (new SPMonitoredScope($"[{ClaimsProviderName}] Get AD groups of user \"{currentContext.IncomingEntity.Value}\" {directoryDetails}", 2000))
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();
                UserPrincipal adUser = null;
                PrincipalContext principalContext = null;
                try
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
                        return groups;
                    }

                    IEnumerable<Principal> adGroups;
                    using (new SPMonitoredScope($"[{ClaimsProviderName}] Get group membership of \"{currentContext.IncomingEntity.Value}\" " + directoryDetails, 1000))
                    {
                        adGroups = adUser.GetAuthorizationGroups();
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

                            // https://github.com/Yvand/LDAPCP/issues/148 - the group property used for the group value should be based on the LDAPCP configuration
                            // By default it should be the SamAccountName, since it's also the default attribute set in LDAPCP configuration
                            string claimValue = adGroup.SamAccountName;
                            switch (groupCTConfig.LDAPAttribute.ToLower())
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
                            }

                            if (!String.IsNullOrEmpty(groupCTConfig.ClaimValuePrefix))
                            {
                                // Principal.DistinguishedName is used to build the domain name / FQDN of the current group. Example of value: CN=group1,CN=Users,DC=contoso,DC=local
                                string groupDN = adGroup.DistinguishedName;
                                if (String.IsNullOrEmpty(groupDN)) { continue; }

                                Utils.GetDomainInformation(groupDN, out groupDomainName, out groupDomainFqdn);
                                if (groupCTConfig.ClaimValuePrefix.Contains(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME))
                                {
                                    claimValue = groupCTConfig.ClaimValuePrefix.Replace(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME, groupDomainName) + claimValue;
                                }
                                else if (groupCTConfig.ClaimValuePrefix.Contains(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN))
                                {
                                    claimValue = groupCTConfig.ClaimValuePrefix.Replace(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN, groupDomainFqdn) + claimValue;
                                }
                            }

                            SPClaim claim = CreateClaim(groupCTConfig.ClaimType, claimValue, groupCTConfig.ClaimValueType, false);
                            groups.Add(claim);
                        }
                    }
                }
                catch (PrincipalOperationException ex)
                {
                    Logger.LogException(ClaimsProviderName, $"while getting AD groups of user \"{currentContext.IncomingEntity.Value}\" using UserPrincipal.GetAuthorizationGroups() {directoryDetails} This is likely due to a bug in .NET framework in UserPrincipal.GetAuthorizationGroups (as of v4.6.1), especially if user is member (directly or not) of a group either in a child domain that was migrated, or a group that has special (deny) entities.", TraceCategory.Augmentation, ex);
                    // In this case, fallback to LDAP method to get group membership.
                    //return GetGroupsFromLDAPDirectory(ldapConnection, currentContext, new List<ClaimTypeConfig>(1) { groupCTConfig });
                }
                catch (PrincipalServerDownException ex)
                {
                    Logger.LogException(ClaimsProviderName, $"while getting AD groups of user \"{currentContext.IncomingEntity.Value}\" using UserPrincipal.GetAuthorizationGroups() {directoryDetails} Is this server an Active Directory server?", TraceCategory.Augmentation, ex);
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
                        TraceSeverity.Medium, EventSeverity.Information, TraceCategory.Augmentation);
                }
            }
            return groups;
        }

        public override List<SearchResultCollection> SearchOrValidateEntities(OperationContext currentContext)
        {
            throw new NotImplementedException();
        }
    }
}
