using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
using Microsoft.SharePoint.Utilities;
using Microsoft.SharePoint.WebControls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices;
using System.Linq;
using System.Threading;
using Yvand.LdapClaimsProvider.Configuration;
using Yvand.LdapClaimsProvider.Logging;
using WIF4_5 = System.Security.Claims;

namespace Yvand.LdapClaimsProvider
{
    public interface IClaimsProviderSettings : ILdapProviderSettings
    {
        //List<ClaimTypeConfig> RuntimeClaimTypeConfigList { get; }
        IEnumerable<ClaimTypeConfig> RuntimeMetadataConfig { get; }
        ClaimTypeConfig UserIdentifierClaimTypeConfig { get; }
        ClaimTypeConfig GroupIdentifierClaimTypeConfig { get; }
    }

    public class ClaimsProviderSettings : LdapProviderSettings, IClaimsProviderSettings
    {
        public static new ClaimsProviderSettings GetDefaultSettings(string claimsProviderName)
        {
            LdapProviderSettings entraIDProviderSettings = LdapProviderSettings.GetDefaultSettings(claimsProviderName);
            return GenerateFromEntraIDProviderSettings(entraIDProviderSettings);
        }

        public static ClaimsProviderSettings GenerateFromEntraIDProviderSettings(ILdapProviderSettings settings)
        {
            ClaimsProviderSettings copy = new ClaimsProviderSettings();
            Utils.CopyPublicProperties(typeof(LdapProviderSettings), settings, copy);
            return copy;
        }

        /// <summary>
        /// This list reflects the up to date settings and is used to initialize the list to use based on the current context.
        /// It is not intended to be used directly
        /// </summary>
        internal List<ClaimTypeConfig> RuntimeClaimTypeConfigList { get; set; }

        public IEnumerable<ClaimTypeConfig> RuntimeMetadataConfig { get; set; }

        public ClaimTypeConfig UserIdentifierClaimTypeConfig { get; set; }

        public ClaimTypeConfig GroupIdentifierClaimTypeConfig { get; set; }
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
        public IClaimsProviderSettings Settings { get; protected set; }

        /// <summary>
        /// Gets custom settings that will be used instead of the settings from the persisted object
        /// </summary>
        private IClaimsProviderSettings CustomSettings { get; }

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
        }

        public LDAPCPSE(string displayName, IClaimsProviderSettings customSettings) : base(displayName)
        {
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
                    TraceSeverity.VerboseEx, TraceCategory.Core);
                    return true;
                }

                this.Settings = ClaimsProviderSettings.GenerateFromEntraIDProviderSettings(settings);
                Logger.Log($"[{this.Name}] Settings have new version {this.Settings.Version}, refreshing local copy",
                    TraceSeverity.Medium, TraceCategory.Core);
                success = this.InitializeInternalRuntimeSettings();
                if (success)
                {
#if !DEBUGx
                    this.SettingsVersion = this.Settings.Version;
#endif
                    this.EntityProvider = new LdapEntityProvider(Name, this.Settings);
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
            ClaimsProviderSettings settings = (ClaimsProviderSettings)this.Settings;
            if (settings.ClaimTypes?.Count <= 0)
            {
                Logger.Log($"[{this.Name}] Cannot continue because configuration has 0 claim configured.",
                    TraceSeverity.Unexpected, TraceCategory.Core);
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
                    !x.IsAdditionalLdapSearchAttribute &&
                    !String.IsNullOrWhiteSpace(x.DirectoryObjectAttribute) &&
                    !String.IsNullOrWhiteSpace(x.DirectoryObjectClass));

                if (claimTypeConfig == null)
                {
                    continue;
                }
                ClaimTypeConfig localClaimTypeConfig = claimTypeConfig.CopyConfiguration();
                localClaimTypeConfig.ClaimTypeDisplayName = claimTypeInformation.DisplayName;
                claimTypesSetInTrust.Add(localClaimTypeConfig);
                if (String.Equals(this.SPTrust.IdentityClaimTypeInformation.MappedClaimType, localClaimTypeConfig.ClaimType, StringComparison.InvariantCultureIgnoreCase))
                {
                    // Identity claim type found, set UserIdentifierClaimTypeConfig property
                    identityClaimTypeFound = true;
                    settings.UserIdentifierClaimTypeConfig = localClaimTypeConfig;
                }
                else if (!groupClaimTypeFound && localClaimTypeConfig.DirectoryObjectType == DirectoryObjectType.Group)
                {
                    groupClaimTypeFound = true;
                    settings.GroupIdentifierClaimTypeConfig = localClaimTypeConfig;
                }
            }

            if (!identityClaimTypeFound)
            {
                Logger.Log($"[{this.Name}] Cannot continue because identity claim type '{this.SPTrust.IdentityClaimTypeInformation.MappedClaimType}' set in the SPTrustedIdentityTokenIssuer '{SPTrust.Name}' is missing in the ClaimTypeConfig list.", TraceSeverity.Unexpected, TraceCategory.Core);
                return false;
            }

            // Check if there are additional properties to use in queries (IsAdditionalLdapSearchAttribute set to true)
            List<ClaimTypeConfig> additionalClaimTypeConfigList = new List<ClaimTypeConfig>();
            foreach (ClaimTypeConfig claimTypeConfig in settings.ClaimTypes.Where(x => x.IsAdditionalLdapSearchAttribute))
            {
                ClaimTypeConfig localClaimTypeConfig = claimTypeConfig.CopyConfiguration();
                if (localClaimTypeConfig.DirectoryObjectType == DirectoryObjectType.User)
                {
                    localClaimTypeConfig.ClaimType = settings.UserIdentifierClaimTypeConfig.ClaimType;
                    localClaimTypeConfig.DirectoryObjectAttributeForDisplayText = settings.UserIdentifierClaimTypeConfig.DirectoryObjectAttributeForDisplayText;
                }
                else
                {
                    // If not a user, it must be a group
                    if (settings.GroupIdentifierClaimTypeConfig == null)
                    {
                        continue;
                    }
                    localClaimTypeConfig.ClaimType = settings.GroupIdentifierClaimTypeConfig.ClaimType;
                    localClaimTypeConfig.DirectoryObjectAttributeForDisplayText = settings.GroupIdentifierClaimTypeConfig.DirectoryObjectAttributeForDisplayText;
                    localClaimTypeConfig.ClaimTypeDisplayName = settings.GroupIdentifierClaimTypeConfig.ClaimTypeDisplayName;
                }
                additionalClaimTypeConfigList.Add(localClaimTypeConfig);
            }

            settings.RuntimeClaimTypeConfigList = new List<ClaimTypeConfig>(claimTypesSetInTrust.Count + additionalClaimTypeConfigList.Count);
            settings.RuntimeClaimTypeConfigList.AddRange(claimTypesSetInTrust);
            settings.RuntimeClaimTypeConfigList.AddRange(additionalClaimTypeConfigList);

            // Get all PickerEntity metadata with a DirectoryObjectProperty set
            settings.RuntimeMetadataConfig = settings.ClaimTypes.Where(x =>
                !String.IsNullOrWhiteSpace(x.SPEntityDataKey) &&
                !String.IsNullOrWhiteSpace(x.DirectoryObjectAttribute) &&
                !String.IsNullOrWhiteSpace(x.DirectoryObjectClass));

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
                    TraceSeverity.VerboseEx, TraceCategory.Augmentation);
                return;
            }

            using (new SPMonitoredScope($"[{ClaimsProviderName}] Augmentation for user \"{decodedEntity.Value}", 3000))
            {
                if (!ValidateSettings(context)) { return; }
                this.Lock_LocalConfigurationRefresh.EnterReadLock();
                OperationContext currentContext = null;
                try
                {
                    // There can be multiple TrustedProvider on the farm, but EntraCP should only do augmentation if current entity is from TrustedProvider it is associated with
                    if (!String.Equals(decodedEntity.OriginalIssuer, this.OriginalIssuerName, StringComparison.InvariantCultureIgnoreCase)) { return; }

                    if (!this.Settings.EnableAugmentation) { return; }

                    if (Settings.GroupIdentifierClaimTypeConfig == null)
                    {
                        Logger.Log($"[{Name}] No object with DirectoryObjectType 'Group' was found, please check claims mapping table.",
                            TraceSeverity.High, TraceCategory.Augmentation);
                        return;
                    }

                    Logger.Log($"[{Name}] Starting augmentation for user '{decodedEntity.Value}'.", TraceSeverity.Verbose, TraceCategory.Augmentation);
                    currentContext = new OperationContext(this.Settings as ClaimsProviderSettings, OperationType.Augmentation, String.Empty, decodedEntity, context, null, null, Int32.MaxValue);
                    ValidateRuntimeSettings(currentContext);
                    Stopwatch timer = new Stopwatch();
                    timer.Start();
                    List<string> groups = this.EntityProvider.GetEntityGroups(currentContext);
                    timer.Stop();
                    if (groups?.Count > 0)
                    {
                        foreach (string group in groups)
                        {
                            claims.Add(CreateClaim(this.Settings.GroupIdentifierClaimTypeConfig.ClaimType, group, this.Settings.GroupIdentifierClaimTypeConfig.ClaimValueType));
                            Logger.Log($"[{Name}] Added group '{group}' to user '{currentContext.IncomingEntity.Value}'",
                                TraceSeverity.Verbose, TraceCategory.Augmentation);
                        }
                        Logger.Log($"[{Name}] Augmented user '{currentContext.IncomingEntity.Value}' with {groups.Count} groups in {timer.ElapsedMilliseconds} ms",
                            TraceSeverity.Medium, TraceCategory.Augmentation);
                    }
                    else
                    {
                        Logger.Log($"[{Name}] Got no group in {timer.ElapsedMilliseconds} ms for user '{currentContext.IncomingEntity.Value}'",
                            TraceSeverity.Medium, TraceCategory.Augmentation);
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
                        foreach (DirectoryConnection ldapConnection in currentContext.LdapConnections)
                        {
                            if (ldapConnection.LdapEntry != null)
                            {
                                ldapConnection.LdapEntry.Dispose();
                            }
                        }
                    }
                }
            }
        }
        #endregion

        #region Search
        protected override void FillResolve(Uri context, string[] entityTypes, string resolveInput, List<PickerEntity> resolved)
        {
            using (new SPMonitoredScope($"[{ClaimsProviderName}] Search entities which match input \"{resolveInput}", 3000))
            {
                if (!ValidateSettings(context)) { return; }

                this.Lock_LocalConfigurationRefresh.EnterReadLock();
                try
                {
                    OperationContext currentContext = new OperationContext(this.Settings as ClaimsProviderSettings, OperationType.Search, resolveInput, null, context, entityTypes, null, 30);
                    ValidateRuntimeSettings(currentContext);
                    List<PickerEntity> entities = SearchOrValidate(currentContext);
                    if (entities == null || entities.Count == 0) { return; }
                    foreach (PickerEntity entity in entities)
                    {
                        resolved.Add(entity);
                        Logger.Log($"[{Name}] Added entity: display text: '{entity.DisplayText}', claim value: '{entity.Claim.Value}', claim type: '{entity.Claim.ClaimType}'",
                            TraceSeverity.Verbose, TraceCategory.Claims_Picking);
                    }
                    Logger.Log($"[{Name}] Returned {entities.Count} entities with value '{currentContext.Input}'", TraceSeverity.Medium, TraceCategory.Claims_Picking);
                }
                catch (Exception ex)
                {
                    Logger.LogException(Name, "in FillResolve(string)", TraceCategory.Claims_Picking, ex);
                }
                finally
                {
                    this.Lock_LocalConfigurationRefresh.ExitReadLock();
                }
            }
        }

        protected override void FillSearch(Uri context, string[] entityTypes, string searchPattern, string hierarchyNodeID, int maxCount, SPProviderHierarchyTree searchTree)
        {
            using (new SPMonitoredScope($"[{ClaimsProviderName}] Search entities which match input \"{searchPattern}", 3000))
            {
                if (!ValidateSettings(context)) { return; }

                this.Lock_LocalConfigurationRefresh.EnterReadLock();
                try
                {
                    OperationContext currentContext = new OperationContext(this.Settings as ClaimsProviderSettings, OperationType.Search, searchPattern, null, context, entityTypes, hierarchyNodeID, maxCount);
                    ValidateRuntimeSettings(currentContext);
                    List<PickerEntity> entities = this.SearchOrValidate(currentContext);
                    if (entities == null || entities.Count == 0) { return; }
                    SPProviderHierarchyNode matchNode = null;
                    foreach (PickerEntity entity in entities)
                    {
                        // Add current PickerEntity to the corresponding ClaimType in the hierarchy
                        if (searchTree.HasChild(entity.Claim.ClaimType))
                        {
                            matchNode = searchTree.Children.First(x => x.HierarchyNodeID == entity.Claim.ClaimType);
                        }
                        else
                        {
                            ClaimTypeConfig ctConfig = currentContext.CurrentClaimTypeConfigList.FirstOrDefault(x =>
                                !x.IsAdditionalLdapSearchAttribute &&
                                String.Equals(x.ClaimType, entity.Claim.ClaimType, StringComparison.InvariantCultureIgnoreCase));

                            string nodeName = ctConfig != null ? ctConfig.ClaimTypeDisplayName : entity.Claim.ClaimType;
                            matchNode = new SPProviderHierarchyNode(Name, nodeName, entity.Claim.ClaimType, true);
                            searchTree.AddChild(matchNode);
                        }
                        matchNode.AddEntity(entity);
                        Logger.Log($"[{Name}] Added entity: display text: '{entity.DisplayText}', claim value: '{entity.Claim.Value}', claim type: '{entity.Claim.ClaimType}'",
                            TraceSeverity.Verbose, TraceCategory.Claims_Picking);
                    }
                    Logger.Log($"[{Name}] Returned {entities.Count} entities from value '{currentContext.Input}'",
                        TraceSeverity.Medium, TraceCategory.Claims_Picking);
                }
                catch (Exception ex)
                {
                }
                finally
                {
                    this.Lock_LocalConfigurationRefresh.ExitReadLock();
                }
            }
        }
        #endregion

        #region Validation
        protected override void FillResolve(Uri context, string[] entityTypes, SPClaim resolveInput, List<PickerEntity> resolved)
        {
            using (new SPMonitoredScope($"[{ClaimsProviderName}] Validate entity whith value \"{resolveInput.ClaimType}", 3000))
            {
                if (!ValidateSettings(context)) { return; }

                this.Lock_LocalConfigurationRefresh.EnterReadLock();
                try
                {
                    // Ensure incoming claim should be validated by EntraCP
                    // Must be made after call to Initialize because SPTrustedLoginProvider name must be known
                    if (!String.Equals(resolveInput.OriginalIssuer, this.OriginalIssuerName, StringComparison.InvariantCultureIgnoreCase)) { return; }

                    OperationContext currentContext = new OperationContext(this.Settings as ClaimsProviderSettings, OperationType.Validation, resolveInput.Value, resolveInput, context, entityTypes, null, 1);
                    ValidateRuntimeSettings(currentContext);
                    List<PickerEntity> entities = this.SearchOrValidate(currentContext);
                    if (entities?.Count == 1)
                    {
                        resolved.Add(entities[0]);
                        Logger.Log($"[{Name}] Validated entity: display text: '{entities[0].DisplayText}', claim value: '{entities[0].Claim.Value}', claim type: '{entities[0].Claim.ClaimType}'",
                            TraceSeverity.High, TraceCategory.Claims_Picking);
                    }
                    else
                    {
                        int entityCount = entities == null ? 0 : entities.Count;
                        Logger.Log($"[{Name}] Validation failed: found {entityCount.ToString()} entities instead of 1 for incoming claim with value '{currentContext.IncomingEntity.Value}' and type '{currentContext.IncomingEntity.ClaimType}'", TraceSeverity.Unexpected, TraceCategory.Claims_Picking);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException(Name, "in FillResolve(SPClaim)", TraceCategory.Claims_Picking, ex);
                }
                finally
                {
                    this.Lock_LocalConfigurationRefresh.ExitReadLock();
                }
            }
        }
        #endregion

        #region ProcessSearchOrValidation
        protected List<PickerEntity> SearchOrValidate(OperationContext currentContext)
        {
            List<LdapEntityProviderResult> ldapSearchResults = null;
            List<PickerEntity> pickerEntityList = new List<PickerEntity>();
            try
            {
                if (this.Settings.AlwaysResolveUserInput)
                {
                    // Completely bypass query to LDAP servers
                    pickerEntityList = CreatePickerEntityForSpecificClaimTypes(
                        currentContext.Input,
                        currentContext.CurrentClaimTypeConfigList.FindAll(x => !x.IsAdditionalLdapSearchAttribute));
                    Logger.Log($"[{Name}] Created {pickerEntityList.Count} entity(ies) without contacting LDAP server(s) because property AlwaysResolveUserInput is set to true.",
                        TraceSeverity.Medium, TraceCategory.Claims_Picking);
                    return pickerEntityList;
                }

                // Create a delegate to query LDAP, so it is called only if needed
                Action SearchOrValidateInLdap = () =>
                {
                    using (new SPMonitoredScope($"[{Name}] Total time spent to query LDAP server(s)", 1000))
                    {
                        ldapSearchResults = this.EntityProvider.SearchOrValidateEntities(currentContext);
                    }
                };

                if (currentContext.OperationType == OperationType.Search)
                {
                    // Between 0 to many PickerEntity is expected by SharePoint

                    // Check if a config to bypass LDAP lookup exists
                    // ClaimTypeConfigEnsureUniquePrefixToBypassLookup ensures that collection cannot contain duplicates
                    ClaimTypeConfig ctConfigWithInputPrefixMatch = currentContext.CurrentClaimTypeConfigList.FirstOrDefault(x =>
                        !String.IsNullOrWhiteSpace(x.LeadingKeywordToBypassDirectory) &&
                        currentContext.Input.StartsWith(x.LeadingKeywordToBypassDirectory, StringComparison.InvariantCultureIgnoreCase));
                    if (ctConfigWithInputPrefixMatch != null)
                    {
                        string inputWithoutPrefix = currentContext.Input.Substring(ctConfigWithInputPrefixMatch.LeadingKeywordToBypassDirectory.Length);
                        if (String.IsNullOrEmpty(inputWithoutPrefix))
                        {
                            // No value in the input after the prefix, return
                            return pickerEntityList;
                        }
                        pickerEntityList = CreatePickerEntityForSpecificClaimTypes(
                            inputWithoutPrefix,
                            new List<ClaimTypeConfig>() { ctConfigWithInputPrefixMatch });
                        if (pickerEntityList?.Count == 1)
                        {
                            PickerEntity entity = pickerEntityList.FirstOrDefault();
                            Logger.Log($"[{Name}] Created entity without contacting Microsoft Entra ID tenant(s) because input started with prefix '{ctConfigWithInputPrefixMatch.LeadingKeywordToBypassDirectory}', which is configured for claim type '{ctConfigWithInputPrefixMatch.ClaimType}'. Claim value: '{entity.Claim.Value}', claim type: '{entity.Claim.ClaimType}'",
                                TraceSeverity.VerboseEx, TraceCategory.Claims_Picking);
                        }
                    }
                    else
                    {
                        SearchOrValidateInLdap();
                        if (ldapSearchResults?.Count > 0)
                        {
                            pickerEntityList = this.ProcessLdapResults(currentContext, ldapSearchResults);
                        }
                    }
                }
                else if (currentContext.OperationType == OperationType.Validation)
                {
                    // Exactly 1 PickerEntity is expected by SharePoint

                    // Check if config corresponding to current claim type has a config to bypass LDAP lookup
                    if (!String.IsNullOrWhiteSpace(currentContext.CurrentClaimTypeConfigList.First().LeadingKeywordToBypassDirectory))
                    {
                        // At this stage, it is impossible to know if entity was originally created with the keyword that bypass query to Microsoft Entra ID
                        // But it should be always validated since property LeadingKeywordToBypassDirectory is set for current ClaimTypeConfig, so create entity manually
                        pickerEntityList = CreatePickerEntityForSpecificClaimTypes(
                            currentContext.IncomingEntity.Value,
                            currentContext.CurrentClaimTypeConfigList);
                        if (pickerEntityList?.Count == 1)
                        {
                            PickerEntity entity = pickerEntityList.FirstOrDefault();
                            Logger.Log($"[{Name}] Validated entity without contacting Microsoft Entra ID tenant(s) because its claim type ('{currentContext.CurrentClaimTypeConfigList.First().ClaimType}') has property 'LeadingKeywordToBypassDirectory' set in EntraCPConfig.ClaimTypes. Claim value: '{entity.Claim.Value}', claim type: '{entity.Claim.ClaimType}'",
                                TraceSeverity.VerboseEx, TraceCategory.Claims_Picking);
                        }
                    }
                    else
                    {
                        SearchOrValidateInLdap();
                        // Even if >1 it must proceed, becausee multiple LDAP servers may validate the entity, and ProcessLdapResults() will eliminate duplicates
                        if (ldapSearchResults?.Count >= 1)
                        {
                            pickerEntityList = this.ProcessLdapResults(currentContext, ldapSearchResults);
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

        protected virtual List<PickerEntity> ProcessLdapResults(OperationContext currentContext, List<LdapEntityProviderResult> ldapSearchResults)
        {
            List<PickerEntity> spEntities = new List<PickerEntity>();
            ClaimsProviderEntityCollection uniqueDirectoryResults = new ClaimsProviderEntityCollection();
            IEnumerable<ClaimTypeConfig> ctConfigs = currentContext.CurrentClaimTypeConfigList;
            if (currentContext.ExactSearch)
            {
                ctConfigs = currentContext.CurrentClaimTypeConfigList.Where(x => !x.IsAdditionalLdapSearchAttribute);
            }

            foreach (LdapEntityProviderResult ldapResult in ldapSearchResults)
            {
                ResultPropertyCollection ldapResultProperties = ldapResult.DirectoryResultProperties;
                // objectclass attribute should never be missing because it is explicitely requested in LDAP query
                if (!ldapResultProperties.Contains("objectclass"))
                {
                    Logger.Log($"[{Name}] Property \"objectclass\" is missing in LDAP result, this may be due to insufficient permissions of the account connecting to LDAP server '{ldapResult.AuthorityMatch.DomainFQDN}'. Skipping result.", TraceSeverity.Unexpected, TraceCategory.Core);
                    continue;
                }

                // Cast collection to be able to use StringComparer.InvariantCultureIgnoreCase for case insensitive search of ldap properties
                IEnumerable<string> ldapResultPropertyNames = ldapResultProperties.PropertyNames.Cast<string>();

                foreach (ClaimTypeConfig ctConfig in ctConfigs)
                {
                    // Skip if: DirectoryObjectClass of current config does not match objectclass of LDAP result
                    if (!ldapResultProperties["objectclass"].Cast<string>().Contains(ctConfig.DirectoryObjectClass, StringComparer.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    // Skip if: DirectoryObjectAttribute of current config is not found in LDAP result
                    if (!ldapResultPropertyNames.Contains(ctConfig.DirectoryObjectAttribute, StringComparer.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    // Get the LDAP attribute value of the current ClaimTypeConfig
                    // Fix https://github.com/Yvand/LDAPCP/issues/43: properly test the type of the LDAP attribute's value, and always get it as a string
                    string directoryObjectPropertyValue = Utils.GetLdapValueAsString(ldapResultProperties[ldapResultPropertyNames.First(x => String.Equals(x, ctConfig.DirectoryObjectAttribute, StringComparison.InvariantCultureIgnoreCase))][0], ctConfig.DirectoryObjectAttribute);
                    string permissionClaimValue = directoryObjectPropertyValue;
                    if (String.IsNullOrWhiteSpace(directoryObjectPropertyValue))
                    {
                        continue;
                    }

                    // Check if current LDAP attribute's value matches the input
                    if (currentContext.ExactSearch || !ctConfig.DirectoryObjectAttributeSupportsWildcard)
                    {
                        if (!String.Equals(directoryObjectPropertyValue, currentContext.Input, StringComparison.InvariantCultureIgnoreCase))
                        {
                            continue;
                        }
                    }
                    else
                    {
                        if (this.Settings.AddWildcardAsPrefixOfInput)
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

                    // Check if current result (association of LDAP result + ClaimTypeConfig) is not already in uniqueDirectoryResults list
                    // Get ClaimTypeConfig to use to check if result is already present in the uniqueDirectoryResults list
                    ClaimTypeConfig ctConfigToUseForDuplicateCheck = ctConfig;
                    if (ctConfig.IsAdditionalLdapSearchAttribute)
                    {
                        if (ctConfig.DirectoryObjectType == DirectoryObjectType.User)
                        {
                            if (String.Equals(ctConfig.DirectoryObjectClass, this.Settings.UserIdentifierClaimTypeConfig.DirectoryObjectClass, StringComparison.InvariantCultureIgnoreCase))
                            {
                                ctConfigToUseForDuplicateCheck = this.Settings.UserIdentifierClaimTypeConfig;

                                // Get the permission value using the LDAP attribute of the identifier config, if it exists, and skip current LDAP result if it does not exist
                                if (!ldapResultPropertyNames.Contains(this.Settings.UserIdentifierClaimTypeConfig.DirectoryObjectAttribute, StringComparer.InvariantCultureIgnoreCase))
                                {
                                    continue;
                                }
                                else
                                {
                                    permissionClaimValue = Utils.GetLdapValueAsString(ldapResultProperties[ldapResultPropertyNames.First(x => String.Equals(x, this.Settings.UserIdentifierClaimTypeConfig.DirectoryObjectAttribute, StringComparison.InvariantCultureIgnoreCase))][0], this.Settings.UserIdentifierClaimTypeConfig.DirectoryObjectAttribute);
                                }
                            }
                            else
                            {
                                continue;  // Local ClaimTypeConfig is a user but current LDAP result is not, skip
                            }
                        }
                        else
                        {
                            if (this.Settings.GroupIdentifierClaimTypeConfig != null && String.Equals(ctConfig.DirectoryObjectClass, this.Settings.GroupIdentifierClaimTypeConfig.DirectoryObjectClass, StringComparison.InvariantCultureIgnoreCase))
                            {
                                ctConfigToUseForDuplicateCheck = this.Settings.GroupIdentifierClaimTypeConfig;

                                // Get the permission value using the LDAP attribute of the identifier config, if it exists, and skip current LDAP result if it does not exist
                                if (!ldapResultPropertyNames.Contains(this.Settings.GroupIdentifierClaimTypeConfig.DirectoryObjectAttribute, StringComparer.InvariantCultureIgnoreCase))
                                {
                                    continue;
                                }
                                else
                                {
                                    permissionClaimValue = Utils.GetLdapValueAsString(ldapResultProperties[ldapResultPropertyNames.First(x => String.Equals(x, this.Settings.GroupIdentifierClaimTypeConfig.DirectoryObjectAttribute, StringComparison.InvariantCultureIgnoreCase))][0], this.Settings.GroupIdentifierClaimTypeConfig.DirectoryObjectAttribute);
                                }
                            }
                            else
                            {
                                continue;  // Local ClaimTypeConfig is a group but current LDAP result is not, skip
                            }
                        }
                    }

                    // When token domain is present, then ensure we do compare with the actual domain name
                    bool dynamicDomainTokenSet = Utils.IsDynamicTokenSet(ctConfig.ClaimValueLeadingToken, ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME);// ? true : this.Settings.CompareResultsWithDomainNameProp;
                    if (!dynamicDomainTokenSet)
                    {
                        dynamicDomainTokenSet = Utils.IsDynamicTokenSet(ctConfig.ClaimValueLeadingToken, ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN);// ? true : this.Settings.CompareResultsWithDomainNameProp;
                    }
                    if (uniqueDirectoryResults.Contains(ldapResult, ctConfigToUseForDuplicateCheck, dynamicDomainTokenSet))
                    {
                        continue;
                    }

                    ClaimsProviderEntity uniqueLdapResult = new ClaimsProviderEntity(ldapResult, ctConfig, directoryObjectPropertyValue, permissionClaimValue);
                    spEntities.Add(CreatePickerEntityHelper(currentContext, uniqueLdapResult));
                    uniqueDirectoryResults.Add(uniqueLdapResult);
                }
            }
            Logger.Log($"[{Name}] Created {spEntities.Count} entity(ies) after filtering directory results", TraceSeverity.Verbose, TraceCategory.Core);
            return spEntities;
        }
        #endregion

        #region Helpers
        protected virtual new SPClaim CreateClaim(string type, string value, string valueType)
        {
            // SPClaimProvider.CreateClaim sets property OriginalIssuer to SPOriginalIssuerType.ClaimProvider, which is not correct
            //return CreateClaim(type, value, valueType);
            return new SPClaim(type, value, valueType, this.OriginalIssuerName);
        }

        protected PickerEntity CreatePickerEntityHelper(OperationContext currentContext, ClaimsProviderEntity result)
        {
            ClaimTypeConfig directoryObjectIdentifierConfig = result.ClaimTypeConfigMatch;
            if (result.ClaimTypeConfigMatch.IsAdditionalLdapSearchAttribute)
            {
                // Get the config to use to create the actual entity (claim type and its DirectoryObjectAttribute) from current result
                directoryObjectIdentifierConfig = result.ClaimTypeConfigMatch.DirectoryObjectType == DirectoryObjectType.User ? this.Settings.UserIdentifierClaimTypeConfig : this.Settings.GroupIdentifierClaimTypeConfig;
            }

            string permissionValue = FormatPermissionValue(currentContext, directoryObjectIdentifierConfig.ClaimType, result);
            SPClaim claim = CreateClaim(directoryObjectIdentifierConfig.ClaimType, permissionValue, directoryObjectIdentifierConfig.ClaimValueType);
            PickerEntity entity = CreatePickerEntity();
            entity.Claim = claim;
            entity.EntityType = directoryObjectIdentifierConfig.SPEntityType;
            if (String.IsNullOrWhiteSpace(entity.EntityType))
            {
                entity.EntityType = directoryObjectIdentifierConfig.DirectoryObjectType == DirectoryObjectType.User ? SPClaimEntityTypes.User : ClaimsProviderConstants.GroupClaimEntityType;
            }
            entity.IsResolved = true;
            entity.EntityGroupName = this.Name;
            entity.Description = String.Format(PickerEntityOnMouseOver, result.ClaimTypeConfigMatch.DirectoryObjectAttribute, result.DirectoryAttributeValueMatch);
            entity.DisplayText = FormatPermissionDisplayText(result.DirectoryResult, directoryObjectIdentifierConfig, permissionValue);

            int nbMetadata = 0;
            // Populate the metadata for this PickerEntity
            // Change condition to fix bug http://ldapcp.codeplex.com/discussions/653087: only rely on the LDAP class
            foreach (ClaimTypeConfig ctConfig in this.Settings.RuntimeMetadataConfig.Where(x => String.Equals(x.DirectoryObjectClass, result.ClaimTypeConfigMatch.DirectoryObjectClass, StringComparison.InvariantCultureIgnoreCase)))
            {
                // if the the LDAP result has a value for the LDAP attribute of the current metadata, then the metadata can be set
                if (result.DirectoryResult.DirectoryResultProperties.Contains(ctConfig.DirectoryObjectAttribute) && result.DirectoryResult.DirectoryResultProperties[ctConfig.DirectoryObjectAttribute].Count > 0)
                {
                    entity.EntityData[ctConfig.SPEntityDataKey] = result.DirectoryResult.DirectoryResultProperties[ctConfig.DirectoryObjectAttribute][0].ToString();
                    nbMetadata++;
                    Logger.Log($"[{Name}] Set metadata '{ctConfig.SPEntityDataKey}' of new entity to '{entity.EntityData[ctConfig.SPEntityDataKey]}'", TraceSeverity.VerboseEx, TraceCategory.Claims_Picking);
                }
            }

            Logger.Log($"[{Name}] Created entity: display text: '{entity.DisplayText}', claim value: '{entity.Claim.Value}', claim type: '{entity.Claim.ClaimType}', and filled with {nbMetadata} metadata.", TraceSeverity.VerboseEx, TraceCategory.Claims_Picking);
            return entity;
        }

        private List<PickerEntity> CreatePickerEntityForSpecificClaimTypes(string claimValue, List<ClaimTypeConfig> ctConfigs)
        {
            List<PickerEntity> entities = new List<PickerEntity>();
            foreach (var ctConfig in ctConfigs)
            {
                SPClaim claim = CreateClaim(ctConfig.ClaimType, claimValue, ctConfig.ClaimValueType);
                PickerEntity entity = CreatePickerEntity();
                entity.Claim = claim;
                entity.IsResolved = true;
                entity.EntityType = ctConfig.SPEntityType;
                if (String.IsNullOrWhiteSpace(entity.EntityType))
                {
                    entity.EntityType = ctConfig.DirectoryObjectType == DirectoryObjectType.User ? SPClaimEntityTypes.User : ClaimsProviderConstants.GroupClaimEntityType;
                }
                entity.EntityGroupName = this.Name;
                entity.Description = String.Format(PickerEntityOnMouseOver, ctConfig.DirectoryObjectAttribute, claimValue);
                entity.DisplayText = FormatPermissionDisplayText(null, ctConfig, claimValue);

                if (!String.IsNullOrWhiteSpace(ctConfig.SPEntityDataKey))
                {
                    entity.EntityData[ctConfig.SPEntityDataKey] = entity.Claim.Value;
                    Logger.Log($"[{Name}] Added metadata '{ctConfig.SPEntityDataKey}' with value '{entity.EntityData[ctConfig.SPEntityDataKey]}' to new entity", TraceSeverity.VerboseEx, TraceCategory.Claims_Picking);
                }

                entities.Add(entity);
                Logger.Log($"[{Name}] Created entity: display text: '{entity.DisplayText}', claim value: '{entity.Claim.Value}', claim type: '{entity.Claim.ClaimType}'.", TraceSeverity.VerboseEx, TraceCategory.Claims_Picking);
            }
            return entities.Count > 0 ? entities : null;
        }

        protected virtual string FormatPermissionValue(OperationContext currentContext, string claimType, ClaimsProviderEntity result)
        {
            string claimValue = result.PermissionClaimValue;
            if (result.DirectoryResult != null)
            {
                var attr = currentContext.CurrentClaimTypeConfigList.FirstOrDefault(x => SPClaimTypes.Equals(x.ClaimType, claimType));
                if (Utils.IsDynamicTokenSet(attr.ClaimValueLeadingToken, ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME))
                {
                    claimValue = string.Format("{0}{1}", attr.ClaimValueLeadingToken.Replace(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME, result.DirectoryResult.AuthorityMatch.DomainName), claimValue);
                }
                else if (Utils.IsDynamicTokenSet(attr.ClaimValueLeadingToken, ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN))
                {
                    claimValue = string.Format("{0}{1}", attr.ClaimValueLeadingToken.Replace(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN, result.DirectoryResult.AuthorityMatch.DomainFQDN), claimValue);
                }
                else
                {
                    claimValue = attr.ClaimValueLeadingToken + claimValue;
                }
            }
            return claimValue;
        }

        protected virtual string FormatPermissionDisplayText(LdapEntityProviderResult directoryResult, ClaimTypeConfig associatedClaimTypeConfig, string claimValue)
        {
            bool isUserIdentityClaimType = String.Equals(associatedClaimTypeConfig.ClaimType, this.Settings.UserIdentifierClaimTypeConfig.ClaimType, StringComparison.InvariantCultureIgnoreCase);
            string entityDisplayText = this.Settings.EntityDisplayTextPrefix;

            if (directoryResult == null)
            {
                if (isUserIdentityClaimType)
                {
                    entityDisplayText += claimValue;
                }
                else
                {
                    entityDisplayText += String.Format(PickerEntityDisplayText, associatedClaimTypeConfig.ClaimTypeDisplayName, claimValue);
                }
            }
            else
            {
                string leadingTokenValue = String.Empty;
                string ldapValueInDisplayText = claimValue;
                if (!String.IsNullOrWhiteSpace(associatedClaimTypeConfig.DirectoryObjectAttributeForDisplayText) && directoryResult.DirectoryResultProperties.Contains(associatedClaimTypeConfig.DirectoryObjectAttributeForDisplayText))
                {
                    ldapValueInDisplayText = Utils.GetLdapValueAsString(directoryResult.DirectoryResultProperties[associatedClaimTypeConfig.DirectoryObjectAttributeForDisplayText][0], associatedClaimTypeConfig.DirectoryObjectAttributeForDisplayText);

                    // not yet 100% decided if the leading token should be displayed if a specific display text attribute is set
                    //if (Utils.IsDynamicTokenSet(associatedClaimTypeConfig.ClaimValueLeadingToken, ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME))
                    //{
                    //    leadingTokenValue = string.Format("{0}", associatedClaimTypeConfig.ClaimValueLeadingToken.Replace(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME, directoryResult.AuthorityMatch.DomainName));
                    //}
                    //else if (Utils.IsDynamicTokenSet(associatedClaimTypeConfig.ClaimValueLeadingToken, ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN))
                    //{
                    //    leadingTokenValue = string.Format("{0}", associatedClaimTypeConfig.ClaimValueLeadingToken.Replace(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN, directoryResult.AuthorityMatch.DomainFQDN));
                    //}
                    //else
                    //{
                    //    leadingTokenValue = associatedClaimTypeConfig.ClaimValueLeadingToken;
                    //}
                }

                ldapValueInDisplayText = leadingTokenValue + ldapValueInDisplayText;
                if (!isUserIdentityClaimType && associatedClaimTypeConfig.ShowClaimNameInDisplayText)
                {
                    entityDisplayText += String.Format(PickerEntityDisplayText, associatedClaimTypeConfig.ClaimTypeDisplayName, ldapValueInDisplayText);
                }
                else
                {
                    entityDisplayText += ldapValueInDisplayText;
                }
            }
            return entityDisplayText;
        }

        public virtual void ValidateRuntimeSettings(OperationContext operationContext)
        {
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
                    TraceSeverity.VerboseEx, TraceCategory.Rehydration);
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

                    foreach (var claimTypeSettings in ((ClaimsProviderSettings)this.Settings).RuntimeClaimTypeConfigList)
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
                    foreach (var azureObject in ((ClaimsProviderSettings)this.Settings).RuntimeClaimTypeConfigList.FindAll(x => !x.IsAdditionalLdapSearchAttribute && aadEntityTypes.Contains(x.DirectoryObjectType)))
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
