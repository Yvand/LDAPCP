using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
using Microsoft.SharePoint.Utilities;
using Microsoft.SharePoint.WebControls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Yvand.LdapClaimsProvider.Configuration;
using WIF4_5 = System.Security.Claims;

namespace Yvand.LdapClaimsProvider
{
    public interface ILDAPCPSettings : ILdapProviderSettings
    {
        List<ClaimTypeConfig> RuntimeClaimTypesList { get; }
        IEnumerable<ClaimTypeConfig> RuntimeMetadataConfig { get; }
        ClaimTypeConfig IdentityClaimTypeConfig { get; }
        ClaimTypeConfig MainGroupClaimTypeConfig { get; }
    }

    public class LDAPCPSettings : LdapProviderSettings, ILDAPCPSettings
    {
        public static new LDAPCPSettings GetDefaultSettings(string claimsProviderName)
        {
            LdapProviderSettings entraIDProviderSettings = LdapProviderSettings.GetDefaultSettings(claimsProviderName);
            return GenerateFromEntraIDProviderSettings(entraIDProviderSettings);
        }

        public static LDAPCPSettings GenerateFromEntraIDProviderSettings(ILdapProviderSettings settings)
        {
            LDAPCPSettings copy = new LDAPCPSettings();
            Utils.CopyPublicProperties(typeof(LdapProviderSettings), settings, copy);
            return copy;
        }

        public List<ClaimTypeConfig> RuntimeClaimTypesList { get; set; }

        public IEnumerable<ClaimTypeConfig> RuntimeMetadataConfig { get; set; }

        public ClaimTypeConfig IdentityClaimTypeConfig { get; set; }

        public ClaimTypeConfig MainGroupClaimTypeConfig { get; set; }
    }

    public class LDAPCPSE : SPClaimProvider
    {
        public static string ClaimsProviderName => "LDAPCPSE";
        public override string Name => ClaimsProviderName;
        public override bool SupportsEntityInformation => true;
        public override bool SupportsHierarchy => true;
        public override bool SupportsResolve => true;
        public override bool SupportsSearch => true;
        public override bool SupportsUserKey => true;
        public LdapEntityProvider EntityProvider { get; private set; }
        private ReaderWriterLockSlim Lock_LocalConfigurationRefresh = new ReaderWriterLockSlim();
        protected virtual string PickerEntityDisplayText => "({0}) {1}";
        protected virtual string PickerEntityOnMouseOver => "{0}: {1}";

        /// <summary>
        /// Gets the settings that contain the configuration for EntraCP
        /// </summary>
        public ILDAPCPSettings Settings { get; protected set; }

        /// <summary>
        /// Gets custom settings that will be used instead of the settings from the persisted object
        /// </summary>
        private ILDAPCPSettings CustomSettings { get; }

        /// <summary>
        /// Gets the version of the settings, used to refresh the settings if the persisted object is updated
        /// </summary>
        public long SettingsVersion { get; private set; } = -1;

        private SPTrustedLoginProvider _SPTrust;
        /// <summary>
        /// Gets the SharePoint trust that has its property ClaimProviderName equals to <see cref="Name"/>
        /// </summary>
        private SPTrustedLoginProvider SPTrust
        {
            get
            {
                if (this._SPTrust == null)
                {
                    this._SPTrust = Utils.GetSPTrustAssociatedWithClaimsProvider(this.Name);
                }
                return this._SPTrust;
            }
        }

        /// <summary>
        /// Gets the issuer formatted to be like the property SPClaim.OriginalIssuer: "TrustedProvider:TrustedProviderName"
        /// </summary>
        public string OriginalIssuerName => this.SPTrust != null ? SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, this.SPTrust.Name) : String.Empty;

        public LDAPCPSE(string displayName) : base(displayName)
        {
            this.EntityProvider = new LdapEntityProvider(Name);
        }

        public LDAPCPSE(string displayName, ILDAPCPSettings customSettings) : base(displayName)
        {
            this.EntityProvider = new LdapEntityProvider(Name);
            this.CustomSettings = customSettings;
        }

        #region ManageConfiguration
        public static LdapProviderConfiguration GetConfiguration(bool initializeLocalConfiguration = false)
        {
            LdapProviderConfiguration configuration = LdapProviderConfiguration.GetGlobalConfiguration(new Guid(ClaimsProviderConstants.CONFIGURATION_ID), initializeLocalConfiguration);
            return configuration;
        }

        /// <summary>
        /// Creates a configuration for LDAPCPSE. This will delete any existing configuration which may already exist
        /// </summary>
        /// <returns></returns>
        public static LdapProviderConfiguration CreateConfiguration()
        {
            LdapProviderConfiguration configuration = LdapProviderConfiguration.CreateGlobalConfiguration(new Guid(ClaimsProviderConstants.CONFIGURATION_ID), ClaimsProviderConstants.CONFIGURATION_NAME, LDAPCPSE.ClaimsProviderName);
            return configuration;
        }

        /// <summary>
        /// Deletes the configuration for EntraCP
        /// </summary>
        public static void DeleteConfiguration()
        {
            LdapProviderConfiguration configuration = LdapProviderConfiguration.GetGlobalConfiguration(new Guid(ClaimsProviderConstants.CONFIGURATION_ID));
            if (configuration != null)
            {
                configuration.Delete();
            }
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Verifies if claims provider can run in the specified <paramref name="context"/>, and if it has valid and up to date <see cref="Settings"/>.
        /// </summary>
        /// <param name="context">The URI of the current site, or null</param>
        /// <returns>true if claims provider can run, false if it cannot continue</returns>
        public bool ValidateSettings(Uri context)
        {
            if (!Utils.IsClaimsProviderUsedInCurrentContext(context, Name))
            {
                return false;
            }

            if (this.SPTrust == null)
            {
                return false;
            }

            bool success = true;
            this.Lock_LocalConfigurationRefresh.EnterWriteLock();
            try
            {
                ILdapProviderSettings settings = this.GetSettings();
                if (settings == null)
                {
                    return false;
                }

                if (settings.Version == this.SettingsVersion)
                {
                    Logger.Log($"[{this.Name}] Local copy of settings is up to date with version {this.SettingsVersion}.",
                    TraceSeverity.VerboseEx, EventSeverity.Information, TraceCategory.Core);
                    return true;
                }

                this.Settings = LDAPCPSettings.GenerateFromEntraIDProviderSettings(settings);
                Logger.Log($"[{this.Name}] Settings have new version {this.Settings.Version}, refreshing local copy",
                    TraceSeverity.Medium, EventSeverity.Information, TraceCategory.Core);
                success = this.InitializeInternalRuntimeSettings();
                if (success)
                {
#if !DEBUGx
                    this.SettingsVersion = this.Settings.Version;
#endif
                }
            }
            catch (Exception ex)
            {
                success = false;
                Logger.LogException(Name, "while refreshing configuration", TraceCategory.Core, ex);
            }
            finally
            {
                this.Lock_LocalConfigurationRefresh.ExitWriteLock();
            }
            return success;
        }

        /// <summary>
        /// Returns the settings to use
        /// </summary>
        /// <returns></returns>
        public virtual ILdapProviderSettings GetSettings()
        {
            if (this.CustomSettings != null)
            {
                return this.CustomSettings;
            }

            ILdapProviderSettings persistedSettings = null;
            LdapProviderConfiguration PersistedConfiguration = LdapProviderConfiguration.GetGlobalConfiguration(new Guid(ClaimsProviderConstants.CONFIGURATION_ID));
            if (PersistedConfiguration != null)
            {
                persistedSettings = PersistedConfiguration.Settings;
            }
            return persistedSettings;
        }

        /// <summary>
        /// Sets the internal runtime settings properties
        /// </summary>
        /// <returns>True if successful, false if not</returns>
        private bool InitializeInternalRuntimeSettings()
        {
            LDAPCPSettings settings = (LDAPCPSettings)this.Settings;
            if (settings.ClaimTypes?.Count <= 0)
            {
                Logger.Log($"[{this.Name}] Cannot continue because configuration has 0 claim configured.",
                    TraceSeverity.Unexpected, EventSeverity.Error, TraceCategory.Core);
                return false;
            }

            bool identityClaimTypeFound = false;
            bool groupClaimTypeFound = false;
            List<ClaimTypeConfig> claimTypesSetInTrust = new List<ClaimTypeConfig>();
            // Parse the ClaimTypeInformation collection set in the SPTrustedLoginProvider
            foreach (SPTrustedClaimTypeInformation claimTypeInformation in this.SPTrust.ClaimTypeInformation)
            {
                // Search if current claim type in trust exists in ClaimTypeConfigCollection
                ClaimTypeConfig claimTypeConfig = settings.ClaimTypes.FirstOrDefault(x =>
                    String.Equals(x.ClaimType, claimTypeInformation.MappedClaimType, StringComparison.InvariantCultureIgnoreCase) &&
                    !x.UseMainClaimTypeOfDirectoryObject &&
                    !String.IsNullOrWhiteSpace(x.LDAPAttribute) &&
                    !String.IsNullOrWhiteSpace(x.LDAPClass));

                if (claimTypeConfig == null)
                {
                    continue;
                }
                ClaimTypeConfig localClaimTypeConfig = claimTypeConfig.CopyConfiguration();
                localClaimTypeConfig.ClaimTypeDisplayName = claimTypeInformation.DisplayName;
                claimTypesSetInTrust.Add(localClaimTypeConfig);
                if (String.Equals(this.SPTrust.IdentityClaimTypeInformation.MappedClaimType, localClaimTypeConfig.ClaimType, StringComparison.InvariantCultureIgnoreCase))
                {
                    // Identity claim type found, set IdentityClaimTypeConfig property
                    identityClaimTypeFound = true;
                    settings.IdentityClaimTypeConfig = localClaimTypeConfig;
                }
                else if (!groupClaimTypeFound && localClaimTypeConfig.EntityType == DirectoryObjectType.Group)
                {
                    groupClaimTypeFound = true;
                    settings.MainGroupClaimTypeConfig = localClaimTypeConfig;
                }
            }

            if (!identityClaimTypeFound)
            {
                Logger.Log($"[{this.Name}] Cannot continue because identity claim type '{this.SPTrust.IdentityClaimTypeInformation.MappedClaimType}' set in the SPTrustedIdentityTokenIssuer '{SPTrust.Name}' is missing in the ClaimTypeConfig list.", TraceSeverity.Unexpected, EventSeverity.ErrorCritical, TraceCategory.Core);
                return false;
            }

            // Check if there are additional properties to use in queries (UseMainClaimTypeOfDirectoryObject set to true)
            List<ClaimTypeConfig> additionalClaimTypeConfigList = new List<ClaimTypeConfig>();
            foreach (ClaimTypeConfig claimTypeConfig in settings.ClaimTypes.Where(x => x.UseMainClaimTypeOfDirectoryObject))
            {
                ClaimTypeConfig localClaimTypeConfig = claimTypeConfig.CopyConfiguration();
                if (localClaimTypeConfig.EntityType == DirectoryObjectType.User)
                {
                    localClaimTypeConfig.ClaimType = settings.IdentityClaimTypeConfig.ClaimType;
                    localClaimTypeConfig.LDAPAttributeToShowAsDisplayText = settings.IdentityClaimTypeConfig.LDAPAttributeToShowAsDisplayText;
                }
                else
                {
                    // If not a user, it must be a group
                    if (settings.MainGroupClaimTypeConfig == null)
                    {
                        continue;
                    }
                    localClaimTypeConfig.ClaimType = settings.MainGroupClaimTypeConfig.ClaimType;
                    localClaimTypeConfig.LDAPAttributeToShowAsDisplayText = settings.MainGroupClaimTypeConfig.LDAPAttributeToShowAsDisplayText;
                    localClaimTypeConfig.ClaimTypeDisplayName = settings.MainGroupClaimTypeConfig.ClaimTypeDisplayName;
                }
                additionalClaimTypeConfigList.Add(localClaimTypeConfig);
            }

            settings.RuntimeClaimTypesList = new List<ClaimTypeConfig>(claimTypesSetInTrust.Count + additionalClaimTypeConfigList.Count);
            settings.RuntimeClaimTypesList.AddRange(claimTypesSetInTrust);
            settings.RuntimeClaimTypesList.AddRange(additionalClaimTypeConfigList);

            // Get all PickerEntity metadata with a DirectoryObjectProperty set
            settings.RuntimeMetadataConfig = settings.ClaimTypes.Where(x =>
                !String.IsNullOrWhiteSpace(x.EntityDataKey) &&
                !String.IsNullOrWhiteSpace(x.LDAPAttribute) &&
                !String.IsNullOrWhiteSpace(x.LDAPClass));

            if (settings.LdapConnections == null || settings.LdapConnections.Count < 1)
            {
                return false;
            }
            // Initialize Graph client on each tenant
            foreach (var tenant in settings.LdapConnections)
            {
                tenant.Initialize();
            }
            this.Settings = settings;
            return true;
        }
        #endregion

        #region Augmentation
        protected override void FillClaimsForEntity(Uri context, SPClaim entity, List<SPClaim> claims)
        {
            AugmentEntity(context, entity, null, claims);
        }
        protected override void FillClaimsForEntity(Uri context, SPClaim entity, SPClaimProviderContext claimProviderContext, List<SPClaim> claims)
        {
            AugmentEntity(context, entity, claimProviderContext, claims);
        }

        /// <summary>
        /// Gets the group membership of the <paramref name="entity"/> and add it to the list of <paramref name="claims"/>
        /// </summary>
        /// <param name="context"></param>
        /// <param name="entity">entity to augment</param>
        /// <param name="claimProviderContext">Can be null</param>
        /// <param name="claims"></param>
        protected void AugmentEntity(Uri context, SPClaim entity, SPClaimProviderContext claimProviderContext, List<SPClaim> claims)
        {
            SPClaim decodedEntity;
            if (SPClaimProviderManager.IsUserIdentifierClaim(entity))
            {
                decodedEntity = SPClaimProviderManager.DecodeUserIdentifierClaim(entity);
            }
            else
            {
                if (SPClaimProviderManager.IsEncodedClaim(entity.Value))
                {
                    decodedEntity = SPClaimProviderManager.Local.DecodeClaim(entity.Value);
                }
                else
                {
                    decodedEntity = entity;
                }
            }

            SPOriginalIssuerType loginType = SPOriginalIssuers.GetIssuerType(decodedEntity.OriginalIssuer);
            if (loginType != SPOriginalIssuerType.TrustedProvider && loginType != SPOriginalIssuerType.ClaimProvider)
            {
                Logger.Log($"[{Name}] Not trying to augment '{decodedEntity.Value}' because his OriginalIssuer is '{decodedEntity.OriginalIssuer}'.",
                    TraceSeverity.VerboseEx, EventSeverity.Information, TraceCategory.Augmentation);
                return;
            }

            if (!ValidateSettings(context)) { return; }

            this.Lock_LocalConfigurationRefresh.EnterReadLock();
            OperationContext currentContext = null;
            try
            {
                // There can be multiple TrustedProvider on the farm, but EntraCP should only do augmentation if current entity is from TrustedProvider it is associated with
                if (!String.Equals(decodedEntity.OriginalIssuer, this.OriginalIssuerName, StringComparison.InvariantCultureIgnoreCase)) { return; }

                if (!this.Settings.EnableAugmentation) { return; }

                Logger.Log($"[{Name}] Starting augmentation for user '{decodedEntity.Value}'.", TraceSeverity.Verbose, EventSeverity.Information, TraceCategory.Augmentation);
                //ClaimTypeConfig groupClaimTypeSettings = this.Settings.RuntimeClaimTypesList.FirstOrDefault(x => x.EntityType == DirectoryObjectType.Group);
                //if (groupClaimTypeSettings == null)
                //{
                //    Logger.Log($"[{Name}] No claim type with EntityType 'Group' was found, please check claims mapping table.",
                //        TraceSeverity.High, EventSeverity.Error, TraceCategory.Augmentation);
                //    return;
                //}

                currentContext = new OperationContext(this.Settings, OperationType.Augmentation, null, decodedEntity, context, null, null, Int32.MaxValue);
                Stopwatch timer = new Stopwatch();
                timer.Start();
                List<string> groups = this.EntityProvider.GetEntityGroups(currentContext);
                timer.Stop();
                if (groups?.Count > 0)
                {
                    foreach (string group in groups)
                    {
                        claims.Add(CreateClaim(currentContext.Settings.MainGroupClaimTypeConfig.ClaimType, group, currentContext.Settings.MainGroupClaimTypeConfig.ClaimValueType));
                        Logger.Log($"[{Name}] Added group '{group}' to user '{currentContext.IncomingEntity.Value}'",
                            TraceSeverity.Verbose, EventSeverity.Information, TraceCategory.Augmentation);
                    }
                    Logger.Log($"[{Name}] Augmented user '{currentContext.IncomingEntity.Value}' with {groups.Count} groups in {timer.ElapsedMilliseconds} ms",
                        TraceSeverity.Medium, EventSeverity.Information, TraceCategory.Augmentation);
                }
                else
                {
                    Logger.Log($"[{Name}] Got no group in {timer.ElapsedMilliseconds} ms for user '{currentContext.IncomingEntity.Value}'",
                        TraceSeverity.Medium, EventSeverity.Information, TraceCategory.Augmentation);
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(Name, "in AugmentEntity", TraceCategory.Augmentation, ex);
            }
            finally
            {
                this.Lock_LocalConfigurationRefresh.ExitReadLock();
                if (currentContext != null)
                {
                    foreach (LdapConnection ldapConnection in currentContext.LdapConnections)
                    {
                        if (ldapConnection.DirectoryConnection != null)
                        {
                            ldapConnection.DirectoryConnection.Dispose();
                        }
                    }
                }
            }
        }
        #endregion

        #region Search
        protected override void FillResolve(Uri context, string[] entityTypes, string resolveInput, List<PickerEntity> resolved)
        {
            return;
        }

        protected override void FillSearch(Uri context, string[] entityTypes, string searchPattern, string hierarchyNodeID, int maxCount, SPProviderHierarchyTree searchTree)
        {
            return;
        }
        #endregion

        #region Validation
        protected override void FillResolve(Uri context, string[] entityTypes, SPClaim resolveInput, List<PickerEntity> resolved)
        {
            return;
        }
        #endregion

        #region ProcessSearchOrValidation
        protected List<PickerEntity> SearchOrValidate(OperationContext currentContext)
        {
            List<LdapSearchResult> ldapSearchResults = null;
            LdapSearchResultCollection processedLdapResults;
            List<PickerEntity> pickerEntityList = new List<PickerEntity>();
            try
            {
                if (currentContext.Settings.AlwaysResolveUserInput)
                {
                    // Completely bypass query to LDAP servers
                    pickerEntityList = CreatePickerEntityForSpecificClaimTypes(
                        currentContext.Input,
                        currentContext.CurrentClaimTypeConfigList.FindAll(x => !x.UseMainClaimTypeOfDirectoryObject));
                    Logger.Log($"[{Name}] Created {pickerEntityList.Count} entity(ies) without contacting LDAP server(s) because property AlwaysResolveUserInput is set to true.",
                        TraceSeverity.Medium, EventSeverity.Information, TraceCategory.Claims_Picking);
                    return pickerEntityList;
                }

                // It is either a search or a validation
                using (new SPMonitoredScope($"[{Name}] Total time spent to query LDAP server(s)", 1000))
                {
                    ldapSearchResults = this.EntityProvider.SearchOrValidateEntities(currentContext);
                }
                if (currentContext.OperationType == OperationType.Search)
                {
                    processedLdapResults = this.ProcessLdapResults(currentContext, ldapSearchResults);
                    pickerEntityList = processedLdapResults.Select(x => x.PickerEntity).ToList();
                    // Check if input starts with a prefix configured on a ClaimTypeConfig. If so an entity should be returned using ClaimTypeConfig found
                    // ClaimTypeConfigEnsureUniquePrefixToBypassLookup ensures that collection cannot contain duplicates
                    ClaimTypeConfig ctConfigWithInputPrefixMatch = currentContext.CurrentClaimTypeConfigList.FirstOrDefault(x =>
                        !String.IsNullOrEmpty(x.PrefixToBypassLookup) &&
                        currentContext.Input.StartsWith(x.PrefixToBypassLookup, StringComparison.InvariantCultureIgnoreCase));
                    if (ctConfigWithInputPrefixMatch != null)
                    {
                        string inputWithoutPrefix = currentContext.Input.Substring(ctConfigWithInputPrefixMatch.PrefixToBypassLookup.Length);
                        if (String.IsNullOrEmpty(inputWithoutPrefix))
                        {
                            // No value in the input after the prefix, return
                            return pickerEntityList;
                        }
                        PickerEntity entity = CreatePickerEntityForSpecificClaimType(
                            inputWithoutPrefix,
                            ctConfigWithInputPrefixMatch);
                        if (entity != null)
                        {
                            if (pickerEntityList == null) { pickerEntityList = new List<PickerEntity>(); }
                            pickerEntityList.Add(entity);
                            Logger.Log($"[{Name}] Created entity without contacting Microsoft Entra ID tenant(s) because input started with prefix '{ctConfigWithInputPrefixMatch.PrefixToBypassLookup}', which is configured for claim type '{ctConfigWithInputPrefixMatch.ClaimType}'. Claim value: '{entity.Claim.Value}', claim type: '{entity.Claim.ClaimType}'",
                                TraceSeverity.VerboseEx, EventSeverity.Information, TraceCategory.Claims_Picking);
                        }
                    }
                }
                else if (currentContext.OperationType == OperationType.Validation)
                {
                    if (ldapSearchResults?.Count == 1)
                    {
                        // Got the expected count (1 DirectoryObject)
                        processedLdapResults = this.ProcessLdapResults(currentContext, ldapSearchResults);
                        pickerEntityList = processedLdapResults.Select(x => x.PickerEntity).ToList();
                    }

                    if (!String.IsNullOrWhiteSpace(currentContext.IncomingEntityClaimTypeConfig.PrefixToBypassLookup))
                    {
                        // At this stage, it is impossible to know if entity was originally created with the keyword that bypass query to Microsoft Entra ID
                        // But it should be always validated since property PrefixToBypassLookup is set for current ClaimTypeConfig, so create entity manually
                        PickerEntity entity = CreatePickerEntityForSpecificClaimType(
                            currentContext.IncomingEntity.Value,
                            currentContext.IncomingEntityClaimTypeConfig);
                        if (entity != null)
                        {
                            pickerEntityList = new List<PickerEntity>(1) { entity };
                            Logger.Log($"[{Name}] Validated entity without contacting Microsoft Entra ID tenant(s) because its claim type ('{currentContext.IncomingEntityClaimTypeConfig.ClaimType}') has property 'PrefixToBypassLookup' set in EntraCPConfig.ClaimTypes. Claim value: '{entity.Claim.Value}', claim type: '{entity.Claim.ClaimType}'",
                                TraceSeverity.VerboseEx, EventSeverity.Information, TraceCategory.Claims_Picking);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(Name, "in SearchOrValidate", TraceCategory.Claims_Picking, ex);
            }
            pickerEntityList = this.InspectEntitiesFound(currentContext, pickerEntityList);
            return pickerEntityList;
        }

        protected virtual List<PickerEntity> InspectEntitiesFound(OperationContext currentContext, List<PickerEntity> entities)
        {
            return entities;
        }

        protected virtual LdapSearchResultCollection ProcessLdapResults(OperationContext currentContext, List<LdapSearchResult> LDAPSearchResults)
        {
            LdapSearchResultCollection results = new LdapSearchResultCollection();
            ResultPropertyCollection LDAPResultProperties;
            IEnumerable<ClaimTypeConfig> ctConfigs = currentContext.CurrentClaimTypeConfigList;
            if (currentContext.ExactSearch)
            {
                ctConfigs = currentContext.CurrentClaimTypeConfigList.Where(x => !x.UseMainClaimTypeOfDirectoryObject);
            }

            foreach (LdapSearchResult LDAPResult in LDAPSearchResults)
            {
                LDAPResultProperties = LDAPResult.LdapEntityProperties;
                // objectclass attribute should never be missing because it is explicitely requested in LDAP query
                if (!LDAPResultProperties.Contains("objectclass"))
                {
                    Logger.Log($"[{Name}] Property \"objectclass\" is missing in LDAP result, this may be due to insufficient entities of the account connecting to LDAP server '{LDAPResult.AuthorityMatch.DomainFQDN}'. Skipping result.", TraceSeverity.Unexpected, EventSeverity.Error, TraceCategory.GraphRequests);
                    continue;
                }

                // Cast collection to be able to use StringComparer.InvariantCultureIgnoreCase for case insensitive search of ldap properties
                IEnumerable<string> LDAPResultPropertyNames = LDAPResultProperties.PropertyNames.Cast<string>();

                // Issue https://github.com/Yvand/LDAPCP/issues/16: If current result is a user, ensure LDAP attribute of identity ClaimTypeConfig exists in current LDAP result
                bool isUserWithNoIdentityAttribute = false;
                if (LDAPResultProperties["objectclass"].Cast<string>().Contains(currentContext.Settings.IdentityClaimTypeConfig.LDAPClass, StringComparer.InvariantCultureIgnoreCase))
                {
                    // This is a user: check if his identity LDAP attribute (e.g. mail or sAMAccountName) is present
                    if (!LDAPResultPropertyNames.Contains(currentContext.Settings.IdentityClaimTypeConfig.LDAPAttribute, StringComparer.InvariantCultureIgnoreCase))
                    {
                        // This may match a result like PrimaryGroupID, which has EntityType "Group", but LDAPClass "User"
                        // So it cannot be ruled out immediately, but needs be tested against each ClaimTypeConfig
                        //Logger.Log($"[{Name}] Ignoring a user because he doesn't have the LDAP attribute '{IdentityClaimTypeConfig.LDAPAttribute}'", TraceSeverity.VerboseEx, EventSeverity.Information, TraceCategory.LDAP_Lookup);
                        //continue;
                        isUserWithNoIdentityAttribute = true;
                    }
                }

                foreach (ClaimTypeConfig ctConfig in ctConfigs)
                {
                    // Skip if: current config is for users AND LDAP result is a user AND LDAP result doesn't have identity attribute set
                    if (ctConfig.EntityType == DirectoryObjectType.User && isUserWithNoIdentityAttribute)
                    {
                        continue;
                    }

                    // Skip if: LDAPClass of current config does not match objectclass of LDAP result
                    if (!LDAPResultProperties["objectclass"].Cast<string>().Contains(ctConfig.LDAPClass, StringComparer.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    // Skip if: LDAPAttribute of current config is not found in LDAP result
                    if (!LDAPResultPropertyNames.Contains(ctConfig.LDAPAttribute, StringComparer.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    // Get value with of current LDAP attribute
                    // TODO: investigate https://github.com/Yvand/LDAPCP/issues/43
                    string directoryObjectPropertyValue = LDAPResultProperties[LDAPResultPropertyNames.First(x => String.Equals(x, ctConfig.LDAPAttribute, StringComparison.InvariantCultureIgnoreCase))][0].ToString();

                    // Check if current LDAP attribute value matches the input
                    if (currentContext.ExactSearch)
                    {
                        if (!String.Equals(directoryObjectPropertyValue, currentContext.Input, StringComparison.InvariantCultureIgnoreCase))
                        {
                            continue;
                        }
                    }
                    else
                    {
                        if (currentContext.Settings.AddWildcardAsPrefixOfInput)
                        {
                            if (directoryObjectPropertyValue.IndexOf(currentContext.Input, StringComparison.InvariantCultureIgnoreCase) != -1)
                            {
                                continue;
                            }
                        }
                        else
                        {
                            if (!directoryObjectPropertyValue.StartsWith(currentContext.Input, StringComparison.InvariantCultureIgnoreCase))
                            {
                                continue;
                            }
                        }
                    }

                    // Check if current result (association of LDAP result + ClaimTypeConfig) is not already in results list
                    // Get ClaimTypeConfig to use to check if result is already present in the results list
                    ClaimTypeConfig ctConfigToUseForDuplicateCheck = ctConfig;
                    if (ctConfig.UseMainClaimTypeOfDirectoryObject)
                    {
                        if (ctConfig.EntityType == DirectoryObjectType.User)
                        {
                            if (String.Equals(ctConfig.LDAPClass, currentContext.Settings.IdentityClaimTypeConfig.LDAPClass, StringComparison.InvariantCultureIgnoreCase))
                            {
                                ctConfigToUseForDuplicateCheck = currentContext.Settings.IdentityClaimTypeConfig;
                            }
                            else
                            {
                                continue;  // Current ClaimTypeConfig is a user but current LDAP result is not, skip
                            }
                        }
                        else
                        {
                            if (currentContext.Settings.MainGroupClaimTypeConfig != null && String.Equals(ctConfig.LDAPClass, currentContext.Settings.MainGroupClaimTypeConfig.LDAPClass, StringComparison.InvariantCultureIgnoreCase))
                            {
                                ctConfigToUseForDuplicateCheck = currentContext.Settings.MainGroupClaimTypeConfig;
                            }
                            else
                            {
                                continue;  // Current ClaimTypeConfig is a group but current LDAP result is not, skip
                            }
                        }
                    }

                    // When token domain is present, then ensure we do compare with the actual domain name
                    bool compareWithDomain = Utils.HasPrefixToken(ctConfig.ClaimValuePrefix, ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME);// ? true : currentContext.Settings.CompareResultsWithDomainNameProp;
                    if (!compareWithDomain)
                    {
                        compareWithDomain = Utils.HasPrefixToken(ctConfig.ClaimValuePrefix, ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN);// ? true : currentContext.Settings.CompareResultsWithDomainNameProp;
                    }
                    if (results.Contains(LDAPResult, ctConfigToUseForDuplicateCheck, compareWithDomain))
                    {
                        continue;
                    }

                    LDAPResult.ClaimTypeConfigMatch = ctConfig;
                    LDAPResult.ValueMatch = directoryObjectPropertyValue;
                    LDAPResult.PickerEntity = CreatePickerEntityHelper(currentContext, LDAPResult);
                    results.Add(LDAPResult);

                    //results.Add(
                    //    new ConsolidatedResult
                    //    {
                    //        ClaimTypeConfig = ctConfig,
                    //        LDAPResults = LDAPResultProperties,
                    //        Value = directoryObjectPropertyValue,
                    //        DomainName = LDAPResult.DomainName,
                    //        DomainFQDN = LDAPResult.DomainFQDN,
                    //        //DEBUG = String.Format("LDAPAttribute: {0}, LDAPAttributeValue: {1}, AlwaysResolveAgainstIdentityClaim: {2}", attr.LDAPAttribute, LDAPResultProperties[attr.LDAPAttribute][0].ToString(), attr.AlwaysResolveAgainstIdentityClaim.ToString())
                    //    });
                }
            }
            Logger.Log(String.Format("[{0}] {1} entity(ies) to create after filtering", Name, results.Count), TraceSeverity.Medium, EventSeverity.Information, TraceCategory.GraphRequests);
            foreach (var result in results)
            {
                //PickerEntity pe = CreatePickerEntityHelper(result);
                //// Add it to the return list of picker entries.
                //result.PickerEntity = pe;
            }
            return results;
        }
        #endregion

        #region Helpers
        protected virtual PickerEntity CreatePickerEntityHelper(OperationContext currentContext, LdapSearchResult result)
        {
            PickerEntity pe = CreatePickerEntity();
            SPClaim claim;
            string permissionValue = result.ValueMatch;
            string permissionClaimType = result.ClaimTypeConfigMatch.ClaimType;
            bool isIdentityClaimType = false;

            if ((SPClaimTypes.Equals(permissionClaimType, SPTrust.IdentityClaimTypeInformation.MappedClaimType)
                || result.ClaimTypeConfigMatch.UseMainClaimTypeOfDirectoryObject) && result.ClaimTypeConfigMatch.LDAPClass == currentContext.Settings.IdentityClaimTypeConfig.LDAPClass)
            {
                isIdentityClaimType = true;
            }

            if (result.ClaimTypeConfigMatch.UseMainClaimTypeOfDirectoryObject && result.ClaimTypeConfigMatch.LDAPClass != currentContext.Settings.IdentityClaimTypeConfig.LDAPClass)
            {
                // Get reference attribute to use to create actual entity (claim type and its LDAPAttribute) from current result
                ClaimTypeConfig attribute = currentContext.Settings.RuntimeClaimTypesList.FirstOrDefault(x => !x.UseMainClaimTypeOfDirectoryObject && x.LDAPClass == result.ClaimTypeConfigMatch.LDAPClass);
                if (attribute != null)
                {
                    permissionClaimType = attribute.ClaimType;
                    result.ClaimTypeConfigMatch.ClaimType = attribute.ClaimType;
                    result.ClaimTypeConfigMatch.EntityType = attribute.EntityType;
                    result.ClaimTypeConfigMatch.ClaimTypeDisplayName = attribute.ClaimTypeDisplayName;
                    permissionValue = result.LdapEntityProperties[attribute.LDAPAttribute][0].ToString();    // Pick value of current result from actual LDAP attribute to use (which is not the LDAP attribute that matches input)
                    result.ClaimTypeConfigMatch.LDAPAttributeToShowAsDisplayText = attribute.LDAPAttributeToShowAsDisplayText;
                    result.ClaimTypeConfigMatch.ClaimValuePrefix = attribute.ClaimValuePrefix;
                    result.ClaimTypeConfigMatch.PrefixToBypassLookup = attribute.PrefixToBypassLookup;
                }
            }

            if (result.ClaimTypeConfigMatch.UseMainClaimTypeOfDirectoryObject && result.ClaimTypeConfigMatch.LDAPClass == currentContext.Settings.IdentityClaimTypeConfig.LDAPClass)
            {
                // This attribute is not directly linked to a claim type, so entity is created with identity claim type
                permissionClaimType = currentContext.Settings.IdentityClaimTypeConfig.ClaimType;
                permissionValue = FormatPermissionValue(currentContext, permissionClaimType, result.LdapEntityProperties[currentContext.Settings.IdentityClaimTypeConfig.LDAPAttribute][0].ToString(), isIdentityClaimType, result);
                claim = CreateClaim(
                    permissionClaimType,
                    permissionValue,
                    currentContext.Settings.IdentityClaimTypeConfig.ClaimValueType/*,
                    false*/);
                pe.EntityType = currentContext.Settings.IdentityClaimTypeConfig.EntityType == DirectoryObjectType.User ? SPClaimEntityTypes.User : ClaimsProviderConstants.GroupClaimEntityType;
            }
            else
            {
                permissionValue = FormatPermissionValue(currentContext, result.ClaimTypeConfigMatch.ClaimType, permissionValue, isIdentityClaimType, result);
                claim = CreateClaim(
                    permissionClaimType,
                    permissionValue,
                    result.ClaimTypeConfigMatch.ClaimValueType/*,
                    false*/);
                pe.EntityType = result.ClaimTypeConfigMatch.EntityType == DirectoryObjectType.User ? SPClaimEntityTypes.User : ClaimsProviderConstants.GroupClaimEntityType;
            }

            int nbMetadata = 0;
            // Populate metadata of new PickerEntity
            // Change condition to fix bug http://ldapcp.codeplex.com/discussions/653087: only rely on the LDAP class
            foreach (ClaimTypeConfig ctConfig in currentContext.Settings.RuntimeMetadataConfig.Where(x => String.Equals(x.LDAPClass, result.ClaimTypeConfigMatch.LDAPClass, StringComparison.InvariantCultureIgnoreCase)))
            {
                // if there is actally a value in the LDAP result, then it can be set
                if (result.LdapEntityProperties.Contains(ctConfig.LDAPAttribute) && result.LdapEntityProperties[ctConfig.LDAPAttribute].Count > 0)
                {
                    pe.EntityData[ctConfig.EntityDataKey] = result.LdapEntityProperties[ctConfig.LDAPAttribute][0].ToString();
                    nbMetadata++;
                    Logger.Log($"[{Name}] Set metadata '{ctConfig.EntityDataKey}' of new entity to '{pe.EntityData[ctConfig.EntityDataKey]}'", TraceSeverity.VerboseEx, EventSeverity.Information, TraceCategory.Claims_Picking);
                }
            }

            pe.Claim = claim;
            pe.IsResolved = true;
            //pe.EntityGroupName = "";
            pe.Description = String.Format(
                PickerEntityOnMouseOver,
                result.ClaimTypeConfigMatch.LDAPAttribute,
                result.ValueMatch);

            pe.DisplayText = FormatPermissionDisplayText(currentContext, pe, isIdentityClaimType, result);

            Logger.Log($"[{Name}] Created entity: display text: '{pe.DisplayText}', value: '{pe.Claim.Value}', claim type: '{pe.Claim.ClaimType}', and filled with {nbMetadata.ToString()} metadata.", TraceSeverity.VerboseEx, EventSeverity.Information, TraceCategory.Claims_Picking);
            return pe;
        }

        private PickerEntity CreatePickerEntityForSpecificClaimType(string value, ClaimTypeConfig ctConfig)
        {
            List<PickerEntity> entities = CreatePickerEntityForSpecificClaimTypes(
                value,
                new List<ClaimTypeConfig>() { ctConfig });
            return entities == null ? null : entities.First();
        }

        private List<PickerEntity> CreatePickerEntityForSpecificClaimTypes(string value, List<ClaimTypeConfig> ctConfigs)
        {
            List<PickerEntity> entities = new List<PickerEntity>();
            foreach (var ctConfig in ctConfigs)
            {
                SPClaim claim = CreateClaim(ctConfig.ClaimType, value, ctConfig.ClaimValueType);
                PickerEntity entity = CreatePickerEntity();
                entity.Claim = claim;
                entity.IsResolved = true;
                entity.EntityType = ctConfig.SharePointEntityType;
                if (String.IsNullOrEmpty(entity.EntityType))
                {
                    entity.EntityType = ctConfig.EntityType == DirectoryObjectType.User ? SPClaimEntityTypes.User : ClaimsProviderConstants.GroupClaimEntityType;
                }
                //entity.EntityGroupName = "";
                entity.Description = String.Format(PickerEntityOnMouseOver, ctConfig.LDAPAttribute, value);

                if (!String.IsNullOrEmpty(ctConfig.EntityDataKey))
                {
                    entity.EntityData[ctConfig.EntityDataKey] = entity.Claim.Value;
                    Logger.Log($"[{Name}] Added metadata '{ctConfig.EntityDataKey}' with value '{entity.EntityData[ctConfig.EntityDataKey]}' to new entity", TraceSeverity.VerboseEx, EventSeverity.Information, TraceCategory.Claims_Picking);
                }

                ClaimsProviderEntityResult result = new ClaimsProviderEntityResult(null, ctConfig, value, value);
                bool isIdentityClaimType = String.Equals(claim.ClaimType, this.Settings.IdentityClaimTypeConfig.ClaimType, StringComparison.InvariantCultureIgnoreCase);
                entity.DisplayText = FormatPermissionDisplayText(entity, isIdentityClaimType, result);

                entities.Add(entity);
                Logger.Log($"[{Name}] Created entity: display text: '{entity.DisplayText}', value: '{entity.Claim.Value}', claim type: '{entity.Claim.ClaimType}'.", TraceSeverity.VerboseEx, EventSeverity.Information, TraceCategory.Claims_Picking);
            }
            return entities.Count > 0 ? entities : null;
        }

        protected virtual string FormatPermissionValue(OperationContext currentContext, string claimType, string claimValue/*, string domainName, string domainFQDN*/, bool isIdentityClaimType, LdapSearchResult result)
        {
            string value = claimValue;

            var attr = currentContext.Settings.RuntimeClaimTypesList.FirstOrDefault(x => SPClaimTypes.Equals(x.ClaimType, claimType));
            if (Utils.HasPrefixToken(attr.ClaimValuePrefix, ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME))
            {
                value = string.Format("{0}{1}", attr.ClaimValuePrefix.Replace(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME, result.AuthorityMatch.DomainName), value);
            }

            if (Utils.HasPrefixToken(attr.ClaimValuePrefix, ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN))
            {
                value = string.Format("{0}{1}", attr.ClaimValuePrefix.Replace(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN, result.AuthorityMatch.DomainFQDN), value);
            }

            return value;
        }

        protected virtual string FormatPermissionDisplayText(OperationContext currentContext, PickerEntity entity, bool isIdentityClaimType, LdapSearchResult result)
        {
            string entityDisplayText = currentContext.Settings.EntityDisplayTextPrefix;
            string claimValue = entity.Claim.Value;
            string valueDisplayedInPermission = String.Empty;
            bool displayLdapMatchForIdentityClaimType = false;
            string prefixToAdd = string.Empty;

            if (result.LdapEntityProperties == null)
            {
                // Result does not come from a LDAP server, it was created manually
                if (isIdentityClaimType)
                {
                    entityDisplayText += claimValue;
                }
                else
                {
                    entityDisplayText += String.Format(PickerEntityDisplayText, result.ClaimTypeConfigMatch.ClaimTypeDisplayName, claimValue);
                }
            }
            else
            {
                if (Utils.HasPrefixToken(result.ClaimTypeConfigMatch.ClaimValuePrefix, ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME))
                {
                    prefixToAdd = string.Format("{0}", result.ClaimTypeConfigMatch.ClaimValuePrefix.Replace(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME, result.AuthorityMatch.DomainName));
                }

                if (Utils.HasPrefixToken(result.ClaimTypeConfigMatch.ClaimValuePrefix, ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN))
                {
                    prefixToAdd = string.Format("{0}", result.ClaimTypeConfigMatch.ClaimValuePrefix.Replace(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN, result.AuthorityMatch.DomainFQDN));
                }

                if (isIdentityClaimType)
                {
                    displayLdapMatchForIdentityClaimType = true; // this.CurrentConfiguration.DisplayLdapMatchForIdentityClaimTypeProp;
                }

                if (!String.IsNullOrEmpty(result.ClaimTypeConfigMatch.LDAPAttributeToShowAsDisplayText) && result.LdapEntityProperties.Contains(result.ClaimTypeConfigMatch.LDAPAttributeToShowAsDisplayText))
                {   // AttributeHelper is set to use a specific LDAP attribute as display text of entity
                    if (!isIdentityClaimType && result.ClaimTypeConfigMatch.ShowClaimNameInDisplayText)
                    {
                        entityDisplayText += "(" + result.ClaimTypeConfigMatch.ClaimTypeDisplayName + ") ";
                    }
                    entityDisplayText += prefixToAdd;
                    valueDisplayedInPermission = result.LdapEntityProperties[result.ClaimTypeConfigMatch.LDAPAttributeToShowAsDisplayText][0].ToString();
                    entityDisplayText += valueDisplayedInPermission;
                }
                else
                {   // AttributeHelper is set to use its actual LDAP attribute as display text of entity
                    if (!isIdentityClaimType)
                    {
                        valueDisplayedInPermission = claimValue.StartsWith(prefixToAdd) ? claimValue : prefixToAdd + claimValue;
                        if (result.ClaimTypeConfigMatch.ShowClaimNameInDisplayText)
                        {
                            entityDisplayText += String.Format(
                                PickerEntityDisplayText,
                                result.ClaimTypeConfigMatch.ClaimTypeDisplayName,
                                valueDisplayedInPermission);
                        }
                        else
                        {
                            entityDisplayText = valueDisplayedInPermission;
                        }
                    }
                    else
                    {   // Always specifically use LDAP attribute of identity claim type
                        entityDisplayText += prefixToAdd;
                        valueDisplayedInPermission = result.LdapEntityProperties[currentContext.Settings.IdentityClaimTypeConfig.LDAPAttribute][0].ToString();
                        entityDisplayText += valueDisplayedInPermission;
                    }
                }

                // Check if LDAP value that actually resolved this result should be included in the display text of the entity
                if (displayLdapMatchForIdentityClaimType && result.LdapEntityProperties.Contains(result.ClaimTypeConfigMatch.LDAPAttribute)
                    && !String.Equals(valueDisplayedInPermission, claimValue, StringComparison.InvariantCultureIgnoreCase))
                {
                    entityDisplayText += String.Format(" ({0})", claimValue);
                }
            }
            return entityDisplayText;
        }
        #endregion

        /// <summary>
        /// Return the identity claim type
        /// </summary>
        /// <returns></returns>
        public override string GetClaimTypeForUserKey()
        {
            try
            {
                return this.SPTrust != null ? this.SPTrust.IdentityClaimTypeInformation.MappedClaimType : String.Empty;
            }
            catch (Exception ex)
            {
                Logger.LogException(Name, "in GetClaimTypeForUserKey", TraceCategory.Rehydration, ex);
            }
            return String.Empty;
        }

        /// <summary>
        /// Return the user key (SPClaim with identity claim type) from the incoming entity
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        protected override SPClaim GetUserKeyForEntity(SPClaim entity)
        {
            try
            {
                if (this.SPTrust == null)
                {
                    return entity;
                }

                // There are 2 scenarios:
                // 1: OriginalIssuer is "SecurityTokenService": Value looks like "05.t|contoso.local|yvand@contoso.local", claim type is "http://schemas.microsoft.com/sharepoint/2009/08/claims/userid" and it must be decoded properly
                // 2: OriginalIssuer is "TrustedProvider:contoso.local": The incoming entity is fine and returned as is
                if (String.Equals(entity.OriginalIssuer, this.OriginalIssuerName, StringComparison.InvariantCultureIgnoreCase))
                {
                    return entity;
                }

                // SPClaimProviderManager.IsUserIdentifierClaim tests if:
                // ClaimType == SPClaimTypes.UserIdentifier ("http://schemas.microsoft.com/sharepoint/2009/08/claims/userid")
                // OriginalIssuer type == SPOriginalIssuerType.SecurityTokenService
                if (!SPClaimProviderManager.IsUserIdentifierClaim(entity))
                {
                    // return entity if not true, otherwise SPClaimProviderManager.DecodeUserIdentifierClaim(entity) throws an ArgumentException
                    return entity;
                }

                // Since SPClaimProviderManager.IsUserIdentifierClaim() returned true, SPClaimProviderManager.DecodeUserIdentifierClaim() will work
                SPClaim curUser = SPClaimProviderManager.DecodeUserIdentifierClaim(entity);
                Logger.Log($"[{Name}] Returning user key for '{entity.Value}'",
                    TraceSeverity.VerboseEx, EventSeverity.Information, TraceCategory.Rehydration);
                return CreateClaim(this.SPTrust.IdentityClaimTypeInformation.MappedClaimType, curUser.Value, curUser.ValueType);
            }
            catch (Exception ex)
            {
                Logger.LogException(Name, "in GetUserKeyForEntity", TraceCategory.Rehydration, ex);
            }
            return null;
        }

        protected override void FillClaimTypes(List<string> claimTypes)
        {
            if (claimTypes == null) { return; }
            bool configIsValid = ValidateSettings(null);
            if (configIsValid)
            {
                this.Lock_LocalConfigurationRefresh.EnterReadLock();
                try
                {

                    foreach (var claimTypeSettings in this.Settings.RuntimeClaimTypesList)
                    {
                        claimTypes.Add(claimTypeSettings.ClaimType);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException(Name, "in FillClaimTypes", TraceCategory.Core, ex);
                }
                finally
                {
                    this.Lock_LocalConfigurationRefresh.ExitReadLock();
                }
            }
        }

        protected override void FillClaimValueTypes(List<string> claimValueTypes)
        {
            claimValueTypes.Add(WIF4_5.ClaimValueTypes.String);
        }

        protected override void FillEntityTypes(List<string> entityTypes)
        {
            entityTypes.Add(SPClaimEntityTypes.User);
            entityTypes.Add(ClaimsProviderConstants.GroupClaimEntityType);
        }

        protected override void FillHierarchy(Uri context, string[] entityTypes, string hierarchyNodeID, int numberOfLevels, SPProviderHierarchyTree hierarchy)
        {
            List<DirectoryObjectType> aadEntityTypes = new List<DirectoryObjectType>();
            if (entityTypes.Contains(SPClaimEntityTypes.User)) { aadEntityTypes.Add(DirectoryObjectType.User); }
            if (entityTypes.Contains(ClaimsProviderConstants.GroupClaimEntityType)) { aadEntityTypes.Add(DirectoryObjectType.Group); }

            if (!ValidateSettings(context)) { return; }

            this.Lock_LocalConfigurationRefresh.EnterReadLock();
            try
            {
                if (hierarchyNodeID == null)
                {
                    // Root level
                    foreach (var azureObject in this.Settings.RuntimeClaimTypesList.FindAll(x => !x.UseMainClaimTypeOfDirectoryObject && aadEntityTypes.Contains(x.EntityType)))
                    {
                        hierarchy.AddChild(
                            new Microsoft.SharePoint.WebControls.SPProviderHierarchyNode(
                                Name,
                                azureObject.ClaimTypeDisplayName,
                                azureObject.ClaimType,
                                true));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(Name, "in FillHierarchy", TraceCategory.Claims_Picking, ex);
            }
            finally
            {
                this.Lock_LocalConfigurationRefresh.ExitReadLock();
            }
        }

        protected override void FillSchema(SPProviderSchema schema)
        {
            schema.AddSchemaElement(new SPSchemaElement(PeopleEditorEntityDataKeys.DisplayName, "Display Name", SPSchemaElementType.Both));
        }

    }
}
