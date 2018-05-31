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
        private long CurrentConfigurationVersion = 0;

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
                        ClaimsProviderLogging.Log($"[{ProviderInternalName}] Configuration '{PersistedObjectName}' was not found. Visit LDAPCP admin pages in central administration to create it.",
                            TraceSeverity.Unexpected, EventSeverity.Error, TraceCategory.Core);
                        // Run with default configuration, which creates a connection to connect to current AD domain
                        globalConfiguration = LDAPCPConfig.ReturnDefaultConfiguration();
                        refreshConfig = true;
                    }
                    else
                    {
                        ((LDAPCPConfig)globalConfiguration).CheckAndCleanPersistedObject();
                    }

                    if (globalConfiguration.ClaimTypes == null || globalConfiguration.ClaimTypes.Count == 0)
                    {
                        ClaimsProviderLogging.Log($"[{ProviderInternalName}] Configuration '{PersistedObjectName}' was found but collection ClaimTypes is null or empty. Visit LDAPCP admin pages in central administration to create it.",
                            TraceSeverity.Unexpected, EventSeverity.Error, TraceCategory.Core);
                        // Cannot continue 
                        success = false;
                    }

                    if (globalConfiguration.LDAPConnectionsProp == null || globalConfiguration.LDAPConnectionsProp.Count == 0)
                    {
                        ClaimsProviderLogging.Log($"[{ProviderInternalName}] Configuration '{PersistedObjectName}' was found but there is no LDAP connection registered. Visit LDAPCP admin pages in central administration to register one.",
                            TraceSeverity.Unexpected, EventSeverity.Error, TraceCategory.Core);
                        // Cannot continue 
                        success = false;
                    }

                    if (success)
                    {
                        if (this.CurrentConfigurationVersion == ((SPPersistedObject)globalConfiguration).Version)
                        {
                            ClaimsProviderLogging.Log($"[{ProviderInternalName}] Configuration '{PersistedObjectName}' was found, version {((SPPersistedObject)globalConfiguration).Version.ToString()}",
                                TraceSeverity.VerboseEx, EventSeverity.Information, TraceCategory.Core);
                        }
                        else
                        {
                            refreshConfig = true;
                            this.CurrentConfigurationVersion = ((SPPersistedObject)globalConfiguration).Version;
                            ClaimsProviderLogging.Log($"[{ProviderInternalName}] Configuration '{PersistedObjectName}' changed to version {((SPPersistedObject)globalConfiguration).Version.ToString()}, refreshing local copy",
                                TraceSeverity.Medium, EventSeverity.Information, TraceCategory.Core);
                        }
                    }

                    // ProcessedClaimTypesList can be null if:
                    // - 1st initialization
                    // - Initialized before but it failed. If so, try again to refresh config
                    if (this.ProcessedClaimTypesList == null) refreshConfig = true;
                }
                catch (Exception ex)
                {
                    success = false;
                    ClaimsProviderLogging.LogException(ProviderInternalName, "in Initialize", TraceCategory.Core, ex);
                }

                //refreshConfig = true;   // DEBUG
                if (!success) return success;
                if (!refreshConfig) return success;

                // 2ND PART: APPLY CONFIGURATION
                // Configuration needs to be refreshed, lock current thread in write mode
                Lock_Config.EnterWriteLock();
                try
                {
                    ClaimsProviderLogging.Log($"[{ProviderInternalName}] Refreshing local copy of configuration '{PersistedObjectName}'",
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
                    else if (!groupClaimTypeFound && claimTypeConfig.EntityType == DirectoryObjectType.Group)
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
                    if (claimTypeConfig.EntityType == DirectoryObjectType.User)
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
        /// Override this method to create read-only configuration that will not be persisted in configuration database, and that cannot be changed with admin pages or PowerShell.
        /// DO NOT Override this method if you want to store configuration in config DB, and be able to updated it with admin pages or PowerShel.
        /// For that, override property PersistedObjectName and set its name
        /// </summary>
        /// <param name="context"></param>
        /// <param name="entityTypes"></param>
        /// <param name="persistedObjectName"></param>
        /// <returns>Read-only configuration to use</returns>
        protected virtual ILDAPCPConfiguration GetConfiguration(Uri context, string[] entityTypes, string persistedObjectName)
        {
            return LDAPCPConfig.GetConfiguration(persistedObjectName);
        }

        /// <summary>
        /// [Deprecated] Override this method to customize the configuration of LDAPCP. Please override GetConfiguration instead.
        /// </summary>
        /// <param name="context">The context, as a URI</param>
        /// <param name="entityTypes">The EntityType entity types set to scope the search to</param>
        [Obsolete("SetCustomConfiguration is deprecated, please override GetConfiguration instead.")]
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
            // In intranet zone, when creating entity, LDAPCP will be called 2 times, but the 2nd time (from FillResolve (SPClaim)) the context will always be the URL of default zone
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
        /// <param name="providerInternalName"></param>
        /// <returns></returns>
        public static SPTrustedLoginProvider GetSPTrustAssociatedWithCP(string providerInternalName)
        {
            var lp = SPSecurityTokenServiceManager.Local.TrustedLoginProviders.Where(x => String.Equals(x.ClaimProviderName, providerInternalName, StringComparison.OrdinalIgnoreCase));

            if (lp != null && lp.Count() == 1)
                return lp.First();

            if (lp != null && lp.Count() > 1)
                ClaimsProviderLogging.Log($"[{providerInternalName}] Cannot continue because '{providerInternalName}' is set with multiple SPTrustedIdentityTokenIssuer", TraceSeverity.Unexpected, EventSeverity.Error, TraceCategory.Core);

            ClaimsProviderLogging.Log($"[{providerInternalName}] Cannot continue because '{providerInternalName}' is not set with any SPTrustedIdentityTokenIssuer.\r\nVisit {ClaimsProviderConstants.PUBLICSITEURL} for more information.", TraceSeverity.High, EventSeverity.Warning, TraceCategory.Core);
            return null;
        }

        /// <summary>
        /// PickerEntity is resolved (underlined) but claim must be resolved to provide again a PickerEntity so that SharePoint can actually create the entity
        /// </summary>
        /// <param name="context"></param>
        /// <param name="entityTypes"></param>
        /// <param name="resolveInput"></param>
        /// <param name="resolved"></param>
        protected override void FillResolve(Uri context, string[] entityTypes, SPClaim resolveInput, List<Microsoft.SharePoint.WebControls.PickerEntity> resolved)
        {
            //ClaimsProviderLogging.LogDebug(String.Format("[{0}] FillResolve(SPClaim) called, incoming claim value: \"{1}\", claim type: \"{2}\", claim issuer: \"{3}\"", ProviderInternalName, resolveInput.Value, resolveInput.ClaimType, resolveInput.OriginalIssuer));
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
                    OperationContext currentContext = new OperationContext(CurrentConfiguration, OperationType.Validation, ProcessedClaimTypesList, resolveInput.Value, resolveInput, context, entityTypes, null, Int32.MaxValue);
                    List<PickerEntity> entities = SearchOrValidate(currentContext);
                    if (entities?.Count == 1)
                    {
                        resolved.Add(entities[0]);
                        ClaimsProviderLogging.Log($"[{ProviderInternalName}] Validated entity: display text: '{entities[0].DisplayText}', claim value: '{entities[0].Claim.Value}', claim type: '{entities[0].Claim.ClaimType}'",
                            TraceSeverity.High, EventSeverity.Information, TraceCategory.Claims_Picking);
                    }
                    else
                    {
                        int entityCount = entities == null ? 0 : entities.Count;
                        ClaimsProviderLogging.Log($"[{ProviderInternalName}] Validation failed: found {entityCount.ToString()} entities instead of 1 for incoming claim with value '{currentContext.IncomingEntity.Value}' and type '{currentContext.IncomingEntity.ClaimType}'", TraceSeverity.Unexpected, EventSeverity.Error, TraceCategory.Claims_Picking);
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
                    OperationContext currentContext = new OperationContext(CurrentConfiguration, OperationType.Search, ProcessedClaimTypesList, resolveInput, null, context, entityTypes, null, Int32.MaxValue);
                    List<PickerEntity> entities = SearchOrValidate(currentContext);
                    FillEntities(context, entityTypes, resolveInput, ref entities);
                    if (entities == null || entities.Count == 0) return;
                    foreach (PickerEntity entity in entities)
                    {
                        resolved.Add(entity);
                        ClaimsProviderLogging.Log($"[{ProviderInternalName}] Added entity: display text: '{entity.DisplayText}', claim value: '{entity.Claim.Value}', claim type: '{entity.Claim.ClaimType}'",
                            TraceSeverity.Verbose, EventSeverity.Information, TraceCategory.Claims_Picking);
                    }
                    ClaimsProviderLogging.Log($"[{ProviderInternalName}] Returned {entities.Count} entities with input '{currentContext.Input}'",
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
                    OperationContext currentContext = new OperationContext(CurrentConfiguration, OperationType.Search, ProcessedClaimTypesList, searchPattern, null, context, entityTypes, hierarchyNodeID, maxCount);
                    List<PickerEntity> entities = SearchOrValidate(currentContext);
                    FillEntities(context, entityTypes, searchPattern, ref entities);
                    if (entities == null || entities.Count == 0) return;
                    SPProviderHierarchyNode matchNode = null;
                    foreach (PickerEntity entity in entities)
                    {
                        // Add current PickerEntity to the corresponding attribute in the hierarchy
                        if (searchTree.HasChild(entity.Claim.ClaimType))
                        {
                            matchNode = searchTree.Children.First(x => x.HierarchyNodeID == entity.Claim.ClaimType);
                        }
                        else
                        {
                            ClaimTypeConfig ctConfig = ProcessedClaimTypesList.FirstOrDefault(x =>
                                !x.UseMainClaimTypeOfDirectoryObject &&
                                String.Equals(x.ClaimType, entity.Claim.ClaimType, StringComparison.InvariantCultureIgnoreCase));

                            string nodeName = ctConfig != null ? ctConfig.ClaimTypeDisplayName : entity.Claim.ClaimType;
                            matchNode = new SPProviderHierarchyNode(_ProviderInternalName, nodeName, entity.Claim.ClaimType, true);
                            searchTree.AddChild(matchNode);
                        }
                        matchNode.AddEntity(entity);
                        ClaimsProviderLogging.Log($"[{ProviderInternalName}] Added entity: display text: '{entity.DisplayText}', claim value: '{entity.Claim.Value}', claim type: '{entity.Claim.ClaimType}'",
                            TraceSeverity.Verbose, EventSeverity.Information, TraceCategory.Claims_Picking);
                    }
                    ClaimsProviderLogging.Log($"[{ProviderInternalName}] Returned {entities.Count} entities from input '{currentContext.Input}'",
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
        /// Search or validate incoming input or entity
        /// </summary>
        /// <param name="currentContext">Information about current context and operation</param>
        /// <returns>Entities generated by AzureCP</returns>
        protected virtual List<PickerEntity> SearchOrValidate(OperationContext currentContext)
        {
            List<PickerEntity> entities = new List<PickerEntity>();
            try
            {
                if (this.CurrentConfiguration.BypassLDAPLookup)
                {
                    // Completely bypass LDAP lookp
                    entities = CreatePickerEntityForSpecificClaimTypes(
                        currentContext.Input,
                        currentContext.CurrentClaimTypeConfigList.Where(x => !x.UseMainClaimTypeOfDirectoryObject),
                        false);
                    ClaimsProviderLogging.Log($"[{ProviderInternalName}] Created {entities.Count} entity(ies) without contacting LDAP server(s) because LDAPCP property BypassLDAPLookup is set to true.",
                        TraceSeverity.Medium, EventSeverity.Information, TraceCategory.Claims_Picking);
                    return entities;
                }

                if (currentContext.OperationType == OperationType.Search)
                {
                    entities = SearchOrValidateInLDAP(currentContext);

                    // Check if input starts with a prefix configured on a ClaimTypeConfig. If so an entity should be returned using ClaimTypeConfig found
                    // ClaimTypeConfigEnsureUniquePrefixToBypassLookup ensures that collection cannot contain duplicates
                    ClaimTypeConfig ctConfigWithInputPrefixMatch = currentContext.CurrentClaimTypeConfigList.FirstOrDefault(x =>
                        !String.IsNullOrEmpty(x.PrefixToBypassLookup) &&
                        currentContext.Input.StartsWith(x.PrefixToBypassLookup, StringComparison.InvariantCultureIgnoreCase));
                    if (ctConfigWithInputPrefixMatch != null)
                    {
                        currentContext.Input = currentContext.Input.Substring(ctConfigWithInputPrefixMatch.PrefixToBypassLookup.Length);
                        if (String.IsNullOrEmpty(currentContext.Input))
                        {
                            // No value in the input after the prefix, return
                            return null;
                        }
                        PickerEntity entity = CreatePickerEntityForSpecificClaimType(
                            currentContext.Input,
                            ctConfigWithInputPrefixMatch,
                            true);
                        if (entity != null)
                        {
                            if (entities == null) entities = new List<PickerEntity>();
                            entities.Add(entity);
                            ClaimsProviderLogging.Log($"[{ProviderInternalName}] Created entity without contacting LDAP server(s) because input started with prefix '{ctConfigWithInputPrefixMatch.PrefixToBypassLookup}', which is configured for claim type '{ctConfigWithInputPrefixMatch.ClaimType}'. Claim value: '{entity.Claim.Value}', claim type: '{entity.Claim.ClaimType}'",
                                TraceSeverity.VerboseEx, EventSeverity.Information, TraceCategory.Claims_Picking);
                            //return entities;
                        }
                    }
                }
                else if (currentContext.OperationType == OperationType.Validation)
                {
                    entities = SearchOrValidateInLDAP(currentContext);
                    if (entities?.Count == 1) return entities;

                    if (!String.IsNullOrEmpty(currentContext.IncomingEntityClaimTypeConfig.PrefixToBypassLookup))
                    {
                        // At this stage, it is impossible to know if entity was originally created with the keyword that bypass query to Azure AD
                        // But it should be always validated since property PrefixToBypassLookup is set for current ClaimTypeConfig, so create entity manually
                        PickerEntity entity = CreatePickerEntityForSpecificClaimType(
                            currentContext.Input,
                            currentContext.IncomingEntityClaimTypeConfig,
                            currentContext.InputHasKeyword);
                        if (entity != null)
                        {
                            entities = new List<PickerEntity>(1) { entity };
                            ClaimsProviderLogging.Log($"[{ProviderInternalName}] Validated entity without contacting LDAP server(s) because its claim type ('{currentContext.IncomingEntityClaimTypeConfig.ClaimType}') has property 'PrefixToBypassLookup' set in AzureCPConfig.ClaimTypes. Claim value: '{entity.Claim.Value}', claim type: '{entity.Claim.ClaimType}'",
                                TraceSeverity.VerboseEx, EventSeverity.Information, TraceCategory.Claims_Picking);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ClaimsProviderLogging.LogException(ProviderInternalName, "in SearchOrValidate", TraceCategory.Claims_Picking, ex);
            }
            return entities;
        }

        /// <summary>
        /// Search or validate incoming input or entity with LDAP lookup
        /// </summary>
        /// <param name="currentContext">Information about current context and operation</param>
        /// <param name="entities"></param>
        /// <returns></returns>
        protected virtual List<PickerEntity> SearchOrValidateInLDAP(OperationContext currentContext)
        {
            LDAPConnection[] connections = GetLDAPServers(currentContext);
            if (connections == null || connections.Length == 0)
            {
                ClaimsProviderLogging.Log(String.Format("[{0}] No LDAP server is configured.", ProviderInternalName), TraceSeverity.Unexpected, EventSeverity.Error, TraceCategory.Configuration);
                return null;
            }

            BuildLDAPFilter(currentContext, connections);

            bool resultsfound = false;
            List<LDAPSearchResult> LDAPSearchResultWrappers = new List<LDAPSearchResult>();
            using (new SPMonitoredScope(String.Format("[{0}] Total time spent in all LDAP server(s)", ProviderInternalName), 1000))
            {
                resultsfound = QueryLDAPServers(connections, currentContext, ref LDAPSearchResultWrappers);
            }

            if (!resultsfound) return null;
            ConsolidatedResultCollection results = ProcessLdapResults(currentContext, ref LDAPSearchResultWrappers);
            if (results?.Count <= 0) return null;

            // There may be some extra work based on currentContext associated with the input claim type
            // Check to see if we have a prefix and have a domain token
            if (currentContext.OperationType == OperationType.Validation && currentContext.IncomingEntityClaimTypeConfig.ClaimValuePrefix != null)
            {
                // Extract just the domain from the input
                bool tokenFound = false;
                string domainOnly = String.Empty;
                if (currentContext.IncomingEntityClaimTypeConfig.ClaimValuePrefix.Contains(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME))
                {
                    tokenFound = true;
                    domainOnly = OperationContext.GetDomainFromFullAccountName(currentContext.IncomingEntity.Value);
                }
                else if (currentContext.IncomingEntityClaimTypeConfig.ClaimValuePrefix.Contains(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN))
                {
                    tokenFound = true;
                    string fqdn = OperationContext.GetDomainFromFullAccountName(currentContext.IncomingEntity.Value);
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

            List<PickerEntity> entities = new List<PickerEntity>();
            foreach (var result in results)
            {
                entities.Add(result.PickerEntity);
                //ClaimsProviderLogging.Log(String.Format("[{0}] Added entity created with LDAP lookup: claim value: \"{1}\", claim type: \"{2}\"", ProviderInternalName, result.PickerEntity.Claim.Value, result.PickerEntity.Claim.ClaimType),
                //    TraceSeverity.VerboseEx, EventSeverity.Information, TraceCategory.Claims_Picking);
            }
            return entities;
        }

        /// <summary>
        /// Processes LDAP results stored in LDAPSearchResultWrappers and returns result in parameter results
        /// </summary>
        /// <param name="currentContext">Information about current context and operation</param>
        /// <param name="LDAPSearchResults"></param>
        /// <returns></returns>
        protected virtual ConsolidatedResultCollection ProcessLdapResults(OperationContext currentContext, ref List<LDAPSearchResult> LDAPSearchResults)
        {
            ConsolidatedResultCollection results = new ConsolidatedResultCollection();
            ResultPropertyCollection LDAPResultProperties;
            IEnumerable<ClaimTypeConfig> ctConfigs = currentContext.CurrentClaimTypeConfigList;
            if (currentContext.ExactSearch) ctConfigs = currentContext.CurrentClaimTypeConfigList.Where(x => !x.UseMainClaimTypeOfDirectoryObject);

            foreach (LDAPSearchResult LDAPResult in LDAPSearchResults)
            {
                LDAPResultProperties = LDAPResult.SearchResult.Properties;
                // objectclass attribute should never be missing because it is explicitely requested in LDAP query
                if (!LDAPResultProperties.Contains(LDAPObjectClassName))
                {
                    ClaimsProviderLogging.Log($"[{ProviderInternalName}] Property '{LDAPObjectClassName}' is missing in LDAP result, this may be due to insufficient entities of the account connecting to LDAP server '{LDAPResult.DomainFQDN}'. Skipping result.", TraceSeverity.Unexpected, EventSeverity.Error, TraceCategory.LDAP_Lookup);
                    continue;
                }

                // Cast collection to be able to use StringComparer.InvariantCultureIgnoreCase for case insensitive search of ldap properties
                IEnumerable<string> LDAPResultPropertyNames = LDAPResultProperties.PropertyNames.Cast<string>();

                // Issue https://github.com/Yvand/LDAPCP/issues/16: If current result is a user, ensure LDAP attribute of identity ClaimTypeConfig exists in current LDAP result
                if (LDAPResultProperties[LDAPObjectClassName].Cast<string>().Contains(IdentityClaimTypeConfig.LDAPClass, StringComparer.InvariantCultureIgnoreCase))
                {
                    // This is a user: check if his identity LDAP attribute (e.g. mail or sAMAccountName) is present
                    if (!LDAPResultPropertyNames.Contains(IdentityClaimTypeConfig.LDAPAttribute, StringComparer.InvariantCultureIgnoreCase))
                    {
                        ClaimsProviderLogging.Log($"[{ProviderInternalName}] Ignoring a user because he doesn't have the LDAP attribute '{IdentityClaimTypeConfig.LDAPAttribute}'", TraceSeverity.VerboseEx, EventSeverity.Information, TraceCategory.LDAP_Lookup);
                        continue;
                    }
                }
                else
                {
                    // This is a group: check if the LDAP attribute used to create groups entities is present
                    // TODO: since groups can have multiple claim types, this check may not make sense
                    if (MainGroupClaimTypeConfig != null && !LDAPResultPropertyNames.Contains(MainGroupClaimTypeConfig.LDAPAttribute, StringComparer.InvariantCultureIgnoreCase))
                    {
                        ClaimsProviderLogging.Log($"[{ProviderInternalName}] Ignoring a group because it doesn't have the LDAP attribute '{MainGroupClaimTypeConfig.LDAPAttribute}'", TraceSeverity.VerboseEx, EventSeverity.Information, TraceCategory.LDAP_Lookup);
                        continue;
                    }
                }

                foreach (ClaimTypeConfig ctConfig in ctConfigs)
                {
                    // Check if LDAPClass of current ClaimTypeConfig matches the current LDAP result
                    if (!LDAPResultProperties[LDAPObjectClassName].Cast<string>().Contains(ctConfig.LDAPClass, StringComparer.InvariantCultureIgnoreCase)) continue;

                    // Check if current LDAP result contains LDAP attribute of current attribute
                    if (!LDAPResultPropertyNames.Contains(ctConfig.LDAPAttribute, StringComparer.InvariantCultureIgnoreCase)) continue;

                    // Get value with of current LDAP attribute
                    // TODO: investigate https://github.com/Yvand/LDAPCP/issues/43
                    string directoryObjectPropertyValue = LDAPResultProperties[LDAPResultPropertyNames.First(x => String.Equals(x, ctConfig.LDAPAttribute, StringComparison.InvariantCultureIgnoreCase))][0].ToString();

                    // Check if current LDAP attribute value matches the input
                    if (currentContext.ExactSearch)
                    {
                        if (!String.Equals(directoryObjectPropertyValue, currentContext.Input, StringComparison.InvariantCultureIgnoreCase)) continue;
                    }
                    else
                    {
                        if (this.CurrentConfiguration.AddWildcardAsPrefixOfInput)
                        {
                            if (directoryObjectPropertyValue.IndexOf(currentContext.Input, StringComparison.InvariantCultureIgnoreCase) != -1) continue;
                        }
                        else
                        {
                            if (!directoryObjectPropertyValue.StartsWith(currentContext.Input, StringComparison.InvariantCultureIgnoreCase)) continue;
                        }
                    }

                    // Check if current result (association of LDAP result + ClaimTypeConfig) is not already in results list
                    // Get ClaimTypeConfig to use to check if result is already present in the results list
                    ClaimTypeConfig ctConfigToUseForDuplicateCheck = ctConfig;
                    if (ctConfig.UseMainClaimTypeOfDirectoryObject)
                    {
                        if (ctConfig.EntityType == DirectoryObjectType.User)
                        {
                            if (String.Equals(ctConfig.LDAPClass, IdentityClaimTypeConfig.LDAPClass, StringComparison.InvariantCultureIgnoreCase))
                                ctConfigToUseForDuplicateCheck = IdentityClaimTypeConfig;
                            else continue;  // Current ClaimTypeConfig is a user but current LDAP result is not, skip
                        }
                        else
                        {
                            if (MainGroupClaimTypeConfig != null && String.Equals(ctConfig.LDAPClass, MainGroupClaimTypeConfig.LDAPClass, StringComparison.InvariantCultureIgnoreCase))
                                ctConfigToUseForDuplicateCheck = MainGroupClaimTypeConfig;
                            else continue;  // Current ClaimTypeConfig is a group but current LDAP result is not, skip
                        }
                    }

                    // When token domain is present, then ensure we do compare with the actual domain name
                    bool compareWithDomain = HasPrefixToken(ctConfig.ClaimValuePrefix, ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME) ? true : this.CurrentConfiguration.CompareResultsWithDomainNameProp;
                    if (!compareWithDomain) compareWithDomain = HasPrefixToken(ctConfig.ClaimValuePrefix, ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN) ? true : this.CurrentConfiguration.CompareResultsWithDomainNameProp;
                    if (results.Contains(LDAPResult, ctConfigToUseForDuplicateCheck, compareWithDomain))
                        continue;

                    results.Add(
                        new ConsolidatedResult
                        {
                            ClaimTypeConfig = ctConfig,
                            LDAPResults = LDAPResultProperties,
                            Value = directoryObjectPropertyValue,
                            DomainName = LDAPResult.DomainName,
                            DomainFQDN = LDAPResult.DomainFQDN,
                            //DEBUG = String.Format("LDAPAttribute: {0}, LDAPAttributeValue: {1}, AlwaysResolveAgainstIdentityClaim: {2}", attr.LDAPAttribute, LDAPResultProperties[attr.LDAPAttribute][0].ToString(), attr.AlwaysResolveAgainstIdentityClaim.ToString())
                        });
                }
            }
            ClaimsProviderLogging.Log(String.Format("[{0}] {1} entity(ies) to create after filtering", ProviderInternalName, results.Count), TraceSeverity.Medium, EventSeverity.Information, TraceCategory.LDAP_Lookup);
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
        /// <param name="currentContext">Information about current context and operation</param>
        /// <param name="ldapServers">List to be populated by this method</param>
        protected virtual void BuildLDAPFilter(OperationContext currentContext, LDAPConnection[] ldapServers)
        {
            // Build LDAP filter as documented in http://technet.microsoft.com/fr-fr/library/aa996205(v=EXCHG.65).aspx
            StringBuilder filter = new StringBuilder();
            if (this.CurrentConfiguration.FilterEnabledUsersOnlyProp) filter.Append(LDAPFilterEnabledUsersOnly);
            filter.Append("(| ");   // START OR

            string preferredFilterPattern;
            string input = currentContext.Input;
            if (currentContext.ExactSearch) preferredFilterPattern = input;
            else preferredFilterPattern = this.CurrentConfiguration.AddWildcardAsPrefixOfInput ? "*" + input + "*" : input + "*";

            foreach (var ctConfig in currentContext.CurrentClaimTypeConfigList)
            {
                if (ctConfig.SupportsWildcard)
                    filter.Append(AddAttributeToFilter(ctConfig, preferredFilterPattern));
                else
                    filter.Append(AddAttributeToFilter(ctConfig, input));
            }

            if (this.CurrentConfiguration.FilterEnabledUsersOnlyProp) filter.Append(")");
            filter.Append(")");     // END OR

            foreach (LDAPConnection ldapServer in ldapServers)
            {
                ldapServer.Filter = filter.ToString();
            }
        }

        /// <summary>
        /// Query LDAP servers in parallel
        /// </summary>
        /// <param name="LDAPServers">LDAP servers to query</param>
        /// <param name="requestInfo">Information about current context and operation</param>
        /// <param name="LDAPSearchResults">LDAP search results list to be populated by this method</param>
        /// <returns>true if a result was found</returns>
        protected bool QueryLDAPServers(LDAPConnection[] LDAPServers, OperationContext requestInfo, ref List<LDAPSearchResult> LDAPSearchResults)
        {
            if (LDAPServers == null || LDAPServers.Length == 0) return false;
            object lockResults = new object();
            List<LDAPSearchResult> results = new List<LDAPSearchResult>();
            Stopwatch globalStopWatch = new Stopwatch();
            globalStopWatch.Start();

            Parallel.ForEach(LDAPServers, LDAPServer =>
            {
                if (LDAPServer == null) return;
                if (String.IsNullOrEmpty(LDAPServer.Filter))
                {
                    ClaimsProviderLogging.Log(String.Format("[{0}] Skipping query on LDAP Server \"{1}\" because it doesn't have any filter, this usually indicates a problem in method GetLDAPFilter.", ProviderInternalName, LDAPServer.LDAPServer.Path), TraceSeverity.Unexpected, EventSeverity.Information, TraceCategory.LDAP_Lookup);
                    return;
                }
                DirectoryEntry directory = LDAPServer.LDAPServer;
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
                                            results.Add(new LDAPSearchResult()
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
        /// <param name="currentContext">Information about current context and operation</param>
        /// <returns>Array of LDAP servers to query</returns>
        protected virtual LDAPConnection[] GetLDAPServers(OperationContext currentContext)
        {
            if (this.CurrentConfiguration.LDAPConnectionsProp == null) return null;
            IEnumerable<LDAPConnection> ldapConnections = this.CurrentConfiguration.LDAPConnectionsProp;
            if (currentContext.OperationType == OperationType.Augmentation) ldapConnections = this.CurrentConfiguration.LDAPConnectionsProp.Where(x => x.AugmentationEnabled);
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
                ClaimsProviderLogging.Log($"[{ProviderInternalName}] Not trying to augment '{decodedEntity.Value}' because his OriginalIssuer is '{decodedEntity.OriginalIssuer}'.",
                    TraceSeverity.VerboseEx, EventSeverity.Information, TraceCategory.Augmentation);
                return;
            }

            SPSecurity.RunWithElevatedPrivileges(delegate ()
            {
                if (!Initialize(context, null))
                    return;

                this.Lock_Config.EnterReadLock();
                try
                {
                    if (!this.CurrentConfiguration.EnableAugmentation) return;
                    if (String.IsNullOrEmpty(this.CurrentConfiguration.ClaimTypeUsedForAugmentation))
                    {
                        ClaimsProviderLogging.Log($"[{ProviderInternalName}] Augmentation is enabled but no claim type is configured.", TraceSeverity.High, EventSeverity.Error, TraceCategory.Augmentation);
                        return;
                    }

                    IEnumerable<ClaimTypeConfig> allGroupsCTConfig = this.ProcessedClaimTypesList.Where(x => x.EntityType == DirectoryObjectType.Group && !x.UseMainClaimTypeOfDirectoryObject);
                    IEnumerable<ClaimTypeConfig> allGroupsExceptMainGroupCTConfig = allGroupsCTConfig.Where(x => !String.Equals(x.ClaimType, this.CurrentConfiguration.ClaimTypeUsedForAugmentation, StringComparison.InvariantCultureIgnoreCase));
                    ClaimTypeConfig mainGroupCTConfig = allGroupsCTConfig.FirstOrDefault(x => String.Equals(x.ClaimType, this.CurrentConfiguration.ClaimTypeUsedForAugmentation, StringComparison.InvariantCultureIgnoreCase));
                    if (mainGroupCTConfig == null)
                    {
                        ClaimsProviderLogging.Log($"[{ProviderInternalName}] Configuration for claim type '{this.CurrentConfiguration.ClaimTypeUsedForAugmentation}' cannot be found, please add it in claim types configuration list.",
                            TraceSeverity.High, EventSeverity.Error, TraceCategory.Augmentation);
                        return;
                    }

                    ClaimsProviderLogging.Log($"[{ProviderInternalName}] Starting augmentation for user '{decodedEntity.Value}'.", TraceSeverity.Verbose, EventSeverity.Information, TraceCategory.Augmentation);
                    OperationContext currentContext = new OperationContext(CurrentConfiguration, OperationType.Augmentation, ProcessedClaimTypesList, null, decodedEntity, context, null, null, Int32.MaxValue);
                    LDAPConnection[] connections = GetLDAPServers(currentContext);
                    if (connections == null || connections.Length == 0)
                    {
                        ClaimsProviderLogging.Log(String.Format("[{0}] No LDAP server is enabled for augmentation", ProviderInternalName), TraceSeverity.High, EventSeverity.Error, TraceCategory.Augmentation);
                        return;
                    }

                    Stopwatch timer = new Stopwatch();
                    timer.Start();
                    List<SPClaim> groups = new List<SPClaim>();
                    object lockResults = new object();
                    Parallel.ForEach(connections, coco =>
                    {
                        List<SPClaim> directoryGroups;
                        if (coco.GetGroupMembershipAsADDomain)
                        {
                            directoryGroups = GetGroupsFromADDirectory(coco.LDAPServer, currentContext, mainGroupCTConfig);
                            directoryGroups.AddRange(GetGroupsFromLDAPDirectory(coco.LDAPServer, currentContext, allGroupsCTConfig.Where(x => !String.Equals(x.ClaimType, this.CurrentConfiguration.ClaimTypeUsedForAugmentation, StringComparison.InvariantCultureIgnoreCase))));
                        }
                        else
                        {
                            directoryGroups = GetGroupsFromLDAPDirectory(coco.LDAPServer, currentContext, allGroupsCTConfig);
                        }

                        lock (lockResults)
                        {
                            groups.AddRange(directoryGroups);
                        }
                    });
                    timer.Stop();
                    ClaimsProviderLogging.Log(String.Format("[{0}] LDAP queries to get group membership on all servers completed in {1}ms",
                        ProviderInternalName, timer.ElapsedMilliseconds.ToString()),
                        TraceSeverity.Verbose, EventSeverity.Information, TraceCategory.Augmentation);

                    foreach (SPClaim group in groups)
                    {
                        claims.Add(group);
                        ClaimsProviderLogging.Log($"[{ProviderInternalName}] Added group '{group.Value}', claim type '{group.ClaimType}' to user '{currentContext.IncomingEntity.Value}'",
                            TraceSeverity.Verbose, EventSeverity.Information, TraceCategory.Augmentation);
                    }
                    if (groups.Count > 0)
                        ClaimsProviderLogging.Log(String.Format("[{0}] User '{1}' was augmented with {2} groups of claim type '{3}'", ProviderInternalName, currentContext.IncomingEntity.Value, groups.Count.ToString(), mainGroupCTConfig.ClaimType),
                            TraceSeverity.Medium, EventSeverity.Information, TraceCategory.Augmentation);
                    else
                        ClaimsProviderLogging.Log(String.Format("[{0}] No group found for user '{1}' during augmentation", ProviderInternalName, currentContext.IncomingEntity.Value),
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
        /// <param name="currentContext"></param>
        /// <param name="groupCTConfig"></param>
        /// <returns></returns>
        protected virtual List<SPClaim> GetGroupsFromADDirectory(DirectoryEntry directory, OperationContext currentContext, ClaimTypeConfig groupCTConfig)
        {
            List<SPClaim> groups = new List<SPClaim>();
            using (new SPMonitoredScope(String.Format("[{0}] Getting AD group membership of user {1} in {2}", ProviderInternalName, currentContext.IncomingEntity.Value, directory.Path), 2000))
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
                        if (String.Equals(currentContext.IncomingEntity.ClaimType, WIF4_5.ClaimTypes.Email, StringComparison.InvariantCultureIgnoreCase))
                        {
                            using (UserPrincipal userEmailPrincipal = new UserPrincipal(principalContext) { Enabled = true, EmailAddress = currentContext.IncomingEntity.Value })
                            {
                                using (PrincipalSearcher userEmailSearcher = new PrincipalSearcher(userEmailPrincipal))
                                {
                                    adUser = userEmailSearcher.FindOne() as UserPrincipal;
                                }
                            }
                        }
                        else adUser = UserPrincipal.FindByIdentity(principalContext, currentContext.IncomingEntity.Value);

                        if (adUser == null) return groups;

                        IEnumerable<Principal> ADGroups = adUser.GetAuthorizationGroups().Where(x => !String.IsNullOrEmpty(x.DistinguishedName));
                        stopWatch.Stop();

                        foreach (Principal group in ADGroups)
                        {
                            string groupDomainName, groupDomainFqdn;
                            OperationContext.GetDomainInformation(group.DistinguishedName, out groupDomainName, out groupDomainFqdn);
                            string claimValue = group.Name;
                            if (!String.IsNullOrEmpty(groupCTConfig.ClaimValuePrefix) && groupCTConfig.ClaimValuePrefix.Contains(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME))
                                claimValue = groupCTConfig.ClaimValuePrefix.Replace(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME, groupDomainName) + group.Name;
                            else if (!String.IsNullOrEmpty(groupCTConfig.ClaimValuePrefix) && groupCTConfig.ClaimValuePrefix.Contains(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN))
                                claimValue = groupCTConfig.ClaimValuePrefix.Replace(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN, groupDomainFqdn) + group.Name;
                            SPClaim claim = CreateClaim(groupCTConfig.ClaimType, claimValue, groupCTConfig.ClaimValueType, false);
                            groups.Add(claim);
                        }
                    }
                }
                catch (PrincipalOperationException ex)
                {
                    ClaimsProviderLogging.LogException(ProviderInternalName, String.Format("while getting AD group membership of user {0} in {1} using UserPrincipal.GetAuthorizationGroups(). This is likely due to a bug in .NET framework in UserPrincipal.GetAuthorizationGroups (as of v4.6.1), especially if user is member (directly or not) of a group either in a child domain that was migrated, or a group that has special (deny) entities.", currentContext.IncomingEntity.Value, directory.Path), TraceCategory.Augmentation, ex);
                    // In this case, fallback to LDAP method to get group membership.
                    return GetGroupsFromLDAPDirectory(directory, currentContext, new List<ClaimTypeConfig>(1) { groupCTConfig });
                }
                catch (PrincipalServerDownException ex)
                {
                    ClaimsProviderLogging.LogException(ProviderInternalName, String.Format("while getting AD group membership of user {0} in {1} using UserPrincipal.GetAuthorizationGroups(). Is this server an Active Directory server?", currentContext.IncomingEntity.Value, directory.Path), TraceCategory.Augmentation, ex);
                }
                catch (Exception ex)
                {
                    ClaimsProviderLogging.LogException(ProviderInternalName, String.Format("while getting AD group membership of user {0} in {1} using UserPrincipal.GetAuthorizationGroups()", currentContext.IncomingEntity.Value, directory.Path), TraceCategory.Augmentation, ex);
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
        /// <param name="currentContext">Information about current context and operation</param>
        /// <param name="groupsCTConfig"></param>
        /// <returns></returns>
        protected virtual List<SPClaim> GetGroupsFromLDAPDirectory(DirectoryEntry directory, OperationContext currentContext, IEnumerable<ClaimTypeConfig> groupsCTConfig)
        {
            List<SPClaim> groups = new List<SPClaim>();
            if (groupsCTConfig == null) return groups;
            using (new SPMonitoredScope(String.Format("[{0}] Getting LDAP group membership of user {1} in {2}", ProviderInternalName, currentContext.IncomingEntity.Value, directory.Path), 2000))
            {
                try
                {
                    string directoryDomainName, directoryDomainFqdn;
                    OperationContext.GetDomainInformation(directory, out directoryDomainName, out directoryDomainFqdn);
                    Stopwatch stopWatch = new Stopwatch();

                    using (DirectorySearcher searcher = new DirectorySearcher(directory))
                    {
                        searcher.ClientTimeout = new TimeSpan(0, 0, this.CurrentConfiguration.LDAPQueryTimeout); // Set the timeout of the query
                        searcher.Filter = string.Format("(&(ObjectClass={0})({1}={2}){3})", IdentityClaimTypeConfig.LDAPClass, IdentityClaimTypeConfig.LDAPAttribute, currentContext.IncomingEntity.Value, IdentityClaimTypeConfig.AdditionalLDAPFilter);
                        searcher.PropertiesToLoad.Add("memberOf");
                        searcher.PropertiesToLoad.Add("uniquememberof");
                        foreach (ClaimTypeConfig groupCTConfig in groupsCTConfig)
                        {
                            searcher.PropertiesToLoad.Add(groupCTConfig.LDAPAttribute);
                        }

                        stopWatch.Start();
                        SearchResult result = searcher.FindOne();
                        stopWatch.Stop();

                        if (result == null) return groups;  // user was not found in this directory

                        foreach (ClaimTypeConfig groupCTConfig in groupsCTConfig)
                        {
                            int propertyCount = 0;
                            ResultPropertyValueCollection groupValues = null;
                            bool valueIsDistinguishedNameFormat;
                            if (groupCTConfig.ClaimType == MainGroupClaimTypeConfig.ClaimType)
                            {
                                valueIsDistinguishedNameFormat = true;
                                if (result.Properties.Contains("memberOf"))
                                {
                                    propertyCount = result.Properties["memberOf"].Count;
                                    groupValues = result.Properties["memberOf"];
                                }

                                if (propertyCount == 0 && result.Properties.Contains("uniquememberof"))
                                {
                                    propertyCount = result.Properties["uniquememberof"].Count;
                                    groupValues = result.Properties["uniquememberof"];
                                }
                            }
                            else
                            {
                                valueIsDistinguishedNameFormat = false;
                                if (result.Properties.Contains(groupCTConfig.LDAPAttribute))
                                {
                                    propertyCount = result.Properties[groupCTConfig.LDAPAttribute].Count;
                                    groupValues = result.Properties[groupCTConfig.LDAPAttribute];
                                }
                            }

                            string value;
                            for (int propertyCounter = 0; propertyCounter < propertyCount; propertyCounter++)
                            {
                                value = groupValues[propertyCounter].ToString();
                                string claimValue;
                                if (valueIsDistinguishedNameFormat)
                                {
                                    claimValue = OperationContext.GetValueFromDistinguishedName(value);
                                    if (String.IsNullOrEmpty(claimValue)) continue;

                                    string groupDomainName, groupDomainFqdn;
                                    OperationContext.GetDomainInformation(value, out groupDomainName, out groupDomainFqdn);
                                    if (!String.IsNullOrEmpty(groupCTConfig.ClaimValuePrefix) && groupCTConfig.ClaimValuePrefix.Contains(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME))
                                        claimValue = groupCTConfig.ClaimValuePrefix.Replace(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME, groupDomainName) + claimValue;
                                    else if (!String.IsNullOrEmpty(groupCTConfig.ClaimValuePrefix) && groupCTConfig.ClaimValuePrefix.Contains(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN))
                                        claimValue = groupCTConfig.ClaimValuePrefix.Replace(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN, groupDomainFqdn) + claimValue;
                                }
                                else
                                {
                                    claimValue = value;
                                }

                                SPClaim claim = CreateClaim(groupCTConfig.ClaimType, claimValue, groupCTConfig.ClaimValueType, false);
                                groups.Add(claim);
                            }
                        }
                    }
                    ClaimsProviderLogging.Log(String.Format("[{0}] Domain {1} returned {2} groups for user {3}. Lookup took {4}ms on LDAP server '{5}'",
                        ProviderInternalName, directoryDomainFqdn, groups.Count, currentContext.IncomingEntity.Value, stopWatch.ElapsedMilliseconds.ToString(), directory.Path),
                        TraceSeverity.Medium, EventSeverity.Information, TraceCategory.Augmentation);
                }
                catch (Exception ex)
                {
                    ClaimsProviderLogging.LogException(ProviderInternalName, String.Format("while getting LDAP group membership of user {0} in {1}.", currentContext.IncomingEntity.Value, directory.Path), TraceCategory.Augmentation, ex);
                }
                finally
                {
                    if (directory != null) directory.Dispose();
                }
            }
            return groups;
        }

        ///// <summary>
        ///// Source: http://www.codeproject.com/Articles/18102/Howto-Almost-Everything-In-Active-Directory-via-C#38
        ///// </summary>
        ///// <param name="attributeName">memberof</param>
        ///// <param name="objectDn"></param>
        ///// <param name="valuesCollection"></param>
        ///// <param name="recursive"></param>
        ///// <returns></returns>
        //public ArrayList AttributeValuesMultiString(string attributeName, string objectDn, ArrayList valuesCollection, bool recursive)
        //{
        //    DirectoryEntry ent = new DirectoryEntry(objectDn);
        //    PropertyValueCollection ValueCollection = ent.Properties[attributeName];
        //    IEnumerator en = ValueCollection.GetEnumerator();
        //    while (en.MoveNext())
        //    {
        //        if (en.Current != null)
        //        {
        //            if (!valuesCollection.Contains(en.Current.ToString()))
        //            {
        //                valuesCollection.Add(en.Current.ToString());
        //                if (recursive)
        //                {
        //                    AttributeValuesMultiString(attributeName, "LDAP://" + en.Current.ToString(), valuesCollection, true);
        //                }
        //            }
        //        }
        //    }
        //    ent.Close();
        //    ent.Dispose();
        //    return valuesCollection;
        //}

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
            List<DirectoryObjectType> aadEntityTypes = new List<DirectoryObjectType>();
            if (entityTypes.Contains(SPClaimEntityTypes.User))
                aadEntityTypes.Add(DirectoryObjectType.User);
            if (entityTypes.Contains(ClaimsProviderConstants.GroupClaimEntityType))
                aadEntityTypes.Add(DirectoryObjectType.Group);

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
                        foreach (var attribute in ProcessedClaimTypesList.Where(x => !x.UseMainClaimTypeOfDirectoryObject && aadEntityTypes.Contains(x.EntityType)))
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
        /// Override this method to change / remove entities created by LDAPCP, or add new ones
        /// </summary>
        /// <param name="context"></param>
        /// <param name="entityTypes"></param>
        /// <param name="input"></param>
        /// <param name="resolved">List of entities created by LDAPCP</param>
        protected virtual void FillEntities(Uri context, string[] entityTypes, string input, ref List<PickerEntity> resolved)
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
            string permissionClaimType = result.ClaimTypeConfig.ClaimType;
            bool isIdentityClaimType = false;

            if ((String.Equals(permissionClaimType, SPTrust.IdentityClaimTypeInformation.MappedClaimType, StringComparison.InvariantCultureIgnoreCase)
                || result.ClaimTypeConfig.UseMainClaimTypeOfDirectoryObject) && result.ClaimTypeConfig.LDAPClass == IdentityClaimTypeConfig.LDAPClass)
            {
                isIdentityClaimType = true;
            }

            if (result.ClaimTypeConfig.UseMainClaimTypeOfDirectoryObject && result.ClaimTypeConfig.LDAPClass != IdentityClaimTypeConfig.LDAPClass)
            {
                // Get reference attribute to use to create actual entity (claim type and its LDAPAttribute) from current result
                ClaimTypeConfig attribute = ProcessedClaimTypesList.FirstOrDefault(x => !x.UseMainClaimTypeOfDirectoryObject && x.LDAPClass == result.ClaimTypeConfig.LDAPClass);
                if (attribute != null)
                {
                    permissionClaimType = attribute.ClaimType;
                    result.ClaimTypeConfig.ClaimType = attribute.ClaimType;
                    result.ClaimTypeConfig.EntityType = attribute.EntityType;
                    result.ClaimTypeConfig.ClaimTypeDisplayName = attribute.ClaimTypeDisplayName;
                    permissionValue = result.LDAPResults[attribute.LDAPAttribute][0].ToString();    // Pick value of current result from actual LDAP attribute to use (which is not the LDAP attribute that matches input)
                    result.ClaimTypeConfig.LDAPAttributeToShowAsDisplayText = attribute.LDAPAttributeToShowAsDisplayText;
                    result.ClaimTypeConfig.ClaimValuePrefix = attribute.ClaimValuePrefix;
                    result.ClaimTypeConfig.PrefixToBypassLookup = attribute.PrefixToBypassLookup;
                }
            }

            if (result.ClaimTypeConfig.UseMainClaimTypeOfDirectoryObject && result.ClaimTypeConfig.LDAPClass == IdentityClaimTypeConfig.LDAPClass)
            {
                // This attribute is not directly linked to a claim type, so entity is created with identity claim type
                permissionClaimType = IdentityClaimTypeConfig.ClaimType;
                permissionValue = FormatPermissionValue(permissionClaimType, result.LDAPResults[IdentityClaimTypeConfig.LDAPAttribute][0].ToString(), result.DomainName, result.DomainFQDN, isIdentityClaimType, result);
                claim = CreateClaim(
                    permissionClaimType,
                    permissionValue,
                    IdentityClaimTypeConfig.ClaimValueType,
                    false);
                pe.EntityType = IdentityClaimTypeConfig.EntityType == DirectoryObjectType.User ? SPClaimEntityTypes.User : ClaimsProviderConstants.GroupClaimEntityType;
            }
            else
            {
                permissionValue = FormatPermissionValue(result.ClaimTypeConfig.ClaimType, permissionValue, result.DomainName, result.DomainFQDN, isIdentityClaimType, result);
                claim = CreateClaim(
                    permissionClaimType,
                    permissionValue,
                    result.ClaimTypeConfig.ClaimValueType,
                    false);
                pe.EntityType = result.ClaimTypeConfig.EntityType == DirectoryObjectType.User ? SPClaimEntityTypes.User : ClaimsProviderConstants.GroupClaimEntityType;
            }

            int nbMetadata = 0;
            // Populate metadata of new PickerEntity
            // Change condition to fix bug http://ldapcp.codeplex.com/discussions/653087: only rely on the LDAP class
            foreach (ClaimTypeConfig ctConfig in MetadataConfig.Where(x => String.Equals(x.LDAPClass, result.ClaimTypeConfig.LDAPClass, StringComparison.InvariantCultureIgnoreCase)))
            {
                // if there is actally a value in the LDAP result, then it can be set
                if (result.LDAPResults.Contains(ctConfig.LDAPAttribute) && result.LDAPResults[ctConfig.LDAPAttribute].Count > 0)
                {
                    pe.EntityData[ctConfig.EntityDataKey] = result.LDAPResults[ctConfig.LDAPAttribute][0].ToString();
                    nbMetadata++;
                    ClaimsProviderLogging.Log($"[{ProviderInternalName}] Set metadata '{ctConfig.EntityDataKey}' of new entity to '{pe.EntityData[ctConfig.EntityDataKey]}'", TraceSeverity.VerboseEx, EventSeverity.Information, TraceCategory.Claims_Picking);
                }
            }

            pe.Claim = claim;
            pe.IsResolved = true;
            pe.EntityGroupName = this.CurrentConfiguration.PickerEntityGroupNameProp;
            pe.Description = String.Format(
                EntityOnMouseOver,
                result.ClaimTypeConfig.LDAPAttribute,
                result.Value);

            pe.DisplayText = FormatPermissionDisplayText(pe, isIdentityClaimType, result);

            ClaimsProviderLogging.Log($"[{ProviderInternalName}] Created entity: display text: '{pe.DisplayText}', value: '{pe.Claim.Value}', claim type: '{pe.Claim.ClaimType}', and filled with {nbMetadata.ToString()} metadata.", TraceSeverity.VerboseEx, EventSeverity.Information, TraceCategory.Claims_Picking);
            return pe;
        }

        /// <summary>
        /// Override this method to customize value of entity created.
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
        /// Override this method to customize display text of entity created
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="isIdentityClaimType"></param>
        /// <param name="result"></param>
        /// <returns>Display text of entity</returns>
        protected virtual string FormatPermissionDisplayText(PickerEntity entity, bool isIdentityClaimType, ConsolidatedResult result)
        {
            string entityDisplayText = this.CurrentConfiguration.EntityDisplayTextPrefix;
            string claimValue = entity.Claim.Value;
            string valueDisplayedInPermission = String.Empty;
            bool displayLdapMatchForIdentityClaimType = false;
            string prefixToAdd = string.Empty;

            if (result.LDAPResults == null)
            {
                // Result does not come from a LDAP server, it was created manually
                if (isIdentityClaimType) entityDisplayText += claimValue;
                else entityDisplayText += String.Format(EntityDisplayText, result.ClaimTypeConfig.ClaimTypeDisplayName, claimValue);
            }
            else
            {
                if (HasPrefixToken(result.ClaimTypeConfig.ClaimValuePrefix, ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME))
                    prefixToAdd = string.Format("{0}", result.ClaimTypeConfig.ClaimValuePrefix.Replace(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME, result.DomainName));

                if (HasPrefixToken(result.ClaimTypeConfig.ClaimValuePrefix, ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN))
                    prefixToAdd = string.Format("{0}", result.ClaimTypeConfig.ClaimValuePrefix.Replace(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN, result.DomainFQDN));

                if (isIdentityClaimType) displayLdapMatchForIdentityClaimType = this.CurrentConfiguration.DisplayLdapMatchForIdentityClaimTypeProp;

                if (!String.IsNullOrEmpty(result.ClaimTypeConfig.LDAPAttributeToShowAsDisplayText) && result.LDAPResults.Contains(result.ClaimTypeConfig.LDAPAttributeToShowAsDisplayText))
                {   // AttributeHelper is set to use a specific LDAP attribute as display text of entity
                    if (!isIdentityClaimType && result.ClaimTypeConfig.ShowClaimNameInDisplayText)
                        entityDisplayText += "(" + result.ClaimTypeConfig.ClaimTypeDisplayName + ") ";
                    entityDisplayText += prefixToAdd;
                    entityDisplayText += valueDisplayedInPermission = result.LDAPResults[result.ClaimTypeConfig.LDAPAttributeToShowAsDisplayText][0].ToString();
                }
                else
                {   // AttributeHelper is set to use its actual LDAP attribute as display text of entity
                    if (!isIdentityClaimType)
                    {
                        valueDisplayedInPermission = claimValue.StartsWith(prefixToAdd) ? claimValue : prefixToAdd + claimValue;
                        if (result.ClaimTypeConfig.ShowClaimNameInDisplayText)
                        {
                            entityDisplayText += String.Format(
                                EntityDisplayText,
                                result.ClaimTypeConfig.ClaimTypeDisplayName,
                                valueDisplayedInPermission);
                        }
                        else entityDisplayText = valueDisplayedInPermission;
                    }
                    else
                    {   // Always specifically use LDAP attribute of identity claim type
                        entityDisplayText += prefixToAdd;
                        entityDisplayText += valueDisplayedInPermission = result.LDAPResults[IdentityClaimTypeConfig.LDAPAttribute][0].ToString();
                    }
                }

                // Check if LDAP value that actually resolved this result should be included in the display text of the entity
                if (displayLdapMatchForIdentityClaimType && result.LDAPResults.Contains(result.ClaimTypeConfig.LDAPAttribute)
                    && !String.Equals(valueDisplayedInPermission, claimValue, StringComparison.InvariantCultureIgnoreCase))
                {
                    entityDisplayText += String.Format(" ({0})", claimValue);
                }
            }
            return entityDisplayText;
        }

        /// <summary>
        /// Create a PickerEntity of the input for the claim type specified in parameter
        /// </summary>
        /// <param name="input">Value of the entity</param>
        /// <param name="ctConfig">claim type of the entity</param>
        /// <param name="inputHasKeyword">Did the original input contain a keyword?</param>
        /// <returns></returns>
        protected virtual PickerEntity CreatePickerEntityForSpecificClaimType(string input, ClaimTypeConfig ctConfig, bool inputHasKeyword)
        {
            List<PickerEntity> entities = CreatePickerEntityForSpecificClaimTypes(
                input,
                new List<ClaimTypeConfig>()
                    {
                        ctConfig,
                    },
                inputHasKeyword);
            return entities == null ? null : entities.First();
        }

        /// <summary>
        /// Create a PickerEntity of the input for each claim type specified in parameter
        /// </summary>
        /// <param name="input">Value of the entity</param>
        /// <param name="ctConfigs">claim types of the entity</param>
        /// <param name="inputHasKeyword">Did the original input contain a keyword?</param>
        /// <returns></returns>
        protected virtual List<PickerEntity> CreatePickerEntityForSpecificClaimTypes(string input, IEnumerable<ClaimTypeConfig> ctConfigs, bool inputHasKeyword)
        {
            List<PickerEntity> entities = new List<PickerEntity>();
            foreach (ClaimTypeConfig ctConfig in ctConfigs)
            {
                SPClaim claim = CreateClaim(ctConfig.ClaimType, input, ctConfig.ClaimValueType, inputHasKeyword);
                PickerEntity pe = CreatePickerEntity();
                pe.Claim = claim;
                pe.IsResolved = true;
                pe.EntityType = ctConfig.EntityType == DirectoryObjectType.User ? SPClaimEntityTypes.User : ClaimsProviderConstants.GroupClaimEntityType;
                pe.EntityGroupName = this.CurrentConfiguration.PickerEntityGroupNameProp;
                pe.Description = String.Format(EntityOnMouseOver, ctConfig.LDAPAttribute, input);

                if (!String.IsNullOrEmpty(ctConfig.EntityDataKey))
                {
                    pe.EntityData[ctConfig.EntityDataKey] = pe.Claim.Value;
                    ClaimsProviderLogging.Log($"[{ProviderInternalName}] Set metadata '{ctConfig.EntityDataKey}' of new entity to '{pe.EntityData[ctConfig.EntityDataKey]}'", TraceSeverity.VerboseEx, EventSeverity.Information, TraceCategory.Claims_Picking);
                }

                ConsolidatedResult result = new ConsolidatedResult();
                result.ClaimTypeConfig = ctConfig;
                result.Value = input;
                bool isIdentityClaimType = String.Equals(claim.ClaimType, IdentityClaimTypeConfig.ClaimType, StringComparison.InvariantCultureIgnoreCase);
                pe.DisplayText = FormatPermissionDisplayText(pe, isIdentityClaimType, result);

                entities.Add(pe);
                ClaimsProviderLogging.Log($"[{ProviderInternalName}] Created entity: display text: '{pe.DisplayText}', value: '{pe.Claim.Value}', claim type: '{pe.Claim.ClaimType}'.", TraceSeverity.VerboseEx, EventSeverity.Information, TraceCategory.Claims_Picking);
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
        public ClaimTypeConfig ClaimTypeConfig;
        public PickerEntity PickerEntity;
        public ResultPropertyCollection LDAPResults;
        public int nbMatch = 0;
        public string Value;
        public string DomainName;
        public string DomainFQDN;
        //public string DEBUG;
    }

    public class LDAPSearchResult
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
        public bool Contains(LDAPSearchResult result, ClaimTypeConfig attribute, bool compareWithDomain)
        {
            foreach (var item in base.Items)
            {
                if (item.ClaimTypeConfig.ClaimType != attribute.ClaimType)
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
