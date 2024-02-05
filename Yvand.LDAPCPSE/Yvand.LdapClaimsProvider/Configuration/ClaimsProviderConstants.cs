using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
using Microsoft.SharePoint.WebControls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.DirectoryServices;
using System.Linq;
using System.Reflection;
using System.Web;
using Yvand.LdapClaimsProvider.Configuration;
using WIF4_5 = System.Security.Claims;

namespace Yvand.LdapClaimsProvider.Configuration
{
    public static class ClaimsProviderConstants
    {
        public static string CONFIGURATION_ID => "F2D006C9-C536-46DA-845D-D5E88CBD15E6";
        public static string CONFIGURATION_NAME => "LDAPCPSEConfig";
        public static string GroupClaimEntityType { get; set; } = SPClaimEntityTypes.FormsRole;
        public static bool EnforceOnly1ClaimTypeForGroup => true;
        public static string DefaultMainGroupClaimType => WIF4_5.ClaimTypes.Role;
        public static string PUBLICSITEURL => "https://ldapcp.com";
        public static string LDAPCPCONFIG_TOKENDOMAINNAME => "{domain}";
        public static string LDAPCPCONFIG_TOKENDOMAINFQDN => "{fqdn}";
        private static object Lock_SetClaimsProviderVersion = new object();
        public static string RegexDomainFromFullAccountName => "(.*)\\\\.*";
        public static string RegexFullDomainFromEmail => ".*@(.*)";
        public static string LDAPFilter => "(&(objectclass={2})({0}={1}){3}) ";
        public static string LDAPFilterEnabledUsersOnly => "(&(!(userAccountControl:1.2.840.113556.1.4.803:=2))";
        public static string LDAPFilterADSecurityGroupsOnly => "(groupType:1.2.840.113556.1.4.803:=2147483648)";
        private static string _ClaimsProviderVersion;
        public static string ClaimsProviderVersion
        {
            get
            {
                if (!String.IsNullOrEmpty(_ClaimsProviderVersion))
                {
                    return _ClaimsProviderVersion;
                }

                // Method FileVersionInfo.GetVersionInfo() may hang and block all LDAPCP threads, so it is read only 1 time
                lock (Lock_SetClaimsProviderVersion)
                {
                    if (!String.IsNullOrEmpty(_ClaimsProviderVersion))
                    {
                        return _ClaimsProviderVersion;
                    }

                    try
                    {
                        _ClaimsProviderVersion = FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(LDAPCPSE)).Location).FileVersion;
                    }
                    // If assembly was removed from the GAC, CLR throws a FileNotFoundException
                    catch (System.IO.FileNotFoundException)
                    {
                        // Local process will never detect if assembly is added to the GAC later, which is fine
                        _ClaimsProviderVersion = " ";
                    }
                    return _ClaimsProviderVersion;
                }
            }
        }

#if DEBUG
        public static int DEFAULT_TIMEOUT => 10000;
#else
        public static int DEFAULT_TIMEOUT => 4000;    // 4 secs
#endif

        /// <summary>
        /// Escape characters to use for special characters in LDAP filters, as documented in https://ldap.com/ldap-filters/
        /// </summary>
        public static Dictionary<string, string> SpecialCharacters
        {
            get => _SpecialCharacters;
            //set => _SpecialCharacters = value;
        }
        private static readonly Dictionary<string, string> _SpecialCharacters = new Dictionary<string, string>
        {
            { @"\", @"\5C" },   // '\' must be the 1st to be evaluated because '\' is also present in the escape characters themselves
            { "*", @"\2A" },
            { "(", @"\28" },
            { ")", @"\29" },
        };

        public static readonly Dictionary<string, string> EntityMetadataPerLdapAttributes = new Dictionary<string, string>
        {
            { "mail", PeopleEditorEntityDataKeys.Email },
            { "title", PeopleEditorEntityDataKeys.JobTitle },
            { "displayName", PeopleEditorEntityDataKeys.DisplayName },
            { "sAMAccountName", PeopleEditorEntityDataKeys.AccountName },
            { "department", PeopleEditorEntityDataKeys.Department },
            { "physicalDeliveryOfficeName", PeopleEditorEntityDataKeys.Location },
            { "mobile", PeopleEditorEntityDataKeys.MobilePhone },
        };

        public static List<KeyValuePair<string, ClaimTypeConfig>> GetDefaultSettingsPerUserClaimType()
        {
            // This Dictionary cannot be exposed directly as a static member of this class, as it would risk one of its members to be modified, and that modif would span on the whole process
            Dictionary<string, ClaimTypeConfig> defaultSettingsPerUserClaimType = new Dictionary<string, ClaimTypeConfig>
            {
                {
                    WIF4_5.ClaimTypes.Upn,
                    new ClaimTypeConfig
                    {
                        DirectoryObjectType = DirectoryObjectType.User,
                        DirectoryObjectClass = "user",
                        DirectoryObjectAttribute = "userPrincipalName",
                        DirectoryObjectAdditionalFilter = "(!(objectClass=computer))"
                    }
                },
                {
                    WIF4_5.ClaimTypes.Email,
                    new ClaimTypeConfig
                    {
                        DirectoryObjectType = DirectoryObjectType.User,
                        DirectoryObjectClass = "user",
                        DirectoryObjectAttribute = "mail",
                        DirectoryObjectAdditionalFilter = "(!(objectClass=computer))",
                        SPEntityDataKey = EntityMetadataPerLdapAttributes.ContainsKey("mail") ? EntityMetadataPerLdapAttributes["mail"] : String.Empty,
                    }
                },
                {
                    WIF4_5.ClaimTypes.WindowsAccountName,
                    new ClaimTypeConfig
                    {
                        DirectoryObjectType = DirectoryObjectType.User,
                        DirectoryObjectClass = "user",
                        DirectoryObjectAttribute = "sAMAccountName",
                        DirectoryObjectAdditionalFilter = "(!(objectClass=computer))",
                        SPEntityDataKey = EntityMetadataPerLdapAttributes.ContainsKey("sAMAccountName") ? EntityMetadataPerLdapAttributes["sAMAccountName"] : String.Empty,
                    }
                }
                //{
                //    WIF4_5.ClaimTypes.PrimarySid,
                //    new ClaimTypeConfig
                //    {
                //        DirectoryObjectType = DirectoryObjectType.User,
                //        DirectoryObjectClass = "user",
                //        DirectoryObjectAttribute = "objectsid",
                //        DirectoryObjectAttributeSupportsWildcard = false,
                //        SPEntityDataKey = EntityMetadataPerLdapAttributes.ContainsKey("objectsid") ? EntityMetadataPerLdapAttributes["objectsid"] : String.Empty,
                //    }
                //},
            };
            // Returns a copy of the Dictionary, not the Dictionary itself, to prevent its members to be modified
            return defaultSettingsPerUserClaimType.ToList();
        }

        public static ClaimTypeConfig GetDefaultSettingsPerUserClaimType(string claimType)
        {
            foreach (KeyValuePair<string, ClaimTypeConfig> kvp in GetDefaultSettingsPerUserClaimType())
            {
                if (String.Equals(kvp.Key, claimType, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }
            return null;
        }
    }

    public enum DirectoryObjectType
    {
        User,
        Group
    }

    public enum OperationType
    {
        Search,
        Validation,
        Augmentation,
    }

    public class LdapEntityProviderResult
    {
        /// <summary>
        /// Single LDAP result from the directory, with all its properties
        /// </summary>
        public ResultPropertyCollection DirectoryResultProperties { get; private set; }

        /// <summary>
        /// The directory which returned the LDAP result
        /// </summary>
        public DirectoryConnection AuthorityMatch { get; private set; }

        public LdapEntityProviderResult(ResultPropertyCollection directoryResultProperties, DirectoryConnection authorityMatch)
        {
            this.DirectoryResultProperties = directoryResultProperties;
            this.AuthorityMatch = authorityMatch;
        }
    }

    public class ClaimsProviderEntity
    {
        public LdapEntityProviderResult DirectoryResult { get; private set; }

        /// <summary>
        /// The configuration which matched the LDAP result
        /// </summary>
        public ClaimTypeConfig ClaimTypeConfigMatch { get; private set; }

        /// <summary>
        /// The LDAP attribute value which matched the input
        /// </summary>
        public string DirectoryAttributeValueMatch { get; private set; }

        /// <summary>
        /// Actual claim value set in the Picker Entity returned to SharePoint
        /// </summary>
        public string PermissionClaimValue { get; private set; }

        public ClaimsProviderEntity(LdapEntityProviderResult directoryResult, ClaimTypeConfig claimTypeConfigMatch, string directoryAttributeValueMatch, string permissionClaimValue)
        {
            this.DirectoryResult = directoryResult;
            this.ClaimTypeConfigMatch = claimTypeConfigMatch;
            this.DirectoryAttributeValueMatch = directoryAttributeValueMatch;
            this.PermissionClaimValue = permissionClaimValue;
        }

        public ClaimsProviderEntity(ClaimTypeConfig claimTypeConfigMatch, string permissionClaimValue)
        {
            this.ClaimTypeConfigMatch = claimTypeConfigMatch;
            this.PermissionClaimValue = permissionClaimValue;
        }
    }

    /// <summary>
    /// This collection ensures it contains only unique results, so no duplicate is returned to SharePoint
    /// </summary>
    public class ClaimsProviderEntityCollection : Collection<ClaimsProviderEntity>
    {
        /// <summary>
        /// Compare 2 results to not add duplicates
        /// they are identical if they have the same claim type and same value
        /// </summary>
        /// <param name="result">LDAP result to compare</param>
        /// <param name="ctConfig">AttributeHelper that matches result</param>
        /// <param name="dynamicDomainTokenSet">if true, don't consider 2 results as identical if they don't are in same domain.</param>
        /// <returns></returns>
        public bool Contains(LdapEntityProviderResult result, ClaimTypeConfig ctConfig, bool dynamicDomainTokenSet)
        {
            foreach (var item in base.Items)
            {
                if (item.ClaimTypeConfigMatch.ClaimType != ctConfig.ClaimType) { continue; }

                if (!item.DirectoryResult.DirectoryResultProperties.Contains(ctConfig.DirectoryObjectAttribute)) { continue; }

                // if dynamicDomainTokenSet is true, don't consider 2 results as identical if they don't are in same domain
                // Using same bool to compare both DomainName and DomainFQDN causes scenario below to potentially generate duplicates:
                // result.DomainName == item.DomainName BUT result.DomainFQDN != item.DomainFQDN AND value of claim is created with DomainName token
                // If so, dynamicDomainTokenSet will be true and test below will be true so duplicates won't be check, even though it would be possible. 
                // But this would be so unlikely that this scenario can be ignored
                if (dynamicDomainTokenSet && (
                    !String.Equals(item.DirectoryResult.AuthorityMatch.DomainName, result.AuthorityMatch.DomainName, StringComparison.InvariantCultureIgnoreCase) ||
                    !String.Equals(item.DirectoryResult.AuthorityMatch.DomainFQDN, result.AuthorityMatch.DomainFQDN, StringComparison.InvariantCultureIgnoreCase)
                                         ))
                {
                    continue;   // They are not in the same domain, so not identical, jump to next item
                }

                string itemDirectoryObjectPropertyValue = Utils.GetLdapValueAsString(item.DirectoryResult.DirectoryResultProperties[ctConfig.DirectoryObjectAttribute][0], ctConfig.DirectoryObjectAttribute);
                string resultDirectoryObjectPropertyValue = Utils.GetLdapValueAsString(result.DirectoryResultProperties[ctConfig.DirectoryObjectAttribute][0], ctConfig.DirectoryObjectAttribute);
                if (String.Equals(itemDirectoryObjectPropertyValue, resultDirectoryObjectPropertyValue, StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Contains information about current operation
    /// </summary>
    public class OperationContext
    {
        public OperationType OperationType { get; private set; }

        /// <summary>
        /// Set only if request is a validation or an augmentation, to the incoming entity provided by SharePoint
        /// </summary>
        public SPClaim IncomingEntity { get; private set; }

        /// <summary>
        /// User submitting the query in the poeple picker, retrieved from HttpContext. Can be null
        /// </summary>
        public SPClaim UserInHttpContext { get; private set; }

        /// <summary>
        /// Uri provided by SharePoint
        /// </summary>
        public Uri UriContext { get; private set; }

        /// <summary>
        /// EntityTypes expected by SharePoint in the entities returned
        /// </summary>
        public DirectoryObjectType[] DirectoryObjectTypes { get; private set; }

        public string HierarchyNodeID { get; private set; }
        public int MaxCount { get; }

        /// <summary>
        /// If request is a validation: contains the value of the SPClaim. If request is a search: contains the input
        /// </summary>
        public string Input { get; private set; }

        /// <summary>
        /// Indicates if search operation should return only results that exactly match the Input
        /// </summary>
        public bool ExactSearch { get; private set; }

        /// <summary>
        /// Contains the relevant list of ClaimTypeConfig for every type of request. In case of validation or augmentation, it will contain only 1 item.
        /// </summary>
        public List<ClaimTypeConfig> CurrentClaimTypeConfigList { get; private set; }

        public List<DirectoryConnection> LdapConnections { get; private set; }

        public OperationContext(ILDAPCPSettings settings, OperationType currentRequestType, string input, SPClaim incomingEntity, Uri context, string[] entityTypes, string hierarchyNodeID, int maxCount)
        {
            this.OperationType = currentRequestType;
            this.Input = input;
            this.IncomingEntity = incomingEntity;
            this.UriContext = context;
            this.HierarchyNodeID = hierarchyNodeID;
            this.MaxCount = maxCount;

            // settings.LdapConnections must be cloned locally to ensure its properties ($select / $filter) won't be updated by multiple threads
            this.LdapConnections = new List<DirectoryConnection>(settings.LdapConnections.Count);
            foreach (DirectoryConnection tenant in settings.LdapConnections)
            {
                DirectoryConnection copy = new DirectoryConnection();
                Utils.CopyPublicProperties(typeof(DirectoryConnection), tenant, copy);
                LdapConnections.Add(copy);
            }

            if (entityTypes != null)
            {
                List<DirectoryObjectType> aadEntityTypes = new List<DirectoryObjectType>();
                if (entityTypes.Contains(SPClaimEntityTypes.User))
                {
                    aadEntityTypes.Add(DirectoryObjectType.User);
                }
                if (entityTypes.Contains(ClaimsProviderConstants.GroupClaimEntityType))
                {
                    aadEntityTypes.Add(DirectoryObjectType.Group);
                }
                this.DirectoryObjectTypes = aadEntityTypes.ToArray();
            }

            HttpContext httpctx = HttpContext.Current;
            if (httpctx != null)
            {
                WIF4_5.ClaimsPrincipal cp = httpctx.User as WIF4_5.ClaimsPrincipal;
                if (cp != null)
                {
                    if (SPClaimProviderManager.IsEncodedClaim(cp.Identity.Name))
                    {
                        this.UserInHttpContext = SPClaimProviderManager.Local.DecodeClaimFromFormsSuffix(cp.Identity.Name);
                    }
                    else
                    {
                        // This code is reached only when called from central administration: current user is always a Windows user
                        this.UserInHttpContext = SPClaimProviderManager.Local.ConvertIdentifierToClaim(cp.Identity.Name, SPIdentifierTypes.WindowsSamAccountName);
                    }
                }
            }

            if (currentRequestType == OperationType.Validation)
            {
                this.InitializeValidation(settings.RuntimeClaimTypesList);
            }
            else if (currentRequestType == OperationType.Search)
            {
                this.InitializeSearch(settings.RuntimeClaimTypesList, settings.FilterExactMatchOnly);
            }
            else if (currentRequestType == OperationType.Augmentation)
            {
                this.InitializeAugmentation(settings.RuntimeClaimTypesList);
            }
        }

        /// <summary>
        /// Validation is when SharePoint expects exactly 1 PickerEntity from the incoming SPClaim
        /// </summary>
        /// <param name="runtimeClaimTypesList"></param>
        private void InitializeValidation(List<ClaimTypeConfig> runtimeClaimTypesList)
        {
            if (this.IncomingEntity == null) { throw new ArgumentNullException(nameof(this.IncomingEntity)); }
            
            // FirstOrDefault returns null if no result, while First throws an exception
            ClaimTypeConfig incomingEntityClaimTypeConfig = runtimeClaimTypesList.FirstOrDefault(x =>
               String.Equals(x.ClaimType, this.IncomingEntity.ClaimType, StringComparison.InvariantCultureIgnoreCase) &&
               !x.IsAdditionalLdapSearchAttribute);
            if (incomingEntityClaimTypeConfig == null)
            {
                Logger.Log($"[{LDAPCPSE.ClaimsProviderName}] Unable to validate entity \"{this.IncomingEntity.Value}\" because its claim type \"{this.IncomingEntity.ClaimType}\" was not found in the ClaimTypes list of current configuration.", TraceSeverity.Unexpected, EventSeverity.Error, TraceCategory.Configuration);
                throw new InvalidOperationException($"[{LDAPCPSE.ClaimsProviderName}] Unable validate entity \"{this.IncomingEntity.Value}\" because its claim type \"{this.IncomingEntity.ClaimType}\" was not found in the ClaimTypes list of current configuration.");
            }
            this.CurrentClaimTypeConfigList = new List<ClaimTypeConfig>(1)
            {
                incomingEntityClaimTypeConfig,
            };

            this.ExactSearch = true;
            this.Input = this.IncomingEntity.Value;

            if (!String.IsNullOrEmpty(incomingEntityClaimTypeConfig.ClaimValueLeadingToken))
            {
                if (this.IncomingEntity.Value.StartsWith(incomingEntityClaimTypeConfig.ClaimValueLeadingToken, StringComparison.InvariantCultureIgnoreCase))
                {
                    this.Input = this.IncomingEntity.Value.Substring(incomingEntityClaimTypeConfig.ClaimValueLeadingToken.Length);
                }

                if (incomingEntityClaimTypeConfig.ClaimValueLeadingToken.Contains(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME) ||
                    incomingEntityClaimTypeConfig.ClaimValueLeadingToken.Contains(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN))
                {
                    this.Input = Utils.GetAccountFromFullAccountName(this.Input);
                }
            }
        }

        /// <summary>
        /// Search is when SharePoint expects a list of any PickerEntity that match input provided
        /// </summary>
        /// <param name="runtimeClaimTypesList"></param>
        private void InitializeSearch(List<ClaimTypeConfig> runtimeClaimTypesList, bool exactSearch)
        {
            this.ExactSearch = exactSearch;
            if (!String.IsNullOrEmpty(this.HierarchyNodeID))
            {
                // Restrict search to ClaimType currently selected in the hierarchy (may return multiple results if identity claim type)
                CurrentClaimTypeConfigList = runtimeClaimTypesList.FindAll(x =>
                    String.Equals(x.ClaimType, this.HierarchyNodeID, StringComparison.InvariantCultureIgnoreCase) &&
                    this.DirectoryObjectTypes.Contains(x.DirectoryObjectType));
            }
            else
            {
                // List<T>.FindAll returns an empty list if no result found: http://msdn.microsoft.com/en-us/library/fh1w7y8z(v=vs.110).aspx
                CurrentClaimTypeConfigList = runtimeClaimTypesList.FindAll(x => this.DirectoryObjectTypes.Contains(x.DirectoryObjectType));
            }
        }

        private void InitializeAugmentation(List<ClaimTypeConfig> runtimeClaimTypesList)
        {
        }
    }
}
