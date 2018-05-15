using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
using Microsoft.SharePoint.Utilities;
using Microsoft.SharePoint.WebControls;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static ldapcp.ClaimsProviderLogging;
using WIF4_5 = System.Security.Claims;

/*
 * DO NOT directly edit LDAPCP class. It is designed to be inherited to customize it as desired.
 * Please download "LDAPCP for Developers.zip" on https://github.com/Yvand/LDAPCP to find examples and guidance.
 * */

namespace ldapcp
{
    /// <summary>
    /// Query Active Directory and LDAP servers to enhance people picker with a great search experience in federated authentication
    /// Please visit https://github.com/Yvand/LDAPCP for updates and to report bugs.
    /// Author: Yvan Duhamel - yvandev@outlook.fr
    /// </summary>
    public class LDAPCP : SPClaimProvider
    {
        public const string _ProviderInternalName = "LDAPCP";
        public virtual string ProviderInternalName => "LDAPCP";

        public virtual string PersistedObjectName => ClaimsProviderConstants.LDAPCPCONFIG_NAME;

        /// <summary>
        /// Contains configuration currently in use by claims provider
        /// </summary>
        public ILDAPCPConfiguration CurrentConfiguration;

        private object Sync_Init = new object();
        private ReaderWriterLockSlim Lock_Config = new ReaderWriterLockSlim();
        private long LdapcpConfigVersion = 0;

        /// <summary>
        /// Contains the attribute mapped to the identity claim in the SPTrustedLoginProvider
        /// </summary>
        protected ClaimTypeConfig IdentityClaimTypeConfig;

        /// <summary>
        /// Group ClaimTypeConfig used to set the claim type for other group ClaimTypeConfig that have UseMainClaimTypeOfDirectoryObject set to true
        /// </summary>
        ClaimTypeConfig MainGroupClaimTypeConfig;

        /// <summary>
        /// Contains attributes that are not used in the filter (both ClaimTypeProp AND UseMainClaimTypeOfDirectoryObject are not set), but have EntityDataKey set
        /// </summary>
        protected IEnumerable<ClaimTypeConfig> MetadataConfig;

        /// <summary>
        /// SPTrust associated with the claims provider
        /// </summary>
        protected SPTrustedLoginProvider SPTrust;

        /// <summary>
        /// List of attributes actually defined in the trust
        /// + list of LDAP attributes that are always queried, even if not defined in the trust (typically the displayName)
        /// </summary>
        private List<ClaimTypeConfig> ProcessedClaimTypesList;

        protected virtual string LDAPObjectClassName => "objectclass";
        protected virtual string LDAPFilter => "(&(" + LDAPObjectClassName + "={2})({0}={1}){3}) ";
        protected virtual string EntityDisplayText => "({0}) {1}";
        protected virtual string EntityOnMouseOver => "{0}={1}";
        protected virtual string LDAPFilterEnabledUsersOnly => "(&(!(userAccountControl:1.2.840.113556.1.4.803:=2))";
        protected virtual string LDAPFilterADSecurityGroupsOnly => "(groupType:1.2.840.113556.1.4.803:=2147483648)";
        protected string IssuerName => SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, SPTrust.Name);

        public LDAPCP(string displayName) : base(displayName) { }

        /// <summary>
        /// Initializes claim provider. This method is reserved for internal use and is not intended to be called from external code or changed
        /// </summary>
        protected bool Initialize(Uri context, string[] entityTypes)
        {
            // Ensures thread safety to initialize class variables
            lock (Sync_Init)
            {
                // 1ST PART: GET CONFIGURATION
                ILDAPCPConfiguration globalConfiguration = null;
                bool refreshConfig = false;
                bool success = true;
                try
                {
                    if (SPTrust == null)
                    {
                        SPTrust = GetSPTrustAssociatedWithCP(ProviderInternalName);
                        if (SPTrust == null) return false;
                    }
                    if (!CheckIfShouldProcessInput(context)) return false;
                    globalConfiguration = GetConfiguration(context, entityTypes, PersistedObjectName);
                    if (globalConfiguration == null)
                    {
                        ClaimsProviderLogging.Log($"[{ProviderInternalName}] PersistedObject '{PersistedObjectName}' was not found. Visit LDAPCP admin pages in central administration to create it.",
                            TraceSeverity.Verbose, EventSeverity.Information, TraceCategory.Core);
                        // Create a fake persisted object just to get the default settings, it will not be saved in config database
                        globalConfiguration = LDAPCPConfig.GetDefaultConfiguration();
                        refreshConfig = true;
                    }
                    else if (globalConfiguration.ClaimTypes == null || globalConfiguration.ClaimTypes.Count == 0)
                    {
                        ClaimsProviderLogging.Log($"[{ProviderInternalName}] PersistedObject '{PersistedObjectName}' was found but there are no Attribute set. Visit AzureCP admin pages in central administration to create it.",
                            TraceSeverity.Unexpected, EventSeverity.Error, TraceCategory.Core);
                        // Cannot continue 
                        success = false;
                    }
                    else if (globalConfiguration.LDAPConnectionsProp == null || globalConfiguration.LDAPConnectionsProp.Count == 0)
                    {
                        ClaimsProviderLogging.Log($"[{ProviderInternalName}] PersistedObject '{PersistedObjectName}' was found but there are no LDAP connection set. Visit AzureCP admin pages in central administration to create it.",
                            TraceSeverity.Unexpected, EventSeverity.Error, TraceCategory.Core);
                        // Cannot continue 
                        success = false;
                    }
                    else
                    {
                        // Persisted object is found and seems valid
                        ClaimsProviderLogging.LogDebug($"[{ProviderInternalName}] PersistedObject '{PersistedObjectName}' was found, version: {((SPPersistedObject)globalConfiguration).Version.ToString()}, previous version: {this.LdapcpConfigVersion.ToString()}");
                        if (this.LdapcpConfigVersion != ((SPPersistedObject)globalConfiguration).Version)
                        {
                            refreshConfig = true;
                            this.LdapcpConfigVersion = ((SPPersistedObject)globalConfiguration).Version;
                            ClaimsProviderLogging.Log($"[{ProviderInternalName}] PersistedObject '{PersistedObjectName}' was changed, refreshing configuration from new version {((SPPersistedObject)globalConfiguration).Version}",
                                TraceSeverity.Medium, EventSeverity.Information, TraceCategory.Core);
                        }
                    }
                    if (this.ProcessedClaimTypesList == null) refreshConfig = true;
                }
                catch (Exception ex)
                {
                    success = false;
                    ClaimsProviderLogging.LogException(ProviderInternalName, "in Initialize", TraceCategory.Core, ex);
                }
                finally
                {
                    // ProcessedAttributes can be null if:
                    // - 1st initialization
                    // - Initialized before but it failed. If so, try again to refresh config
                    if (this.ProcessedClaimTypesList == null) refreshConfig = true;

                    // Cannot continue if something went wrong to retrieve global configuration
                    if (globalConfiguration == null) success = false;
                }

                //refreshConfig = true;   // DEBUG
                if (!success) return success;
                if (!refreshConfig) return success;

                // 2ND PART: APPLY CONFIGURATION
                // Configuration needs to be refreshed, lock current thread in write mode
                Lock_Config.EnterWriteLock();
                try
                {
                    ClaimsProviderLogging.Log($"[{ProviderInternalName}] Refreshing configuration from PersistedObject '{PersistedObjectName}' version {((SPPersistedObject)globalConfiguration).Version.ToString()}...",
                        TraceSeverity.Verbose, EventSeverity.Information, TraceCategory.Core);

                    // Create local version of the persisted object, that will never be saved in config DB
                    // This copy is unique to current object instance to avoid thread safety issues
                    this.CurrentConfiguration = ((LDAPCPConfig)globalConfiguration).CopyCurrentObject();

                    SetCustomConfiguration(context, entityTypes);
                    if (this.CurrentConfiguration.ClaimTypes == null)
                    {
                        // this.CurrentConfiguration.ClaimTypes was set to null in SetCustomConfiguration, which is bad
                        ClaimsProviderLogging.Log(String.Format("[{0}] ClaimTypes was set to null in SetCustomConfiguration, don't set it or set it with actual entries.", ProviderInternalName), TraceSeverity.Unexpected, EventSeverity.Error, TraceCategory.Core);
                        return false;
                    }
                    success = InitializeClaimTypeConfigList(this.CurrentConfiguration.ClaimTypes);
                }
                catch (Exception ex)
                {
                    success = false;
                    ClaimsProviderLogging.LogException(ProviderInternalName, "in Initialize, while refreshing configuration", TraceCategory.Core, ex);
                }
                finally
                {
                    Lock_Config.ExitWriteLock();
                }
                return success;
            }
        }

        /// <summary>
        /// Initializes claim provider. This method is reserved for internal use and is not intended to be called from external code or changed
        /// </summary>
        private bool InitializeClaimTypeConfigList(ClaimTypeConfigCollection nonProcessedClaimTypes)
        {
            bool success = true;
            try
            {
                bool identityClaimTypeFound = false;
                bool groupClaimTypeFound = false;
                List<ClaimTypeConfig> claimTypesSetInTrust = new List<ClaimTypeConfig>();
                // Foreach MappedClaimType in the SPTrustedLoginProvider
                foreach (SPTrustedClaimTypeInformation claimTypeInformation in SPTrust.ClaimTypeInformation)
                {
                    // Search if current claim type in trust exists in ClaimTypeConfigCollection
                    ClaimTypeConfig claimTypeConfig = nonProcessedClaimTypes.FirstOrDefault(x =>
                        String.Equals(x.ClaimType, claimTypeInformation.MappedClaimType, StringComparison.InvariantCultureIgnoreCase) &&
                        !x.UseMainClaimTypeOfDirectoryObject &&
                        !String.IsNullOrEmpty(x.LDAPAttribute) &&
                        !String.IsNullOrEmpty(x.LDAPClass));

                    if (claimTypeConfig == null) continue;
                    claimTypeConfig.ClaimTypeDisplayName = claimTypeInformation.DisplayName;
                    claimTypesSetInTrust.Add(claimTypeConfig);
                    if (String.Equals(SPTrust.IdentityClaimTypeInformation.MappedClaimType, claimTypeConfig.ClaimType, StringComparison.InvariantCultureIgnoreCase))
                    {
                        // Identity claim type found, set IdentityClaimTypeConfig property
                        identityClaimTypeFound = true;
                        IdentityClaimTypeConfig = claimTypeConfig;
                    }
                    else if (!groupClaimTypeFound && claimTypeConfig.DirectoryObjectType == LDAPObjectType.Group)
                    {
                        // If ClaimTypeUsedForAugmentation is set, try to set MainGroupClaimTypeConfig with the ClaimTypeConfig that has the same ClaimType
                        // Otherwise, use arbitrarily the first valid group ClaimTypeConfig found
                        if (!String.IsNullOrEmpty(this.CurrentConfiguration.ClaimTypeUsedForAugmentation))
                        {
                            if (String.Equals(claimTypeConfig.ClaimType, this.CurrentConfiguration.ClaimTypeUsedForAugmentation, StringComparison.InvariantCultureIgnoreCase))
                            {
                                groupClaimTypeFound = true;
                                MainGroupClaimTypeConfig = claimTypeConfig;
                            }
                        }
                        else
                        {
                            groupClaimTypeFound = true;
                            MainGroupClaimTypeConfig = claimTypeConfig;
                            this.CurrentConfiguration.ClaimTypeUsedForAugmentation = claimTypeConfig.ClaimType;
                        }
                    }
                }

                if (!identityClaimTypeFound)
                {
                    ClaimsProviderLogging.Log($"[{ProviderInternalName}] Cannot continue because identity claim type '{SPTrust.IdentityClaimTypeInformation.MappedClaimType}' set in the SPTrustedIdentityTokenIssuer '{SPTrust.Name}' is missing in the ClaimTypeConfig list.", TraceSeverity.Unexpected, EventSeverity.ErrorCritical, TraceCategory.Core);
                    return false;
                }

                // Check if there are additional properties to use in queries (UseMainClaimTypeOfDirectoryObject set to true)
                List<ClaimTypeConfig> additionalClaimTypeConfigList = new List<ClaimTypeConfig>();
                foreach (ClaimTypeConfig claimTypeConfig in nonProcessedClaimTypes.Where(x => x.UseMainClaimTypeOfDirectoryObject))
                {
                    if (claimTypeConfig.DirectoryObjectType == LDAPObjectType.User)
                    {
                        claimTypeConfig.ClaimType = IdentityClaimTypeConfig.ClaimType;
                        claimTypeConfig.LDAPAttributeToShowAsDisplayText = IdentityClaimTypeConfig.LDAPAttributeToShowAsDisplayText;
                    }
                    else
                    {
                        // If not a user, it must be a group
                        if (MainGroupClaimTypeConfig == null) continue;
                        claimTypeConfig.ClaimType = MainGroupClaimTypeConfig.ClaimType;
                        claimTypeConfig.LDAPAttributeToShowAsDisplayText = MainGroupClaimTypeConfig.LDAPAttributeToShowAsDisplayText;
                    }
                    additionalClaimTypeConfigList.Add(claimTypeConfig);
                }

                this.ProcessedClaimTypesList = new List<ClaimTypeConfig>(claimTypesSetInTrust.Count + additionalClaimTypeConfigList.Count);
                this.ProcessedClaimTypesList.AddRange(claimTypesSetInTrust);
                this.ProcessedClaimTypesList.AddRange(additionalClaimTypeConfigList);

                // Any metadata for a user with at least an LDAP attribute and a LDAP class is valid
                this.MetadataConfig = nonProcessedClaimTypes.Where(x =>
                    !String.IsNullOrEmpty(x.EntityDataKey) &&
                    !String.IsNullOrEmpty(x.LDAPAttribute) &&
                    !String.IsNullOrEmpty(x.LDAPClass));
            }
            catch (Exception ex)
            {
                ClaimsProviderLogging.LogException(ProviderInternalName, "in InitializeClaimTypeConfigList", TraceCategory.Core, ex);
                success = false;
            }
            return success;
        }

        /// <summary>
        /// DO NOT Override this method if you use a custom persisted object to hold your configuration.
        /// To get you custom persisted object, you must override property LDAPCP.PersistedObjectName and set its name
        /// </summary>
        /// <returns></returns>
        protected virtual ILDAPCPConfiguration GetConfiguration(Uri context, string[] entityTypes, string persistedObjectName)
        {
            return LDAPCPConfig.GetConfiguration(persistedObjectName);
            //if (String.Equals(ProviderInternalName, LDAPCP._ProviderInternalName, StringComparison.InvariantCultureIgnoreCase))
            //    return LDAPCPConfig.GetFromConfigDB(persistedObjectName);
            //else
            //    return null;
        }

        /// <summary>
        /// [Deprecated] Override this method to customize configuration of LDAPCP. Please use GetConfiguration instead.
        /// </summary>
        /// <param name="context">The context, as a URI</param>
        /// <param name="entityTypes">The EntityType entity types set to scope the search to</param>
        [Obsolete("SetCustomConfiguration is deprecated, please use GetConfiguration instead.")]
        protected virtual void SetCustomConfiguration(Uri context, string[] entityTypes)
        {
        }

        /// <summary>
        /// Check if LDAPCP should process input (and show results) based on current URL (context)
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        protected virtual bool CheckIfShouldProcessInput(Uri context)
        {
            if (context == null) return true;
            var webApp = SPWebApplication.Lookup(context);
            if (webApp == null) return false;
            if (webApp.IsAdministrationWebApplication) return true;

            // Not central admin web app, enable LDAPCP only if current web app uses it
            // It is not possible to exclude zones where LDAPCP is not used because:
            // Consider following scenario: default zone is NTLM, intranet zone is claims
            // In intranet zone, when creating permission, LDAPCP will be called 2 times, but the 2nd time (from FillResolve (SPClaim)) the context will always be the URL of default zone
            foreach (var zone in Enum.GetValues(typeof(SPUrlZone)))
            {
                SPIisSettings iisSettings = webApp.GetIisSettingsWithFallback((SPUrlZone)zone);
                if (!iisSettings.UseTrustedClaimsAuthenticationProvider)
                    continue;

                // Get the list of authentication providers associated with the zone
                foreach (SPAuthenticationProvider prov in iisSettings.ClaimsAuthenticationProviders)
                {
                    if (prov.GetType() == typeof(Microsoft.SharePoint.Administration.SPTrustedAuthenticationProvider))
                    {
                        // Check if the current SPTrustedAuthenticationProvider is associated with the claim provider
                        if (String.Equals(prov.ClaimProviderName, ProviderInternalName, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Get the first TrustedLoginProvider associated with current claim provider
        /// LIMITATION: The same claims provider (uniquely identified by its name) cannot be associated to multiple TrustedLoginProvider because at runtime there is no way to determine what TrustedLoginProvider is currently calling
        /// </summary>
        /// <param name="ProviderInternalName"></param>
        /// <returns></returns>
        public static SPTrustedLoginProvider GetSPTrustAssociatedWithCP(string ProviderInternalName)
        {
            var lp = SPSecurityTokenServiceManager.Local.TrustedLoginProviders.Where(x => String.Equals(x.ClaimProviderName, ProviderInternalName, StringComparison.OrdinalIgnoreCase));

            if (lp != null && lp.Count() == 1)
                return lp.First();

            if (lp != null && lp.Count() > 1)
                ClaimsProviderLogging.Log(String.Format("[{0}] Claims provider {0} is associated to multiple SPTrustedIdentityTokenIssuer, which is not supported because at runtime there is no way to determine what TrustedLoginProvider is currently calling", ProviderInternalName), TraceSeverity.Unexpected, EventSeverity.Error, TraceCategory.Core);

            ClaimsProviderLogging.Log(String.Format("[{0}] Claims provider {0} is not associated with any SPTrustedIdentityTokenIssuer so it cannot create permissions.\r\nVisit http://www.ldapcp.com for installation procedure or set property ClaimProviderName with PowerShell cmdlet Get-SPTrustedIdentityTokenIssuer to create association.", ProviderInternalName), TraceSeverity.High, EventSeverity.Warning, TraceCategory.Core);
            return null;
        }

        /// <summary>
        /// PickerEntity is resolved (underlined) but claim must be resolved to provide again a PickerEntity so that SharePoint can actually create the permission
        /// </summary>
        /// <param name="context"></param>
        /// <param name="entityTypes"></param>
        /// <param name="resolveInput"></param>
        /// <param name="resolved"></param>
        protected override void FillResolve(Uri context, string[] entityTypes, SPClaim resolveInput, List<Microsoft.SharePoint.WebControls.PickerEntity> resolved)
        {
            ClaimsProviderLogging.LogDebug(String.Format("[{0}] FillResolve(SPClaim) called, incoming claim value: \"{1}\", claim type: \"{2}\", claim issuer: \"{3}\"", ProviderInternalName, resolveInput.Value, resolveInput.ClaimType, resolveInput.OriginalIssuer));

            SPSecurity.RunWithElevatedPrivileges(delegate ()
            {
                if (!Initialize(context, entityTypes))
                    return;

                // Ensure incoming claim should be validated by LDAPCP
                // Must be made after call to Initialize because SPTrustedLoginProvider name must be known
                if (!String.Equals(resolveInput.OriginalIssuer, IssuerName, StringComparison.InvariantCultureIgnoreCase))
                    return;

                this.Lock_Config.EnterReadLock();
                try
                {
                    OperationContext infos = new OperationContext(CurrentConfiguration, OperationType.Validation, ProcessedClaimTypesList, resolveInput.Value, resolveInput, context, entityTypes, null, Int32.MaxValue);
                    List<PickerEntity> permissions = SearchOrValidate(infos);
                    if (permissions.Count == 1)
                    {
                        resolved.Add(permissions[0]);
                        ClaimsProviderLogging.Log(String.Format("[{0}] Validated permission: claim value: \"{1}\", claim type: \"{2}\"", ProviderInternalName, permissions[0].Claim.Value, permissions[0].Claim.ClaimType),
                            TraceSeverity.Medium, EventSeverity.Information, TraceCategory.Claims_Picking);
                    }
                    else
                    {
                        ClaimsProviderLogging.Log(String.Format("[{0}] Validation of incoming claim returned {1} permissions instead of 1 expected. Aborting operation", ProviderInternalName, permissions.Count.ToString()), TraceSeverity.Unexpected, EventSeverity.Error, TraceCategory.Claims_Picking);
                    }
                }
                catch (Exception ex)
                {
                    ClaimsProviderLogging.LogException(ProviderInternalName, "in FillResolve(SPClaim)", TraceCategory.Claims_Picking, ex);
                }
                finally
                {
                    this.Lock_Config.ExitReadLock();
                }
            });
        }

        /// <summary>
        /// Called during a search in the small people picker control
        /// </summary>
        /// <param name="context"></param>
        /// <param name="entityTypes"></param>
        /// <param name="resolveInput"></param>
        /// <param name="resolved"></param>
        protected override void FillResolve(Uri context, string[] entityTypes, string resolveInput, List<Microsoft.SharePoint.WebControls.PickerEntity> resolved)
        {
            ClaimsProviderLogging.LogDebug(String.Format("[{0}] FillResolve(string) called, incoming input \"{1}\"", ProviderInternalName, resolveInput));

            SPSecurity.RunWithElevatedPrivileges(delegate ()
            {
                if (!Initialize(context, entityTypes))
                    return;

                this.Lock_Config.EnterReadLock();
                try
                {
                    OperationContext settings = new OperationContext(CurrentConfiguration, OperationType.Search, ProcessedClaimTypesList, resolveInput, null, context, entityTypes, null, Int32.MaxValue);
                    List<PickerEntity> permissions = SearchOrValidate(settings);
                    FillPermissions(context, entityTypes, resolveInput, ref permissions);
                    foreach (PickerEntity permission in permissions)
                    {
                        resolved.Add(permission);
                        ClaimsProviderLogging.Log($"[{ProviderInternalName}] Added permission: claim value: '{permission.Claim.Value}', claim type: '{permission.Claim.ClaimType}', display text: '{permission.DisplayText}'",
                            TraceSeverity.Verbose, EventSeverity.Information, TraceCategory.Claims_Picking);
                    }
                    ClaimsProviderLogging.Log($"[{ProviderInternalName}] Added {permissions.Count} permissions",
                        TraceSeverity.Medium, EventSeverity.Information, TraceCategory.Claims_Picking);
                }
                catch (Exception ex)
                {
                    ClaimsProviderLogging.LogException(ProviderInternalName, "in FillResolve(string)", TraceCategory.Claims_Picking, ex);
                }
                finally
                {
                    this.Lock_Config.ExitReadLock();
                }
            });
        }

        protected override void FillSearch(Uri context, string[] entityTypes, string searchPattern, string hierarchyNodeID, int maxCount, Microsoft.SharePoint.WebControls.SPProviderHierarchyTree searchTree)
        {
            ClaimsProviderLogging.LogDebug(String.Format("[{0}] FillSearch called, incoming input: \"{1}\"", ProviderInternalName, searchPattern));

            SPSecurity.RunWithElevatedPrivileges(delegate ()
            {
                if (!Initialize(context, entityTypes))
                    return;

                this.Lock_Config.EnterReadLock();
                try
                {
                    OperationContext settings = new OperationContext(CurrentConfiguration, OperationType.Search, ProcessedClaimTypesList, searchPattern, null, context, entityTypes, hierarchyNodeID, maxCount);
                    List<PickerEntity> permissions = SearchOrValidate(settings);
                    FillPermissions(context, entityTypes, searchPattern, ref permissions);
                    SPProviderHierarchyNode matchNode = null;
                    foreach (PickerEntity permission in permissions)
                    {
                        // Add current PickerEntity to the corresponding attribute in the hierarchy
                        if (searchTree.HasChild(permission.Claim.ClaimType))
                        {
                            matchNode = searchTree.Children.First(x => x.HierarchyNodeID == permission.Claim.ClaimType);
                        }
                        else
                        {
                            ClaimTypeConfig attrHelper = ProcessedClaimTypesList.FirstOrDefault(x =>
                                !x.UseMainClaimTypeOfDirectoryObject &&
                                String.Equals(x.ClaimType, permission.Claim.ClaimType, StringComparison.InvariantCultureIgnoreCase));

                            string nodeName = attrHelper != null ? attrHelper.ClaimTypeDisplayName : permission.Claim.ClaimType;
                            matchNode = new SPProviderHierarchyNode(_ProviderInternalName, nodeName, permission.Claim.ClaimType, true);
                            searchTree.AddChild(matchNode);
                        }
                        matchNode.AddEntity(permission);
                        ClaimsProviderLogging.Log($"[{ProviderInternalName}] Added permission: claim value: '{permission.Claim.Value}', claim type: '{permission.Claim.ClaimType}', display text: '{permission.DisplayText}'",
                            TraceSeverity.Verbose, EventSeverity.Information, TraceCategory.Claims_Picking);
                    }
                    ClaimsProviderLogging.Log($"[{ProviderInternalName}] Added {permissions.Count} permissions",
                        TraceSeverity.Medium, EventSeverity.Information, TraceCategory.Claims_Picking);
                }
                catch (Exception ex)
                {
                    ClaimsProviderLogging.LogException(ProviderInternalName, "in FillSearch", TraceCategory.Claims_Picking, ex);
                }
                finally
                {
                    this.Lock_Config.ExitReadLock();
                }
            });
        }

        /// <summary>
        /// Search and validate requests coming from SharePoint
        /// </summary>
        /// <param name="requestInfo">Information about current context and operation</param>
        /// <returns></returns>
        protected virtual List<PickerEntity> SearchOrValidate(OperationContext requestInfo)
        {
            List<PickerEntity> permissions = new List<PickerEntity>();
            try
            {
                if (this.CurrentConfiguration.BypassLDAPLookup)
                {
                    // Completely bypass LDAP lookp
                    //FINDTOWHERE
                    List<PickerEntity> entities = CreatePickerEntityForSpecificClaimTypes(
                        requestInfo.Input,
                        requestInfo.CurrentClaimTypeConfigList.Where(x => !x.UseMainClaimTypeOfDirectoryObject),
                        false);
                    if (entities != null)
                    {
                        foreach (var entity in entities)
                        {
                            permissions.Add(entity);
                            ClaimsProviderLogging.Log(String.Format("[{0}] Added permission created without LDAP lookup because LDAPCP configured to always resolve input: claim value: {1}, claim type: \"{2}\"", ProviderInternalName, entity.Claim.Value, entity.Claim.ClaimType),
                                TraceSeverity.Verbose, EventSeverity.Information, TraceCategory.Claims_Picking);
                        }
                    }
                    return permissions;
                }

                if (requestInfo.OperationType == OperationType.Search)
                {
                    //FINDTOWHERE
                    IEnumerable<ClaimTypeConfig> attribsMatchInputPrefix = requestInfo.CurrentClaimTypeConfigList.Where(x =>
                        !String.IsNullOrEmpty(x.PrefixToBypassLookup) &&
                        requestInfo.Input.StartsWith(x.PrefixToBypassLookup, StringComparison.InvariantCultureIgnoreCase));
                    if (attribsMatchInputPrefix.Count() > 0)
                    {
                        // Input has a prefix, so it should be validated with no lookup
                        ClaimTypeConfig attribMatchInputPrefix = attribsMatchInputPrefix.First();
                        if (attribsMatchInputPrefix.Count() > 1)
                        {
                            // Multiple attributes have same prefix, which is not allowed
                            ClaimsProviderLogging.Log(String.Format("[{0}] Multiple attributes have same prefix ({1}), which is not allowed.", ProviderInternalName, attribMatchInputPrefix.PrefixToBypassLookup), TraceSeverity.Unexpected, EventSeverity.Error, TraceCategory.Claims_Picking);
                            return permissions;
                        }

                        // Check if a keyword was typed to bypass lookup and create permission manually
                        requestInfo.Input = requestInfo.Input.Substring(attribMatchInputPrefix.PrefixToBypassLookup.Length);
                        if (String.IsNullOrEmpty(requestInfo.Input)) return permissions;    // Keyword was found but nothing typed after, give up
                        PickerEntity entity = CreatePickerEntityForSpecificClaimType(
                            requestInfo.Input,
                            attribMatchInputPrefix,
                            true);
                        if (entity != null)
                        {
                            permissions.Add(entity);
                            ClaimsProviderLogging.Log(String.Format("[{0}] Added permission created without LDAP lookup because input matches a keyword: claim value: \"{1}\", claim type: \"{2}\"", ProviderInternalName, entity.Claim.Value, entity.Claim.ClaimType),
                                TraceSeverity.Verbose, EventSeverity.Information, TraceCategory.Claims_Picking);
                            return permissions;
                        }
                    }
                    SearchOrValidateWithLDAP(requestInfo, ref permissions);
                }
                else if (requestInfo.OperationType == OperationType.Validation)
                {
                    SearchOrValidateWithLDAP(requestInfo, ref permissions);
                    if (!String.IsNullOrEmpty(requestInfo.CurrentClaimTypeConfig.PrefixToBypassLookup))
                    {
                        // At this stage, it is impossible to know if input was originally created with the keyword that bypasses LDAP lookup
                        // But it should be validated anyway since keyword is set for this claim type
                        // If previous LDAP lookup found the permission, return it as is
                        if (permissions.Count == 1) return permissions;

                        // If we don't get exactly 1 permission, create it manually
                        PickerEntity entity = CreatePickerEntityForSpecificClaimType(
                            requestInfo.Input,
                            requestInfo.CurrentClaimTypeConfig,
                            requestInfo.InputHasKeyword);
                        if (entity != null)
                        {
                            permissions.Add(entity);
                            ClaimsProviderLogging.Log(String.Format("[{0}] Added permission without LDAP lookup because corresponding claim type has a keyword associated. Claim value: \"{1}\", Claim type: \"{2}\"", ProviderInternalName, entity.Claim.Value, entity.Claim.ClaimType),
                                TraceSeverity.Verbose, EventSeverity.Information, TraceCategory.Claims_Picking);
                        }
                        return permissions;
                    }
                }
            }
            catch (Exception ex)
            {
                ClaimsProviderLogging.LogException(ProviderInternalName, "in SearchOrValidate", TraceCategory.Claims_Picking, ex);
            }
            return permissions;
        }

        /// <summary>
        /// Search and validate requests coming from SharePoint with LDAP lookup
        /// </summary>
        /// <param name="requestInfo">Information about current context and operation</param>
        /// <param name="permissions"></param>
        /// <returns></returns>
        protected virtual void SearchOrValidateWithLDAP(OperationContext requestInfo, ref List<PickerEntity> permissions)
        {
            //DirectoryEntry[] directories = GetLDAPServers(requestInfo);
            LDAPConnection[] connections = GetLDAPServers(requestInfo);
            if (connections == null || connections.Length == 0)
            {
                ClaimsProviderLogging.Log(String.Format("[{0}] No LDAP server is configured.", ProviderInternalName), TraceSeverity.Unexpected, EventSeverity.Error, TraceCategory.Configuration);
                return;
            }

            List<LDAPConnectionSettings> LDAPServers = new List<LDAPConnectionSettings>(connections.Length);
            foreach (LDAPConnection coco in connections)
            {
                LDAPServers.Add(new LDAPConnectionSettings() { Directory = coco.LDAPServer });
            }
            BuildLDAPFilter(requestInfo, ref LDAPServers);

            bool resultsfound = false;
            List<LDAPSearchResultWrapper> LDAPSearchResultWrappers = new List<LDAPSearchResultWrapper>();
            using (new SPMonitoredScope(String.Format("[{0}] Total time spent in all LDAP server(s)", ProviderInternalName), 1000))
            {
                resultsfound = QueryLDAPServers(LDAPServers, requestInfo, ref LDAPSearchResultWrappers);
            }

            if (!resultsfound) return;
            ConsolidatedResultCollection results = ProcessLdapResults(requestInfo, ref LDAPSearchResultWrappers);

            if (results.Count > 0)
            {
                // There may be some extra work based on settings associated with the input claim type
                // Check to see if we have a prefix and have a domain token
                if (requestInfo.OperationType == OperationType.Validation
                    && requestInfo.CurrentClaimTypeConfig.ClaimValuePrefix != null)
                {
                    // Extract just the domain from the input
                    bool tokenFound = false;
                    string domainOnly = String.Empty;
                    if (requestInfo.CurrentClaimTypeConfig.ClaimValuePrefix.Contains(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME))
                    {
                        tokenFound = true;
                        domainOnly = OperationContext.GetDomainFromFullAccountName(requestInfo.IncomingEntity.Value);
                    }
                    else if (requestInfo.CurrentClaimTypeConfig.ClaimValuePrefix.Contains(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN))
                    {
                        tokenFound = true;
                        string fqdn = OperationContext.GetDomainFromFullAccountName(requestInfo.IncomingEntity.Value);
                        domainOnly = OperationContext.GetFirstSubString(fqdn, ".");
                    }

                    if (tokenFound)
                    {
                        // Only keep results where the domain is a match
                        ConsolidatedResultCollection filteredResults = new ConsolidatedResultCollection();
                        foreach (var result in results)
                        {
                            if (String.Equals(result.DomainName, domainOnly, StringComparison.InvariantCultureIgnoreCase))
                                filteredResults.Add(result);
                        }
                        results = filteredResults;
                    }
                }

                foreach (var result in results)
                {
                    permissions.Add(result.PickerEntity);
                    ClaimsProviderLogging.Log(String.Format("[{0}] Added permission created with LDAP lookup: claim value: \"{1}\", claim type: \"{2}\"", ProviderInternalName, result.PickerEntity.Claim.Value, result.PickerEntity.Claim.ClaimType),
                        TraceSeverity.VerboseEx, EventSeverity.Information, TraceCategory.Claims_Picking);
                }
            }
        }

        /// <summary>
        /// Processes LDAP results stored in LDAPSearchResultWrappers and returns result in parameter results
        /// </summary>
        /// <param name="requestInfo">Information about current context and operation</param>
        /// <param name="LDAPSearchResultWrappers"></param>
        /// <returns></returns>
        protected virtual ConsolidatedResultCollection ProcessLdapResults(OperationContext requestInfo, ref List<LDAPSearchResultWrapper> LDAPSearchResultWrappers)
        {
            ConsolidatedResultCollection results = new ConsolidatedResultCollection();
            ResultPropertyCollection resultPropertyCollection;
            IEnumerable<ClaimTypeConfig> attributes;
            // If exactSearch is true, we don't care about attributes with UseMainClaimTypeOfDirectoryObject = true
            //FINDTOWHERE
            if (requestInfo.ExactSearch) attributes = requestInfo.CurrentClaimTypeConfigList.Where(x => !x.UseMainClaimTypeOfDirectoryObject);
            else attributes = requestInfo.CurrentClaimTypeConfigList;

            foreach (LDAPSearchResultWrapper LDAPresult in LDAPSearchResultWrappers)
            {
                resultPropertyCollection = LDAPresult.SearchResult.Properties;
                // objectclass attribute should never be missing because it is explicitely requested in LDAP query
                if (!resultPropertyCollection.Contains(LDAPObjectClassName))
                {
                    ClaimsProviderLogging.Log(String.Format("[{0}] Property \"{1}\" is missing in LDAP result, this is probably due to insufficient permissions of account doing query in LDAP server {2}.", ProviderInternalName, LDAPObjectClassName, LDAPresult.DomainFQDN), TraceSeverity.Unexpected, EventSeverity.Error, TraceCategory.LDAP_Lookup);
                    continue;
                }

                // Cast collection to be able to use StringComparer.InvariantCultureIgnoreCase for case insensitive search of ldap properties
                var resultPropertyCollectionPropertyNames = resultPropertyCollection.PropertyNames.Cast<string>();

                // Issue https://github.com/Yvand/LDAPCP/issues/16: Ensure identity attribute exists in current LDAP result
                if (resultPropertyCollection[LDAPObjectClassName].Cast<string>().Contains(IdentityClaimTypeConfig.LDAPClass, StringComparer.InvariantCultureIgnoreCase))
                {
                    // This is a user: check if his identity LDAP attribute (e.g. mail or sAMAccountName) is present
                    if (!resultPropertyCollectionPropertyNames.Contains(IdentityClaimTypeConfig.LDAPAttribute, StringComparer.InvariantCultureIgnoreCase))
                    {
                        ClaimsProviderLogging.Log(String.Format("[{0}] A user was ignored because it is missing the identity attribute '{1}'", ProviderInternalName, IdentityClaimTypeConfig.LDAPAttribute), TraceSeverity.Medium, EventSeverity.Information, TraceCategory.LDAP_Lookup);
                        continue;
                    }
                }
                else
                {
                    // This is a group: get the identity attribute of groups, and ensure it is present
                    var groupAttribute = attributes.FirstOrDefault(x => resultPropertyCollection[LDAPObjectClassName].Contains(x.LDAPClass) && x.ClaimType != null);
                    if (groupAttribute != null && !resultPropertyCollectionPropertyNames.Contains(groupAttribute.LDAPAttribute, StringComparer.InvariantCultureIgnoreCase))
                    {
                        ClaimsProviderLogging.Log(String.Format("[{0}] A group was ignored because it is missing the identity attribute '{1}'", ProviderInternalName, groupAttribute.LDAPAttribute), TraceSeverity.Medium, EventSeverity.Information, TraceCategory.LDAP_Lookup);
                        continue;
                    }
                }

                foreach (var attr in attributes)
                {
                    // Check if current attribute object class matches the current LDAP result
                    if (!resultPropertyCollection[LDAPObjectClassName].Cast<string>().Contains(attr.LDAPClass, StringComparer.InvariantCultureIgnoreCase)) continue;

                    // Check if current LDAP result contains LDAP attribute of current attribute
                    if (!resultPropertyCollectionPropertyNames.Contains(attr.LDAPAttribute, StringComparer.InvariantCultureIgnoreCase)) continue;

                    // TODO: investigate http://ldapcp.codeplex.com/discussions/648655
                    string value = resultPropertyCollection[resultPropertyCollectionPropertyNames.Where(x => x.ToLowerInvariant() == attr.LDAPAttribute.ToLowerInvariant()).First()][0].ToString();
                    // Check if current attribute matches the input
                    if (requestInfo.ExactSearch)
                    {
                        if (!String.Equals(value, requestInfo.Input, StringComparison.InvariantCultureIgnoreCase)) continue;
                    }
                    else
                    {
                        if (this.CurrentConfiguration.AddWildcardAsPrefixOfInput)
                        {
                            // Changed to a case insensitive search
                            if (value.IndexOf(requestInfo.Input, StringComparison.InvariantCultureIgnoreCase) != -1) continue;
                        }
                        else
                        {
                            if (!value.StartsWith(requestInfo.Input, StringComparison.InvariantCultureIgnoreCase)) continue;
                        }
                    }

                    // Add to collection of objectclass/ldap attribute in list of results if it doesn't already exist
                    ClaimTypeConfig objCompare;
                    if (attr.UseMainClaimTypeOfDirectoryObject && (String.Equals(attr.LDAPClass, IdentityClaimTypeConfig.LDAPClass, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        if (!resultPropertyCollectionPropertyNames.Contains(attr.LDAPAttribute, StringComparer.InvariantCultureIgnoreCase)) continue;
                        // If exactSearch is true, then IdentityAttribute.LDAPAttribute value should be also equals to input, otherwise igno
                        objCompare = IdentityClaimTypeConfig;
                    }
                    else
                    {
                        objCompare = attr;
                    }

                    // When token domain is present, then ensure we do compare with the actual domain name
                    // There are 2 scenarios to
                    bool compareWithDomain = HasPrefixToken(attr.ClaimValuePrefix, ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME) ? true : this.CurrentConfiguration.CompareResultsWithDomainNameProp;
                    if (!compareWithDomain) compareWithDomain = HasPrefixToken(attr.ClaimValuePrefix, ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN) ? true : this.CurrentConfiguration.CompareResultsWithDomainNameProp;
                    if (results.Contains(LDAPresult, objCompare, compareWithDomain))
                        continue;

                    results.Add(
                        new ConsolidatedResult
                        {
                            Attribute = attr,
                            LDAPResults = resultPropertyCollection,
                            Value = value,
                            DomainName = LDAPresult.DomainName,
                            DomainFQDN = LDAPresult.DomainFQDN,
                            //DEBUG = String.Format("LDAPAttribute: {0}, LDAPAttributeValue: {1}, AlwaysResolveAgainstIdentityClaim: {2}", attr.LDAPAttribute, resultPropertyCollection[attr.LDAPAttribute][0].ToString(), attr.AlwaysResolveAgainstIdentityClaim.ToString())
                        });
                }
            }
            ClaimsProviderLogging.Log(String.Format("[{0}] {1} permission(s) to create after filtering", ProviderInternalName, results.Count), TraceSeverity.Medium, EventSeverity.Information, TraceCategory.LDAP_Lookup);
            foreach (var result in results)
            {
                PickerEntity pe = CreatePickerEntityHelper(result);
                // Add it to the return list of picker entries.
                result.PickerEntity = pe;
            }
            return results;
        }

        /// <summary>
        /// Override this method to change LDAP filter created by LDAPCP. It's possible to set a different LDAP filter for each LDAP server
        /// </summary>
        /// <param name="context">Information about current context and operation</param>
        /// <param name="ldapServers">List to be populated by this method</param>
        protected virtual void BuildLDAPFilter(OperationContext context, ref List<LDAPConnectionSettings> ldapServers)
        {
            string filter = BuildLDAPFilterForCurrentContext(context);
            foreach (LDAPConnectionSettings ldapServer in ldapServers)
            {
                ldapServer.Filter = filter.ToString();
            }
        }

        /// <summary>
        /// Returns the LDAP filter based on settings provided
        /// </summary>
        /// <param name="context">Information about current context and operation</param>
        /// <returns>LDAP filter created from settings provided</returns>
        protected string BuildLDAPFilterForCurrentContext(OperationContext context)
        {
            // Build LDAP filter as documented in http://technet.microsoft.com/fr-fr/library/aa996205(v=EXCHG.65).aspx
            StringBuilder filter = new StringBuilder();
            if (this.CurrentConfiguration.FilterEnabledUsersOnlyProp) filter.Append(LDAPFilterEnabledUsersOnly);
            filter.Append("(| ");   // START OR

            string preferredFilterPattern;
            string input = context.Input;
            if (context.ExactSearch) preferredFilterPattern = input;
            else preferredFilterPattern = this.CurrentConfiguration.AddWildcardAsPrefixOfInput ? "*" + input + "*" : input + "*";

            foreach (var ctConfig in context.CurrentClaimTypeConfigList)
            {
                if (ctConfig.SupportsWildcard)
                    filter.Append(AddAttributeToFilter(ctConfig, preferredFilterPattern));
                else
                    filter.Append(AddAttributeToFilter(ctConfig, input));
            }

            if (this.CurrentConfiguration.FilterEnabledUsersOnlyProp) filter.Append(")");
            filter.Append(")");     // END OR
            return filter.ToString();
        }

        /// <summary>
        /// Query LDAP servers in parallel
        /// </summary>
        /// <param name="LDAPServers">LDAP servers to query</param>
        /// <param name="requestInfo">Information about current context and operation</param>
        /// <param name="LDAPSearchResults">LDAP search results list to be populated by this method</param>
        /// <returns>true if a result was found</returns>
        protected bool QueryLDAPServers(List<LDAPConnectionSettings> LDAPServers, OperationContext requestInfo, ref List<LDAPSearchResultWrapper> LDAPSearchResults)
        {
            if (LDAPServers == null || LDAPServers.Count == 0) return false;
            object lockResults = new object();
            List<LDAPSearchResultWrapper> results = new List<LDAPSearchResultWrapper>();
            Stopwatch globalStopWatch = new Stopwatch();
            globalStopWatch.Start();

            Parallel.ForEach(LDAPServers, LDAPServer =>
            {
                if (LDAPServer == null) return;
                if (String.IsNullOrEmpty(LDAPServer.Filter))
                {
                    ClaimsProviderLogging.Log(String.Format("[{0}] Skipping query on LDAP Server \"{1}\" because it doesn't have any filter, this usually indicates a problem in method GetLDAPFilter.", ProviderInternalName, LDAPServer.Directory.Path), TraceSeverity.Unexpected, EventSeverity.Information, TraceCategory.LDAP_Lookup);
                    return;
                }
                DirectoryEntry directory = LDAPServer.Directory;
                using (DirectorySearcher ds = new DirectorySearcher(LDAPServer.Filter))
                {
                    ds.SearchRoot = directory;
                    ds.ClientTimeout = new TimeSpan(0, 0, this.CurrentConfiguration.LDAPQueryTimeout); // Set the timeout of the query
                    ds.PropertiesToLoad.Add(LDAPObjectClassName);
                    ds.PropertiesToLoad.Add("nETBIOSName");
                    foreach (var ldapAttribute in ProcessedClaimTypesList.Where(x => !String.IsNullOrEmpty(x.LDAPAttribute)))
                    {
                        ds.PropertiesToLoad.Add(ldapAttribute.LDAPAttribute);
                        if (!String.IsNullOrEmpty(ldapAttribute.LDAPAttributeToShowAsDisplayText)) ds.PropertiesToLoad.Add(ldapAttribute.LDAPAttributeToShowAsDisplayText);
                    }
                    // Populate additional attributes that are not part of the filter but are requested in the result
                    foreach (var metadataAttribute in MetadataConfig)
                    {
                        if (!ds.PropertiesToLoad.Contains(metadataAttribute.LDAPAttribute)) ds.PropertiesToLoad.Add(metadataAttribute.LDAPAttribute);
                    }

                    using (new SPMonitoredScope(String.Format("[{0}] Connecting to \"{1}\" with AuthenticationType \"{2}\" and filter \"{3}\"", ProviderInternalName, directory.Path, directory.AuthenticationType.ToString(), ds.Filter), 3000)) // threshold of 3 seconds before it's considered too much. If exceeded it is recorded in a higher logging level
                    {
                        try
                        {
                            ClaimsProviderLogging.Log(String.Format("[{0}] Connecting to \"{1}\" with AuthenticationType \"{2}\" and filter \"{3}\"", ProviderInternalName, directory.Path, directory.AuthenticationType.ToString(), ds.Filter), TraceSeverity.Verbose, EventSeverity.Information, TraceCategory.LDAP_Lookup);
                            Stopwatch stopWatch = new Stopwatch();
                            stopWatch.Start();
                            using (SearchResultCollection directoryResults = ds.FindAll())
                            {
                                stopWatch.Stop();
                                ClaimsProviderLogging.Log(String.Format("[{0}] Got {1} result(s) in {2}ms from \"{3}\" with query \"{4}\"", ProviderInternalName, directoryResults.Count.ToString(), stopWatch.ElapsedMilliseconds.ToString(), directory.Path, ds.Filter), TraceSeverity.Medium, EventSeverity.Information, TraceCategory.LDAP_Lookup);
                                if (directoryResults != null && directoryResults.Count > 0)
                                {
                                    lock (lockResults)
                                    {
                                        // Retrieve FQDN and domain name of current DirectoryEntry
                                        string domainName, domainFQDN = String.Empty;
                                        OperationContext.GetDomainInformation(directory, out domainName, out domainFQDN);
                                        foreach (SearchResult item in directoryResults)
                                        {
                                            results.Add(new LDAPSearchResultWrapper()
                                            {
                                                SearchResult = item,
                                                DomainName = domainName,
                                                DomainFQDN = domainFQDN,
                                            });
                                        }
                                    }
                                    ClaimsProviderLogging.Log(String.Format("[{0}] Got {1} result(s) from {2}", ProviderInternalName, directoryResults.Count.ToString(), directory.Path), TraceSeverity.Verbose, EventSeverity.Information, TraceCategory.LDAP_Lookup);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            ClaimsProviderLogging.LogException(ProviderInternalName, "during connection to LDAP server " + directory.Path, TraceCategory.LDAP_Lookup, ex);
                        }
                        finally
                        {
                            directory.Dispose();
                        }
                    }
                }
            });

            globalStopWatch.Stop();
            LDAPSearchResults = results;
            ClaimsProviderLogging.Log(String.Format("[{0}] Got {1} result(s) in {2}ms from all servers with query \"{3}\"", ProviderInternalName, LDAPSearchResults.Count, globalStopWatch.ElapsedMilliseconds.ToString(), LDAPServers[0].Filter), TraceSeverity.Verbose, EventSeverity.Information, TraceCategory.LDAP_Lookup);
            return LDAPSearchResults.Count > 0;
        }

        /// <summary>
        /// Override this method to set LDAP connections
        /// </summary>
        /// <param name="requestInfo">Information about current context and operation</param>
        /// <returns>Array of LDAP servers to query</returns>
        protected virtual LDAPConnection[] GetLDAPServers(OperationContext requestInfo)
        {
            if (this.CurrentConfiguration.LDAPConnectionsProp == null) return null;
            IEnumerable<LDAPConnection> ldapConnections = this.CurrentConfiguration.LDAPConnectionsProp;
            if (requestInfo.OperationType == OperationType.Augmentation) ldapConnections = this.CurrentConfiguration.LDAPConnectionsProp.Where(x => x.AugmentationEnabled);
            LDAPConnection[] connections = new LDAPConnection[ldapConnections.Count()];

            int i = 0;
            foreach (var ldapConnection in ldapConnections)
            {
                LDAPConnection coco = ldapConnection.CopyPersistedProperties();
                if (!ldapConnection.UserServerDirectoryEntry)
                {
                    coco.LDAPServer = new DirectoryEntry(ldapConnection.Path, ldapConnection.Username, ldapConnection.Password, ldapConnection.AuthenticationTypes);
                    string serverType = coco.GetGroupMembershipAsADDomain ? "AD" : "LDAP";
                    ClaimsProviderLogging.Log(String.Format("[{0}] Add {1} server \"{2}\" with AuthenticationType \"{3}\" and credentials \"{4}\".", ProviderInternalName, serverType, ldapConnection.Path, ldapConnection.AuthenticationTypes.ToString(), ldapConnection.Username), TraceSeverity.Verbose, EventSeverity.Information, TraceCategory.LDAP_Lookup);
                }
                else
                {
                    coco.LDAPServer = Domain.GetComputerDomain().GetDirectoryEntry();
                    ClaimsProviderLogging.Log(String.Format("[{0}] Add AD server \"{1}\" with AuthenticationType \"{2}\" and credentials of application pool account.", ProviderInternalName, coco.LDAPServer.Path, coco.LDAPServer.AuthenticationType.ToString()), TraceSeverity.Verbose, EventSeverity.Information, TraceCategory.LDAP_Lookup);
                }
                connections[i++] = coco;
            }
            return connections;
        }

        protected override void FillClaimTypes(List<string> claimTypes)
        {
            if (claimTypes == null)
                throw new ArgumentNullException("claimTypes");

            ClaimsProviderLogging.LogDebug(String.Format("[{0}] FillClaimValueTypes called, ProcessedAttributes null: {1}", ProviderInternalName, ProcessedClaimTypesList == null ? true : false));

            if (ProcessedClaimTypesList == null)
                return;

            this.Lock_Config.EnterReadLock();
            try
            {
                foreach (var attribute in ProcessedClaimTypesList.Where(x => !String.IsNullOrEmpty(x.ClaimType)))
                {
                    claimTypes.Add(attribute.ClaimType);
                }
            }
            finally
            {
                this.Lock_Config.ExitReadLock();
            }
        }

        protected override void FillClaimValueTypes(List<string> claimValueTypes)
        {
            if (claimValueTypes == null)
                throw new ArgumentNullException("claimValueTypes");

            ClaimsProviderLogging.LogDebug(String.Format("[{0}] FillClaimValueTypes called, ProcessedAttributes null: {1}", ProviderInternalName, ProcessedClaimTypesList == null ? true : false));

            if (ProcessedClaimTypesList == null)
                return;

            this.Lock_Config.EnterReadLock();
            try
            {
                foreach (var attribute in ProcessedClaimTypesList.Where(x => !String.IsNullOrEmpty(x.ClaimValueType)))
                {
                    claimValueTypes.Add(attribute.ClaimValueType);
                }
            }
            finally
            {
                this.Lock_Config.ExitReadLock();
            }
        }

        protected override void FillClaimsForEntity(Uri context, SPClaim entity, List<SPClaim> claims)
        {
            AugmentEntity(context, entity, null, claims);
        }

        protected override void FillClaimsForEntity(Uri context, SPClaim entity, SPClaimProviderContext claimProviderContext, List<SPClaim> claims)
        {
            AugmentEntity(context, entity, claimProviderContext, claims);
        }

        /// <summary>
        /// Perform augmentation of entity supplied
        /// </summary>
        /// <param name="context"></param>
        /// <param name="entity">entity to augment</param>
        /// <param name="claimProviderContext">Can be null</param>
        /// <param name="claims"></param>
        protected virtual void AugmentEntity(Uri context, SPClaim entity, SPClaimProviderContext claimProviderContext, List<SPClaim> claims)
        {

            SPClaim decodedEntity;
            if (SPClaimProviderManager.IsUserIdentifierClaim(entity))
                decodedEntity = SPClaimProviderManager.DecodeUserIdentifierClaim(entity);
            else
            {
                if (SPClaimProviderManager.IsEncodedClaim(entity.Value))
                    decodedEntity = SPClaimProviderManager.Local.DecodeClaim(entity.Value);
                else
                    decodedEntity = entity;
            }

            SPOriginalIssuerType loginType = SPOriginalIssuers.GetIssuerType(decodedEntity.OriginalIssuer);
            if (loginType != SPOriginalIssuerType.TrustedProvider && loginType != SPOriginalIssuerType.ClaimProvider)
            {
                ClaimsProviderLogging.LogDebug(String.Format("[{0}] Not trying to augment '{1}' because OriginalIssuer is '{2}'.", ProviderInternalName, decodedEntity.Value, decodedEntity.OriginalIssuer));
                return;
            }

            SPSecurity.RunWithElevatedPrivileges(delegate ()
            {
                if (!Initialize(context, null))
                    return;

                this.Lock_Config.EnterReadLock();
                try
                {
                    ClaimsProviderLogging.LogDebug(String.Format("[{0}] Original entity to augment: '{1}', augmentation enabled: {2}.", ProviderInternalName, entity.Value, CurrentConfiguration.EnableAugmentation));
                    if (!this.CurrentConfiguration.EnableAugmentation) return;
                    if (String.IsNullOrEmpty(this.CurrentConfiguration.ClaimTypeUsedForAugmentation))
                    {
                        ClaimsProviderLogging.Log(String.Format("[{0}] Augmentation is enabled but no claim type is configured.", ProviderInternalName),
                            TraceSeverity.High, EventSeverity.Error, TraceCategory.Augmentation);
                        return;
                    }
                    var groupAttribute = this.ProcessedClaimTypesList.FirstOrDefault(x => String.Equals(x.ClaimType, this.CurrentConfiguration.ClaimTypeUsedForAugmentation, StringComparison.InvariantCultureIgnoreCase) && !x.UseMainClaimTypeOfDirectoryObject);
                    if (groupAttribute == null)
                    {
                        ClaimsProviderLogging.Log(String.Format("[{0}] Settings for claim type \"{1}\" cannot be found, its entry may have been deleted from claims mapping table.", ProviderInternalName, this.CurrentConfiguration.ClaimTypeUsedForAugmentation),
                            TraceSeverity.High, EventSeverity.Error, TraceCategory.Augmentation);
                        return;
                    }

                    OperationContext infos = new OperationContext(CurrentConfiguration, OperationType.Augmentation, ProcessedClaimTypesList, null, decodedEntity, context, null, null, Int32.MaxValue);
                    LDAPConnection[] connections = GetLDAPServers(infos);
                    if (connections == null || connections.Length == 0)
                    {
                        ClaimsProviderLogging.Log(String.Format("[{0}] No LDAP server is enabled for augmentation", ProviderInternalName), TraceSeverity.High, EventSeverity.Error, TraceCategory.Augmentation);
                        return;
                    }

                    List<SPClaim> groups = new List<SPClaim>();
                    object lockResults = new object();
                    Stopwatch stopWatch = new Stopwatch();
                    stopWatch.Start();
                    Parallel.ForEach(connections, coco =>
                    {
                        List<SPClaim> directoryGroups;
                        if (coco.GetGroupMembershipAsADDomain)
                            directoryGroups = GetGroupsFromADDirectory(coco.LDAPServer, infos, groupAttribute);
                        else
                            directoryGroups = GetGroupsFromLDAPDirectory(coco.LDAPServer, infos, groupAttribute);

                        lock (lockResults)
                        {
                            groups.AddRange(directoryGroups);
                        }
                    });
                    stopWatch.Stop();
                    ClaimsProviderLogging.Log(String.Format("[{0}] LDAP queries to get group membership on all servers completed in {1}ms",
                        ProviderInternalName, stopWatch.ElapsedMilliseconds.ToString()),
                        TraceSeverity.Verbose, EventSeverity.Information, TraceCategory.Augmentation);

                    foreach (SPClaim group in groups)
                    {
                        claims.Add(group);
                        ClaimsProviderLogging.Log(String.Format("[{0}] Added group \"{1}\" to user \"{2}\"", ProviderInternalName, group.Value, infos.IncomingEntity.Value),
                            TraceSeverity.Verbose, EventSeverity.Information, TraceCategory.Augmentation);
                    }
                    if (groups.Count > 0)
                        ClaimsProviderLogging.Log(String.Format("[{0}] User '{1}' was augmented with {2} groups of claim type '{3}'", ProviderInternalName, infos.IncomingEntity.Value, groups.Count.ToString(), groupAttribute.ClaimType),
                            TraceSeverity.Medium, EventSeverity.Information, TraceCategory.Augmentation);
                    else
                        ClaimsProviderLogging.Log(String.Format("[{0}] No group found for user '{1}' during augmentation", ProviderInternalName, infos.IncomingEntity.Value),
                            TraceSeverity.Medium, EventSeverity.Information, TraceCategory.Augmentation);
                }
                catch (Exception ex)
                {
                    ClaimsProviderLogging.LogException(ProviderInternalName, "in AugmentEntity", TraceCategory.Augmentation, ex);
                }
                finally
                {
                    this.Lock_Config.ExitReadLock();
                }
            });
        }

        /// <summary>
        /// Get group membership using UserPrincipal.GetAuthorizationGroups(), which works only with AD
        /// UserPrincipal.GetAuthorizationGroups() gets groups using Kerberos protocol transition (preferred way), and falls back to LDAP queries otherwise.
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="requestInfo"></param>
        /// <param name="groupAttribute"></param>
        /// <returns></returns>
        protected virtual List<SPClaim> GetGroupsFromADDirectory(DirectoryEntry directory, OperationContext requestInfo, ClaimTypeConfig groupAttribute)
        {
            List<SPClaim> groups = new List<SPClaim>();
            using (new SPMonitoredScope(String.Format("[{0}] Getting AD group membership of user {1} in {2}", ProviderInternalName, requestInfo.IncomingEntity.Value, directory.Path), 2000))
            {
                UserPrincipal adUser = null;
                try
                {
                    string directoryDomainName, directoryDomainFqdn;
                    OperationContext.GetDomainInformation(directory, out directoryDomainName, out directoryDomainFqdn);
                    Stopwatch stopWatch = new Stopwatch();
                    stopWatch.Start();

                    using (PrincipalContext principalContext = new PrincipalContext(ContextType.Domain, directoryDomainFqdn))
                    {
                        // https://github.com/Yvand/LDAPCP/issues/22
                        // UserPrincipal.FindByIdentity() doesn't support emails, so if IncomingEntity is an email, user needs to be retrieved in a different way
                        if (String.Equals(requestInfo.IncomingEntity.ClaimType, WIF4_5.ClaimTypes.Email, StringComparison.InvariantCultureIgnoreCase))
                        {
                            using (UserPrincipal userEmailPrincipal = new UserPrincipal(principalContext) { Enabled = true, EmailAddress = requestInfo.IncomingEntity.Value })
                            {
                                using (PrincipalSearcher userEmailSearcher = new PrincipalSearcher(userEmailPrincipal))
                                {
                                    adUser = userEmailSearcher.FindOne() as UserPrincipal;
                                }
                            }
                        }
                        else adUser = UserPrincipal.FindByIdentity(principalContext, requestInfo.IncomingEntity.Value);

                        if (adUser == null) return groups;

                        IEnumerable<Principal> ADGroups = adUser.GetAuthorizationGroups().Where(x => !String.IsNullOrEmpty(x.DistinguishedName));
                        stopWatch.Stop();

                        foreach (Principal group in ADGroups)
                        {
                            string groupDomainName, groupDomainFqdn;
                            OperationContext.GetDomainInformation(group.DistinguishedName, out groupDomainName, out groupDomainFqdn);
                            string claimValue = group.Name;
                            if (!String.IsNullOrEmpty(groupAttribute.ClaimValuePrefix) && groupAttribute.ClaimValuePrefix.Contains(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME))
                                claimValue = groupAttribute.ClaimValuePrefix.Replace(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME, groupDomainName) + group.Name;
                            else if (!String.IsNullOrEmpty(groupAttribute.ClaimValuePrefix) && groupAttribute.ClaimValuePrefix.Contains(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN))
                                claimValue = groupAttribute.ClaimValuePrefix.Replace(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN, groupDomainFqdn) + group.Name;
                            SPClaim claim = CreateClaim(groupAttribute.ClaimType, claimValue, groupAttribute.ClaimValueType, false);
                            groups.Add(claim);
                        }
                    }
                }
                catch (PrincipalOperationException ex)
                {
                    ClaimsProviderLogging.LogException(ProviderInternalName, String.Format("while getting AD group membership of user {0} in {1} using UserPrincipal.GetAuthorizationGroups(). This is likely due to a bug in .NET framework in UserPrincipal.GetAuthorizationGroups (as of v4.6.1), especially if user is member (directly or not) of a group either in a child domain that was migrated, or a group that has special (deny) permissions.", requestInfo.IncomingEntity.Value, directory.Path), TraceCategory.Augmentation, ex);
                    // In this case, fallback to LDAP method to get group membership.
                    return GetGroupsFromLDAPDirectory(directory, requestInfo, groupAttribute);
                }
                catch (PrincipalServerDownException ex)
                {
                    ClaimsProviderLogging.LogException(ProviderInternalName, String.Format("while getting AD group membership of user {0} in {1} using UserPrincipal.GetAuthorizationGroups(). Is this server an Active Directory server?", requestInfo.IncomingEntity.Value, directory.Path), TraceCategory.Augmentation, ex);
                }
                catch (Exception ex)
                {
                    ClaimsProviderLogging.LogException(ProviderInternalName, String.Format("while getting AD group membership of user {0} in {1} using UserPrincipal.GetAuthorizationGroups()", requestInfo.IncomingEntity.Value, directory.Path), TraceCategory.Augmentation, ex);
                }
                finally
                {
                    if (adUser != null) adUser.Dispose();
                }
            }
            return groups;
        }

        /// <summary>
        /// Get group membership with a LDAP query
        /// </summary>
        /// <param name="directory">LDAP server to query</param>
        /// <param name="requestInfo">Information about current context and operation</param>
        /// <param name="groupAttribute"></param>
        /// <returns></returns>
        protected virtual List<SPClaim> GetGroupsFromLDAPDirectory(DirectoryEntry directory, OperationContext requestInfo, ClaimTypeConfig groupAttribute)
        {
            List<SPClaim> groups = new List<SPClaim>();
            using (new SPMonitoredScope(String.Format("[{0}] Getting LDAP group membership of user {1} in {2}", ProviderInternalName, requestInfo.IncomingEntity.Value, directory.Path), 2000))
            {
                try
                {
                    string directoryDomainName, directoryDomainFqdn;
                    OperationContext.GetDomainInformation(directory, out directoryDomainName, out directoryDomainFqdn);
                    Stopwatch stopWatch = new Stopwatch();

                    using (DirectorySearcher searcher = new DirectorySearcher(directory))
                    {
                        searcher.ClientTimeout = new TimeSpan(0, 0, this.CurrentConfiguration.LDAPQueryTimeout); // Set the timeout of the query
                        searcher.Filter = string.Format("(&(ObjectClass={0})({1}={2}){3})", IdentityClaimTypeConfig.LDAPClass, IdentityClaimTypeConfig.LDAPAttribute, requestInfo.IncomingEntity.Value, IdentityClaimTypeConfig.AdditionalLDAPFilter);
                        searcher.PropertiesToLoad.Add("memberOf");
                        searcher.PropertiesToLoad.Add("uniquememberof");

                        stopWatch.Start();
                        SearchResult result = searcher.FindOne();
                        stopWatch.Stop();

                        if (result == null) return groups;  // user was not found in this directory

                        int propertyCount = result.Properties["memberOf"].Count;
                        var groupCollection = result.Properties["memberOf"];

                        if (propertyCount == 0)
                        {
                            propertyCount = result.Properties["uniquememberof"].Count;
                            groupCollection = result.Properties["uniquememberof"];
                        }

                        string groupDN;
                        for (int propertyCounter = 0; propertyCounter < propertyCount; propertyCounter++)
                        {
                            groupDN = (string)groupCollection[propertyCounter];
                            string claimValue = OperationContext.GetValueFromDistinguishedName(groupDN);
                            if (String.IsNullOrEmpty(claimValue)) continue;

                            string groupDomainName, groupDomainFqdn;
                            OperationContext.GetDomainInformation(groupDN, out groupDomainName, out groupDomainFqdn);
                            if (!String.IsNullOrEmpty(groupAttribute.ClaimValuePrefix) && groupAttribute.ClaimValuePrefix.Contains(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME))
                                claimValue = groupAttribute.ClaimValuePrefix.Replace(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME, groupDomainName) + claimValue;
                            else if (!String.IsNullOrEmpty(groupAttribute.ClaimValuePrefix) && groupAttribute.ClaimValuePrefix.Contains(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN))
                                claimValue = groupAttribute.ClaimValuePrefix.Replace(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN, groupDomainFqdn) + claimValue;
                            SPClaim claim = CreateClaim(groupAttribute.ClaimType, claimValue, groupAttribute.ClaimValueType, false);
                            groups.Add(claim);
                        }
                    }
                    ClaimsProviderLogging.Log(String.Format("[{0}] Domain {1} returned {2} groups for user {3}. Lookup took {4}ms on LDAP server '{5}'",
                        ProviderInternalName, directoryDomainFqdn, groups.Count, requestInfo.IncomingEntity.Value, stopWatch.ElapsedMilliseconds.ToString(), directory.Path),
                        TraceSeverity.Medium, EventSeverity.Information, TraceCategory.Augmentation);
                }
                catch (Exception ex)
                {
                    ClaimsProviderLogging.LogException(ProviderInternalName, String.Format("while getting LDAP group membership of user {0} in {1}. This is likely due to a bug in .NET framework in UserPrincipal.GetAuthorizationGroups (as of v4.6.1), especially if user is member (directly or not) of a group either in a child domain that was migrated, or a group that has special (deny) permissions.", requestInfo.IncomingEntity.Value, directory.Path), TraceCategory.Augmentation, ex);
                }
                finally
                {
                    if (directory != null) directory.Dispose();
                }
            }
            return groups;
        }

        /// <summary>
        /// Source: http://www.codeproject.com/Articles/18102/Howto-Almost-Everything-In-Active-Directory-via-C#38
        /// </summary>
        /// <param name="attributeName">memberof</param>
        /// <param name="objectDn"></param>
        /// <param name="valuesCollection"></param>
        /// <param name="recursive"></param>
        /// <returns></returns>
        public ArrayList AttributeValuesMultiString(string attributeName, string objectDn, ArrayList valuesCollection, bool recursive)
        {
            DirectoryEntry ent = new DirectoryEntry(objectDn);
            PropertyValueCollection ValueCollection = ent.Properties[attributeName];
            IEnumerator en = ValueCollection.GetEnumerator();
            while (en.MoveNext())
            {
                if (en.Current != null)
                {
                    if (!valuesCollection.Contains(en.Current.ToString()))
                    {
                        valuesCollection.Add(en.Current.ToString());
                        if (recursive)
                        {
                            AttributeValuesMultiString(attributeName, "LDAP://" + en.Current.ToString(), valuesCollection, true);
                        }
                    }
                }
            }
            ent.Close();
            ent.Dispose();
            return valuesCollection;
        }

        /// <summary>
        /// the type of SPClaimEntityTypes that this provider support, such as SPClaimEntityTypes.User or SPClaimEntityTypes.FormsRole
        /// </summary>
        /// <param name="entityTypes"></param>
        protected override void FillEntityTypes(List<string> entityTypes)
        {
            entityTypes.Add(SPClaimEntityTypes.User);
            entityTypes.Add(ClaimsProviderConstants.GroupClaimEntityType);
        }

        /// <summary>
        /// Populates the list of attributes in the left side of the people picker
        /// </summary>
        /// <param name="context">the current web application</param>
        /// <param name="entityTypes">the type of SPClaimEntityTypes we should return</param>
        /// <param name="hierarchyNodeID"></param>
        /// <param name="numberOfLevels"></param>
        /// <param name="hierarchy"></param>
        protected override void FillHierarchy(Uri context, string[] entityTypes, string hierarchyNodeID, int numberOfLevels, Microsoft.SharePoint.WebControls.SPProviderHierarchyTree hierarchy)
        {
            List<LDAPObjectType> aadEntityTypes = new List<LDAPObjectType>();
            if (entityTypes.Contains(SPClaimEntityTypes.User))
                aadEntityTypes.Add(LDAPObjectType.User);
            if (entityTypes.Contains(ClaimsProviderConstants.GroupClaimEntityType))
                aadEntityTypes.Add(LDAPObjectType.Group);

            SPSecurity.RunWithElevatedPrivileges(delegate ()
            {
                if (!Initialize(context, entityTypes))
                    return;

                this.Lock_Config.EnterReadLock();
                try
                {
                    if (hierarchyNodeID == null)
                    {
                        // Root level
                        foreach (var attribute in ProcessedClaimTypesList.Where(x => !x.UseMainClaimTypeOfDirectoryObject && aadEntityTypes.Contains(x.DirectoryObjectType)))
                        {
                            hierarchy.AddChild(
                                new Microsoft.SharePoint.WebControls.SPProviderHierarchyNode(
                                    _ProviderInternalName,
                                    attribute.ClaimTypeDisplayName,
                                    attribute.ClaimType,
                                    true));
                        }
                    }
                }
                catch (Exception ex)
                {
                    ClaimsProviderLogging.LogException(ProviderInternalName, "in FillHierarchy", TraceCategory.Claims_Picking, ex);
                }
                finally
                {
                    this.Lock_Config.ExitReadLock();
                }
            });
        }

        /// <summary>
        /// Override this method to change / remove permissions created by LDAPCP, or add new ones
        /// </summary>
        /// <param name="context"></param>
        /// <param name="entityTypes"></param>
        /// <param name="input"></param>
        /// <param name="resolved">List of permissions created by LDAPCP</param>
        protected virtual void FillPermissions(Uri context, string[] entityTypes, string input, ref List<PickerEntity> resolved)
        {
        }

        protected virtual string AddAttributeToFilter(ClaimTypeConfig attribute, string searchPattern)
        {
            string filter = String.Empty;
            string additionalFilter = String.Empty;

            if (this.CurrentConfiguration.FilterSecurityGroupsOnlyProp && String.Equals(attribute.LDAPClass, "group", StringComparison.OrdinalIgnoreCase))
                additionalFilter = LDAPFilterADSecurityGroupsOnly;

            if (!String.IsNullOrEmpty(attribute.AdditionalLDAPFilter))
                additionalFilter += attribute.AdditionalLDAPFilter;

            filter = String.Format(LDAPFilter, attribute.LDAPAttribute, searchPattern, attribute.LDAPClass, additionalFilter);
            return filter;
        }

        /// <summary>
        /// Create the SPClaim with proper trust name
        /// </summary>
        /// <param name="type">Claim type</param>
        /// <param name="value">Claim value</param>
        /// <param name="valueType">Claim valueType</param>
        /// <param name="inputHasKeyword">Did the original input contain a keyword?</param>
        /// <returns></returns>
        protected virtual SPClaim CreateClaim(string type, string value, string valueType, bool inputHasKeyword)
        {
            string claimValue = String.Empty;
            //var attr = ProcessedAttributes.Where(x => x.ClaimTypeProp == type).FirstOrDefault();
            var attr = ProcessedClaimTypesList.FirstOrDefault(x => String.Equals(x.ClaimType, type, StringComparison.InvariantCultureIgnoreCase));
            //if (inputHasKeyword && attr.DoNotAddPrefixIfInputHasKeywordProp)
            if ((!inputHasKeyword || !attr.DoNotAddClaimValuePrefixIfBypassLookup) &&
                !HasPrefixToken(attr.ClaimValuePrefix, ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME) &&
                !HasPrefixToken(attr.ClaimValuePrefix, ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN)
            )
                claimValue = attr.ClaimValuePrefix;

            claimValue += value;
            // SPClaimProvider.CreateClaim issues with SPOriginalIssuerType.ClaimProvider
            //return CreateClaim(type, claimValue, valueType);
            return new SPClaim(type, claimValue, valueType, IssuerName);
        }

        protected virtual PickerEntity CreatePickerEntityHelper(ConsolidatedResult result)
        {
            PickerEntity pe = CreatePickerEntity();
            SPClaim claim;
            string permissionValue = result.Value;
            string permissionClaimType = result.Attribute.ClaimType;
            bool isIdentityClaimType = false;

            if ((String.Equals(permissionClaimType, SPTrust.IdentityClaimTypeInformation.MappedClaimType, StringComparison.InvariantCultureIgnoreCase)
                || result.Attribute.UseMainClaimTypeOfDirectoryObject) && result.Attribute.LDAPClass == IdentityClaimTypeConfig.LDAPClass)
            {
                isIdentityClaimType = true;
            }

            if (result.Attribute.UseMainClaimTypeOfDirectoryObject && result.Attribute.LDAPClass != IdentityClaimTypeConfig.LDAPClass)
            {
                // Get reference attribute to use to create actual permission (claim type and its LDAPAttribute) from current result
                ClaimTypeConfig attribute = ProcessedClaimTypesList.FirstOrDefault(x => !x.UseMainClaimTypeOfDirectoryObject && x.LDAPClass == result.Attribute.LDAPClass);
                if (attribute != null)
                {
                    permissionClaimType = attribute.ClaimType;
                    result.Attribute.ClaimType = attribute.ClaimType;
                    result.Attribute.DirectoryObjectType = attribute.DirectoryObjectType;
                    result.Attribute.ClaimTypeDisplayName = attribute.ClaimTypeDisplayName;
                    permissionValue = result.LDAPResults[attribute.LDAPAttribute][0].ToString();    // Pick value of current result from actual LDAP attribute to use (which is not the LDAP attribute that matches input)
                    result.Attribute.LDAPAttributeToShowAsDisplayText = attribute.LDAPAttributeToShowAsDisplayText;
                    result.Attribute.ClaimValuePrefix = attribute.ClaimValuePrefix;
                    result.Attribute.PrefixToBypassLookup = attribute.PrefixToBypassLookup;
                }
            }

            if (result.Attribute.UseMainClaimTypeOfDirectoryObject && result.Attribute.LDAPClass == IdentityClaimTypeConfig.LDAPClass)
            {
                // This attribute is not directly linked to a claim type, so permission is created with identity claim type
                permissionClaimType = IdentityClaimTypeConfig.ClaimType;
                permissionValue = FormatPermissionValue(permissionClaimType, result.LDAPResults[IdentityClaimTypeConfig.LDAPAttribute][0].ToString(), result.DomainName, result.DomainFQDN, isIdentityClaimType, result);
                claim = CreateClaim(
                    permissionClaimType,
                    permissionValue,
                    IdentityClaimTypeConfig.ClaimValueType,
                    false);
                pe.EntityType = IdentityClaimTypeConfig.DirectoryObjectType == LDAPObjectType.User ? SPClaimEntityTypes.User : ClaimsProviderConstants.GroupClaimEntityType;
            }
            else
            {
                permissionValue = FormatPermissionValue(result.Attribute.ClaimType, permissionValue, result.DomainName, result.DomainFQDN, isIdentityClaimType, result);
                claim = CreateClaim(
                    permissionClaimType,
                    permissionValue,
                    result.Attribute.ClaimValueType,
                    false);
                pe.EntityType = result.Attribute.DirectoryObjectType == LDAPObjectType.User ? SPClaimEntityTypes.User : ClaimsProviderConstants.GroupClaimEntityType;
            }

            int nbMetadata = 0;
            // Populate metadata attributes of permission created
            // Change condition to fix bug http://ldapcp.codeplex.com/discussions/653087
            // We don't care about the claim entity type, it must be unique based on the LDAP class
            //foreach (var entityAttrib in MetadataAttributes.Where(x => x.ClaimEntityType == result.Attribute.ClaimEntityType))
            foreach (var entityAttrib in MetadataConfig.Where(x => String.Equals(x.LDAPClass, result.Attribute.LDAPClass, StringComparison.InvariantCultureIgnoreCase)))
            {
                // if there is actally a value in the LDAP result, then it can be set
                if (result.LDAPResults.Contains(entityAttrib.LDAPAttribute) && result.LDAPResults[entityAttrib.LDAPAttribute].Count > 0)
                {
                    pe.EntityData[entityAttrib.EntityDataKey] = result.LDAPResults[entityAttrib.LDAPAttribute][0].ToString();
                    nbMetadata++;
                    ClaimsProviderLogging.Log(String.Format("[{0}] Added metadata \"{1}\" with value \"{2}\" to permission", ProviderInternalName, entityAttrib.EntityDataKey, pe.EntityData[entityAttrib.EntityDataKey]), TraceSeverity.Verbose, EventSeverity.Information, TraceCategory.Claims_Picking);
                }
            }

            pe.Claim = claim;
            pe.IsResolved = true;
            pe.EntityGroupName = this.CurrentConfiguration.PickerEntityGroupNameProp;
            pe.Description = String.Format(
                EntityOnMouseOver,
                result.Attribute.LDAPAttribute,
                result.Value);

            pe.DisplayText = FormatPermissionDisplayText(permissionClaimType, permissionValue, isIdentityClaimType, result, pe);

            ClaimsProviderLogging.Log(String.Format("[{0}] Created permission: display text: \"{1}\", value: \"{2}\", claim type: \"{3}\", and filled with {4} metadata.", ProviderInternalName, pe.DisplayText, pe.Claim.Value, pe.Claim.ClaimType, nbMetadata.ToString()), TraceSeverity.Verbose, EventSeverity.Information, TraceCategory.Claims_Picking);
            return pe;
        }

        /// <summary>
        /// Override this method to customize value of permission created.
        /// </summary>
        /// <param name="claimType"></param>
        /// <param name="claimValue"></param>
        /// <param name="domainName"></param>
        /// <returns></returns>
        protected virtual string FormatPermissionValue(string claimType, string claimValue, string domainName, string domainFQDN, bool isIdentityClaimType, ConsolidatedResult result)
        {
            string value = claimValue;

            var attr = ProcessedClaimTypesList.FirstOrDefault(x => String.Equals(x.ClaimType, claimType, StringComparison.InvariantCultureIgnoreCase));
            if (HasPrefixToken(attr.ClaimValuePrefix, ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME))
                value = string.Format("{0}{1}", attr.ClaimValuePrefix.Replace(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME, domainName), value);

            if (HasPrefixToken(attr.ClaimValuePrefix, ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN))
                value = string.Format("{0}{1}", attr.ClaimValuePrefix.Replace(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN, domainFQDN), value);

            return value;
        }

        private bool HasPrefixToken(string prefix, string tokenToSearch)
        {
            return prefix != null && prefix.Contains(tokenToSearch);
        }

        /// <summary>
        /// Override this method to customize display text of permission created
        /// </summary>
        /// <param name="displayText"></param>
        /// <param name="claimType"></param>
        /// <param name="claimValue"></param>
        /// <param name="isIdentityClaim"></param>
        /// <param name="result"></param>
        /// <param name="entityInfo"></param>
        /// <returns>Display text of permission</returns>
        protected virtual string FormatPermissionDisplayText(string claimType, string claimValue, bool isIdentityClaimType, ConsolidatedResult result, PickerEntity entityInfo)
        {
            string permissionDisplayText = String.Empty;
            string valueDisplayedInPermission = String.Empty;
            bool displayLdapMatchForIdentityClaimType = false;
            string prefixToAdd = string.Empty;

            //if (result == null)
            //{
            //    // Permission created without actual lookup
            //    if (isIdentityClaimType) return claimValue;
            //    else return String.Format(PickerEntityDisplayText, claimTypeToResolve.ClaimTypeMappingName, claimValue);
            //}
            var attr = result.Attribute;
            if (HasPrefixToken(attr.ClaimValuePrefix, ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME))
                prefixToAdd = string.Format("{0}", attr.ClaimValuePrefix.Replace(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME, result.DomainName));

            if (HasPrefixToken(attr.ClaimValuePrefix, ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN))
                prefixToAdd = string.Format("{0}", attr.ClaimValuePrefix.Replace(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN, result.DomainFQDN));

            if (isIdentityClaimType) displayLdapMatchForIdentityClaimType = this.CurrentConfiguration.DisplayLdapMatchForIdentityClaimTypeProp;

            if (!String.IsNullOrEmpty(result.Attribute.LDAPAttributeToShowAsDisplayText) && result.LDAPResults.Contains(result.Attribute.LDAPAttributeToShowAsDisplayText))
            {   // AttributeHelper is set to use a specific LDAP attribute as display text of permission
                if (!isIdentityClaimType && result.Attribute.ShowClaimNameInDisplayText)
                    permissionDisplayText = "(" + result.Attribute.ClaimTypeDisplayName + ") ";
                permissionDisplayText += prefixToAdd;
                permissionDisplayText += valueDisplayedInPermission = result.LDAPResults[result.Attribute.LDAPAttributeToShowAsDisplayText][0].ToString();
            }
            else
            {   // AttributeHelper is set to use its actual LDAP attribute as display text of permission
                if (!isIdentityClaimType)
                {
                    valueDisplayedInPermission = claimValue.StartsWith(prefixToAdd) ? claimValue : prefixToAdd + claimValue;
                    if (result.Attribute.ShowClaimNameInDisplayText)
                    {
                        permissionDisplayText = String.Format(
                            EntityDisplayText,
                            result.Attribute.ClaimTypeDisplayName,
                            valueDisplayedInPermission);
                    }
                    else permissionDisplayText = valueDisplayedInPermission;
                }
                else
                {   // Always specifically use LDAP attribute of identity claim type
                    permissionDisplayText = prefixToAdd;
                    permissionDisplayText += valueDisplayedInPermission = result.LDAPResults[IdentityClaimTypeConfig.LDAPAttribute][0].ToString();
                }
            }

            // Check if LDAP value that actually resolved this result should be included in the display text of the permission
            if (displayLdapMatchForIdentityClaimType && result.LDAPResults.Contains(result.Attribute.LDAPAttribute)
                && !String.Equals(valueDisplayedInPermission, claimValue, StringComparison.InvariantCultureIgnoreCase))
            {
                permissionDisplayText += String.Format(" ({0})", claimValue);
            }

            return permissionDisplayText;
        }

        /// <summary>
        /// Create a PickerEntity of the input for the claim type specified in parameter
        /// </summary>
        /// <param name="input">Value of the permission</param>
        /// <param name="claimTypesToResolve">claim type of the permission</param>
        /// <param name="inputHasKeyword">Did the original input contain a keyword?</param>
        /// <returns></returns>
        protected virtual PickerEntity CreatePickerEntityForSpecificClaimType(string input, ClaimTypeConfig claimTypesToResolve, bool inputHasKeyword)
        {
            List<PickerEntity> entities = CreatePickerEntityForSpecificClaimTypes(
                input,
                new List<ClaimTypeConfig>()
                    {
                        claimTypesToResolve,
                    },
                inputHasKeyword);
            return entities == null ? null : entities.First();
        }

        /// <summary>
        /// Create a PickerEntity of the input for each claim type specified in parameter
        /// </summary>
        /// <param name="input">Value of the permission</param>
        /// <param name="claimTypesToResolve">claim types of the permission</param>
        /// <param name="inputHasKeyword">Did the original input contain a keyword?</param>
        /// <returns></returns>
        protected virtual List<PickerEntity> CreatePickerEntityForSpecificClaimTypes(string input, IEnumerable<ClaimTypeConfig> claimTypesToResolve, bool inputHasKeyword)
        {
            List<PickerEntity> entities = new List<PickerEntity>();
            foreach (var claimTypeToResolve in claimTypesToResolve)
            {
                PickerEntity pe = CreatePickerEntity();
                SPClaim claim = CreateClaim(claimTypeToResolve.ClaimType, input, claimTypeToResolve.ClaimValueType, inputHasKeyword);

                if (String.Equals(claim.ClaimType, SPTrust.IdentityClaimTypeInformation.MappedClaimType, StringComparison.InvariantCultureIgnoreCase))
                {
                    pe.DisplayText = input;
                }
                else
                {
                    pe.DisplayText = String.Format(
                        EntityDisplayText,
                        claimTypeToResolve.ClaimTypeDisplayName,
                        input);
                }

                pe.EntityType = claimTypeToResolve.DirectoryObjectType == LDAPObjectType.User ? SPClaimEntityTypes.User : ClaimsProviderConstants.GroupClaimEntityType;
                pe.Description = String.Format(
                    EntityOnMouseOver,
                    claimTypeToResolve.LDAPAttribute,
                    input);

                pe.Claim = claim;
                pe.IsResolved = true;
                pe.EntityGroupName = this.CurrentConfiguration.PickerEntityGroupNameProp;

                if (claimTypeToResolve.DirectoryObjectType == LDAPObjectType.User && !String.IsNullOrEmpty(claimTypeToResolve.EntityDataKey))
                {
                    pe.EntityData[claimTypeToResolve.EntityDataKey] = pe.Claim.Value;
                    ClaimsProviderLogging.Log(String.Format("[{0}] Added metadata \"{1}\" with value \"{2}\" to permission", ProviderInternalName, claimTypeToResolve.EntityDataKey, pe.EntityData[claimTypeToResolve.EntityDataKey]), TraceSeverity.Verbose, EventSeverity.Information, TraceCategory.Claims_Picking);
                }
                entities.Add(pe);
                ClaimsProviderLogging.Log(String.Format("[{0}] Created permission: display text: \"{1}\", value: \"{2}\", claim type: \"{3}\".", ProviderInternalName, pe.DisplayText, pe.Claim.Value, pe.Claim.ClaimType), TraceSeverity.Verbose, EventSeverity.Information, TraceCategory.Claims_Picking);
            }
            return entities.Count > 0 ? entities : null;
        }

        protected override void FillSchema(Microsoft.SharePoint.WebControls.SPProviderSchema schema)
        {
        }

        public override string Name { get { return ProviderInternalName; } }
        public override bool SupportsEntityInformation { get { return true; } }
        public override bool SupportsHierarchy { get { return true; } }
        public override bool SupportsResolve { get { return true; } }
        public override bool SupportsSearch { get { return true; } }
        public override bool SupportsUserKey { get { return true; } }

        /// <summary>
        /// Return the identity claim type
        /// </summary>
        /// <returns></returns>
        public override string GetClaimTypeForUserKey()
        {
            ClaimsProviderLogging.LogDebug(String.Format("[{0}] GetClaimTypeForUserKey called", ProviderInternalName));

            if (!Initialize(null, null))
                return null;

            this.Lock_Config.EnterReadLock();
            try
            {
                return IdentityClaimTypeConfig.ClaimType;
            }
            catch (Exception ex)
            {
                ClaimsProviderLogging.LogException(ProviderInternalName, "in GetClaimTypeForUserKey", TraceCategory.Rehydration, ex);
            }
            finally
            {
                this.Lock_Config.ExitReadLock();
            }
            return null;
        }

        /// <summary>
        /// Return the user key (SPClaim with identity claim type) from the incoming entity
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        protected override SPClaim GetUserKeyForEntity(SPClaim entity)
        {
            ClaimsProviderLogging.LogDebug(String.Format("[{0}] GetUserKeyForEntity called, incoming claim value: \"{1}\", claim type: \"{2}\", claim issuer: \"{3}\"", ProviderInternalName, entity.Value, entity.ClaimType, entity.OriginalIssuer));

            if (!Initialize(null, null))
                return null;

            // There are 2 scenarios:
            // 1: OriginalIssuer is "SecurityTokenService": Value looks like "05.t|yvanhost|yvand@yvanhost.local", claim type is "http://schemas.microsoft.com/sharepoint/2009/08/claims/userid" and it must be decoded properly
            // 2: OriginalIssuer is LDAPCP: in this case incoming entity is valid and returned as is
            if (String.Equals(entity.OriginalIssuer, IssuerName, StringComparison.InvariantCultureIgnoreCase))
                return entity;

            SPClaimProviderManager cpm = SPClaimProviderManager.Local;
            SPClaim curUser = SPClaimProviderManager.DecodeUserIdentifierClaim(entity);

            this.Lock_Config.EnterReadLock();
            try
            {
                ClaimsProviderLogging.Log(String.Format("[{0}] Return user key for user \"{1}\"", ProviderInternalName, entity.Value),
                    TraceSeverity.Verbose, EventSeverity.Information, TraceCategory.Rehydration);
                return CreateClaim(IdentityClaimTypeConfig.ClaimType, curUser.Value, curUser.ValueType);
            }
            catch (Exception ex)
            {
                ClaimsProviderLogging.LogException(ProviderInternalName, "in GetUserKeyForEntity", TraceCategory.Rehydration, ex);
            }
            finally
            {
                this.Lock_Config.ExitReadLock();
            }
            return null;
        }

        protected override void FillDefaultLocalizedDisplayName(System.Globalization.CultureInfo culture, out string localizedName)
        {
            if (SPTrust != null)
                localizedName = SPTrust.DisplayName;
            else
                base.FillDefaultLocalizedDisplayName(culture, out localizedName);
        }
    }

    public class ConsolidatedResult
    {
        public ClaimTypeConfig Attribute;
        public PickerEntity PickerEntity;
        public ResultPropertyCollection LDAPResults;
        public int nbMatch = 0;
        public string Value;
        public string DomainName;
        public string DomainFQDN;
        //public string DEBUG;
    }

    public class LDAPSearchResultWrapper
    {
        public SearchResult SearchResult;
        public string DomainName;
        public string DomainFQDN;
    }    

    public class ConsolidatedResultCollection : Collection<ConsolidatedResult>
    {
        /// <summary>
        /// Compare 2 results to not add duplicates
        /// they are identical if they have the same claim type and same value
        /// </summary>
        /// <param name="result">LDAP result to compare</param>
        /// <param name="attribute">AttributeHelper that matches result</param>
        /// <param name="compareWithDomain">if true, don't consider 2 results as identical if they don't are in same domain.</param>
        /// <returns></returns>
        public bool Contains(LDAPSearchResultWrapper result, ClaimTypeConfig attribute, bool compareWithDomain)
        {
            foreach (var item in base.Items)
            {
                if (item.Attribute.ClaimType != attribute.ClaimType)
                    continue;

                if (!item.LDAPResults.Contains(attribute.LDAPAttribute))
                    continue;

                // if compareWithDomain is true, don't consider 2 results as identical if they don't are in same domain
                // Using same bool to compare both DomainName and DomainFQDN causes scenario below to potentially generate duplicates:
                // result.DomainName == item.DomainName BUT result.DomainFQDN != item.DomainFQDN AND value of claim is created with DomainName token
                // If so, compareWithDomain will be true and test below will be true so duplicates won't be check, even though it would be possible. 
                // But this would be so unlikely that this scenario can be ignored
                if (compareWithDomain && (
                    !String.Equals(item.DomainName, result.DomainName, StringComparison.InvariantCultureIgnoreCase) ||
                    !String.Equals(item.DomainFQDN, result.DomainFQDN, StringComparison.InvariantCultureIgnoreCase)
                                         ))
                    continue;   // They don't are in same domain, so not identical, jump to next item

                if (String.Equals(item.LDAPResults[attribute.LDAPAttribute][0].ToString(), result.SearchResult.Properties[attribute.LDAPAttribute][0].ToString(), StringComparison.InvariantCultureIgnoreCase))
                {
                    item.nbMatch++;
                    return true;
                }
            }
            return false;
        }
    }
}
