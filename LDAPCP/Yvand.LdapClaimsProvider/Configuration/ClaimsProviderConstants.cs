using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Web;
using WIF4_5 = System.Security.Claims;

namespace Yvand.LdapClaimsProvider.Configuration
{
    public static class ClaimsProviderConstants
    {
        public static string CONFIGURATION_ID => "F2D006C9-C536-46DA-845D-D5E88CBD15E6";
        public static string CONFIGURATION_NAME => "LDAPCPSEConfig";
        public static string GroupClaimEntityType { get; set; } = SPClaimEntityTypes.FormsRole;
        public static bool EnforceOnly1ClaimTypeForGroup => false;    // In LDAPCP, multiple claim types can be used to create group permissions
        public static string DefaultMainGroupClaimType => WIF4_5.ClaimTypes.Role;
        public static string PUBLICSITEURL => "https://ldapcp.com";
        public static string LDAPCPCONFIG_TOKENDOMAINNAME => "{domain}";
        public static string LDAPCPCONFIG_TOKENDOMAINFQDN => "{fqdn}";
        private static object Lock_SetClaimsProviderVersion = new object();
        public static string RegexDomainFromFullAccountName => "(.*)\\\\.*";
        public static string RegexFullDomainFromEmail => ".*@(.*)";
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
                        // Current process will never detect if assembly is added to the GAC later, which is fine
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
            set => _SpecialCharacters = value;
        }
        private static Dictionary<string, string> _SpecialCharacters = new Dictionary<string, string>
        {
            { @"\", @"\5C" },   // '\' must be the 1st to be evaluated because '\' is also present in the escape characters themselves
            { "*", @"\2A" },
            { "(", @"\28" },
            { ")", @"\29" },
        };
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

    /// <summary>
    /// Contains information about current operation
    /// </summary>
    public class OperationContext
    {
        public ILDAPCPSettings Settings { get; private set; }
        /// <summary>
        /// Indicates what kind of operation SharePoint is requesting
        /// </summary>
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

        public bool InputHasKeyword { get; private set; }

        /// <summary>
        /// Indicates if search operation should return only results that exactly match the Input
        /// </summary>
        public bool ExactSearch { get; private set; }

        /// <summary>
        /// Set only if request is a validation or an augmentation, to the ClaimTypeConfig that matches the ClaimType of the incoming entity
        /// </summary>
        public ClaimTypeConfig IncomingEntityClaimTypeConfig { get; private set; }

        /// <summary>
        /// Contains the relevant list of ClaimTypeConfig for every type of request. In case of validation or augmentation, it will contain only 1 item.
        /// </summary>
        public List<ClaimTypeConfig> CurrentClaimTypeConfigList { get; private set; }

        public List<LdapConnection> LdapConnections { get; private set; }

        public OperationContext(ILDAPCPSettings settings, OperationType currentRequestType, string input, SPClaim incomingEntity, Uri context, string[] entityTypes, string hierarchyNodeID, int maxCount)
        {
            this.Settings = settings;
            this.OperationType = currentRequestType;
            this.Input = input;
            this.IncomingEntity = incomingEntity;
            this.UriContext = context;
            this.HierarchyNodeID = hierarchyNodeID;
            this.MaxCount = maxCount;

            // settings.LdapConnections must be cloned locally to ensure its properties ($select / $filter) won't be updated by multiple threads
            this.LdapConnections = new List<LdapConnection>(settings.LdapConnections.Count);
            foreach (LdapConnection tenant in settings.LdapConnections)
            {
                LdapConnection copy = new LdapConnection();
                Utils.CopyPublicProperties(typeof(LdapConnection), tenant, copy);
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
        protected void InitializeValidation(List<ClaimTypeConfig> runtimeClaimTypesList)
        {
            if (this.IncomingEntity == null) { throw new ArgumentNullException(nameof(this.IncomingEntity)); }
            this.IncomingEntityClaimTypeConfig = runtimeClaimTypesList.FirstOrDefault(x =>
               String.Equals(x.ClaimType, this.IncomingEntity.ClaimType, StringComparison.InvariantCultureIgnoreCase) &&
               !x.UseMainClaimTypeOfDirectoryObject);

            if (this.IncomingEntityClaimTypeConfig == null)
            {
                Logger.Log($"[{LDAPCPSE.ClaimsProviderName}] Unable to validate entity \"{this.IncomingEntity.Value}\" because its claim type \"{this.IncomingEntity.ClaimType}\" was not found in the ClaimTypes list of current configuration.", TraceSeverity.Unexpected, EventSeverity.Error, TraceCategory.Configuration);
                throw new InvalidOperationException($"[{LDAPCPSE.ClaimsProviderName}] Unable validate entity \"{this.IncomingEntity.Value}\" because its claim type \"{this.IncomingEntity.ClaimType}\" was not found in the ClaimTypes list of current configuration.");
            }

            this.CurrentClaimTypeConfigList = new List<ClaimTypeConfig>(1)
            {
                this.IncomingEntityClaimTypeConfig
            };
            this.ExactSearch = true;
            this.Input = this.IncomingEntity.Value;
            this.InputHasKeyword = false;
            if (!String.IsNullOrEmpty(IncomingEntityClaimTypeConfig.ClaimValuePrefix))
            {
                if (this.IncomingEntity.Value.StartsWith(IncomingEntityClaimTypeConfig.ClaimValuePrefix, StringComparison.InvariantCultureIgnoreCase))
                {
                    this.Input = this.IncomingEntity.Value.Substring(IncomingEntityClaimTypeConfig.ClaimValuePrefix.Length);
                }
                else
                {
                    this.InputHasKeyword = IncomingEntityClaimTypeConfig.DoNotAddClaimValuePrefixIfBypassLookup;
                }

                if (IncomingEntityClaimTypeConfig.ClaimValuePrefix.Contains(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME) ||
                    IncomingEntityClaimTypeConfig.ClaimValuePrefix.Contains(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN))
                {
                    this.Input = Utils.GetAccountFromFullAccountName(this.Input);
                }
            }
        }

        /// <summary>
        /// Search is when SharePoint expects a list of any PickerEntity that match input provided
        /// </summary>
        /// <param name="runtimeClaimTypesList"></param>
        protected void InitializeSearch(List<ClaimTypeConfig> runtimeClaimTypesList, bool exactSearch)
        {
            this.ExactSearch = exactSearch;
            if (!String.IsNullOrEmpty(this.HierarchyNodeID))
            {
                // Restrict search to ClaimType currently selected in the hierarchy (may return multiple results if identity claim type)
                CurrentClaimTypeConfigList = runtimeClaimTypesList.FindAll(x =>
                    String.Equals(x.ClaimType, this.HierarchyNodeID, StringComparison.InvariantCultureIgnoreCase) &&
                    this.DirectoryObjectTypes.Contains(x.EntityType));
            }
            else
            {
                // List<T>.FindAll returns an empty list if no result found: http://msdn.microsoft.com/en-us/library/fh1w7y8z(v=vs.110).aspx
                CurrentClaimTypeConfigList = runtimeClaimTypesList.FindAll(x => this.DirectoryObjectTypes.Contains(x.EntityType));
            }
        }

        protected void InitializeAugmentation(List<ClaimTypeConfig> runtimeClaimTypesList)
        {
            if (this.IncomingEntity == null) { throw new ArgumentNullException(nameof(this.IncomingEntity)); }
            this.IncomingEntityClaimTypeConfig = runtimeClaimTypesList.FirstOrDefault(x =>
               String.Equals(x.ClaimType, this.IncomingEntity.ClaimType, StringComparison.InvariantCultureIgnoreCase) &&
               !x.UseMainClaimTypeOfDirectoryObject);

            if (this.IncomingEntityClaimTypeConfig == null)
            {
                Logger.Log($"[{LDAPCPSE.ClaimsProviderName}] Unable to augment entity \"{this.IncomingEntity.Value}\" because its claim type \"{this.IncomingEntity.ClaimType}\" was not found in the ClaimTypes list of current configuration.", TraceSeverity.Unexpected, EventSeverity.Error, TraceCategory.Configuration);
                throw new InvalidOperationException($"[{LDAPCPSE.ClaimsProviderName}] Unable to augment entity \"{this.IncomingEntity.Value}\" because its claim type \"{this.IncomingEntity.ClaimType}\" was not found in the ClaimTypes list of current configuration.");
            }
        }
    }
}
