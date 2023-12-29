using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
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
        protected virtual string PickerEntityOnMouseOver => "{0}={1}";

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
                    //LDAPResult.PickerEntity = CreatePickerEntityHelper(LDAPResult);;
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
