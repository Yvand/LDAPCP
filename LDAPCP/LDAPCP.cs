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
using WIF = System.Security.Claims;

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

        public virtual string PersistedObjectName => Constants.LDAPCPCONFIG_NAME;

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
        protected AttributeHelper IdentityClaimTypeConfig;

        /// <summary>
        /// Contains attributes that are not used in the filter (both ClaimTypeProp AND CreateAsIdentityClaim are not set), but have EntityDataKey set
        /// </summary>
        protected List<AttributeHelper> UserMetadataClaimTypeConfigList;

        /// <summary>
        /// SPTrust associated with the claims provider
        /// </summary>
        protected SPTrustedLoginProvider SPTrust;

        /// <summary>
        /// List of attributes actually defined in the trust
        /// + list of LDAP attributes that are always queried, even if not defined in the trust (typically the displayName)
        /// </summary>
        private List<AttributeHelper> ProcessedClaimTypesConfig;

        protected virtual string LDAPObjectClassName { get { return "objectclass"; } }
        protected virtual string LDAPFilter { get { return "(&(" + LDAPObjectClassName + "={2})({0}={1}){3}) "; } }
        protected virtual string PickerEntityDisplayText { get { return "({0}) {1}"; } }
        protected virtual string PickerEntityOnMouseOver { get { return "{0}={1}"; } }
        protected virtual string EnabledUsersOnlyLDAPFilter { get { return "(&(!(userAccountControl:1.2.840.113556.1.4.803:=2))"; } }
        protected virtual string FilterSecurityGroupsOnlyLDAPFilter { get { return "(groupType:1.2.840.113556.1.4.803:=2147483648)"; } }

        protected string IssuerName
        {
            get
            {
                // The benefit of using the SPTrustedLoginProvider name for the issuer name is that it makes easy to replace or remove LDAPCP.
                return SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, SPTrust.Name);
            }
        }

        public LDAPCP(string displayName) : base(displayName)
        {
        }

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
                        LdapcpLogging.Log($"[{ProviderInternalName}] PersistedObject '{PersistedObjectName}' was not found. Visit LDAPCP admin pages in central administration to create it.",
                            TraceSeverity.Verbose, EventSeverity.Information, LdapcpLogging.Categories.Core);
                        // Create a fake persisted object just to get the default settings, it will not be saved in config database
                        globalConfiguration = LDAPCPConfig.GetDefaultConfiguration(PersistedObjectName);
                        refreshConfig = true;
                    }
                    else if (globalConfiguration.ClaimTypesConfigList == null || globalConfiguration.ClaimTypesConfigList.Count == 0)
                    {
                        LdapcpLogging.Log($"[{ProviderInternalName}] PersistedObject '{PersistedObjectName}' was found but there are no Attribute set. Visit AzureCP admin pages in central administration to create it.",
                            TraceSeverity.Unexpected, EventSeverity.Error, LdapcpLogging.Categories.Core);
                        // Cannot continue 
                        success = false;
                    }
                    else if (globalConfiguration.LDAPConnectionsProp == null || globalConfiguration.LDAPConnectionsProp.Count == 0)
                    {
                        LdapcpLogging.Log($"[{ProviderInternalName}] PersistedObject '{PersistedObjectName}' was found but there are no LDAP connection set. Visit AzureCP admin pages in central administration to create it.",
                            TraceSeverity.Unexpected, EventSeverity.Error, LdapcpLogging.Categories.Core);
                        // Cannot continue 
                        success = false;
                    }
                    else
                    {
                        // Persisted object is found and seems valid
                        LdapcpLogging.LogDebug($"[{ProviderInternalName}] PersistedObject '{PersistedObjectName}' was found, version: {((SPPersistedObject)globalConfiguration).Version.ToString()}, previous version: {this.LdapcpConfigVersion.ToString()}");
                        if (this.LdapcpConfigVersion != ((SPPersistedObject)globalConfiguration).Version)
                        {
                            refreshConfig = true;
                            this.LdapcpConfigVersion = ((SPPersistedObject)globalConfiguration).Version;
                            LdapcpLogging.Log($"[{ProviderInternalName}] PersistedObject '{PersistedObjectName}' was changed, refreshing configuration",
                                TraceSeverity.Medium, EventSeverity.Information, LdapcpLogging.Categories.Core);
                        }
                    }
                    if (this.ProcessedClaimTypesConfig == null) refreshConfig = true;
                }
                catch (Exception ex)
                {
                    success = false;
                    LdapcpLogging.LogException(ProviderInternalName, "in Initialize", LdapcpLogging.Categories.Core, ex);
                }
                finally
                {
                    // ProcessedAttributes can be null if:
                    // - 1st initialization
                    // - Initialized before but it failed. If so, try again to refresh config
                    if (this.ProcessedClaimTypesConfig == null) refreshConfig = true;

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
                    LdapcpLogging.Log($"[{ProviderInternalName}] Refreshing configuration from PersistedObject '{PersistedObjectName}'...",
                        TraceSeverity.Verbose, EventSeverity.Information, LdapcpLogging.Categories.Core);

                    // Create local version of the persisted object, that will never be saved in config DB
                    // This copy is unique to current object instance to avoid thread safety issues
                    this.CurrentConfiguration = new LDAPCPConfig();
                    this.CurrentConfiguration.BypassLDAPLookup = globalConfiguration.BypassLDAPLookup;
                    this.CurrentConfiguration.AddWildcardAsPrefixOfInput = globalConfiguration.AddWildcardAsPrefixOfInput;
                    this.CurrentConfiguration.PickerEntityGroupNameProp = globalConfiguration.PickerEntityGroupNameProp;
                    this.CurrentConfiguration.DisplayLdapMatchForIdentityClaimTypeProp = globalConfiguration.DisplayLdapMatchForIdentityClaimTypeProp;
                    this.CurrentConfiguration.FilterEnabledUsersOnlyProp = globalConfiguration.FilterEnabledUsersOnlyProp;
                    this.CurrentConfiguration.FilterSecurityGroupsOnlyProp = globalConfiguration.FilterSecurityGroupsOnlyProp;
                    this.CurrentConfiguration.FilterExactMatchOnlyProp = globalConfiguration.FilterExactMatchOnlyProp;
                    this.CurrentConfiguration.LDAPQueryTimeout = globalConfiguration.LDAPQueryTimeout;
                    this.CurrentConfiguration.EnableAugmentation = globalConfiguration.EnableAugmentation;
                    this.CurrentConfiguration.ClaimTypeUsedForAugmentation = globalConfiguration.ClaimTypeUsedForAugmentation;
                    this.CurrentConfiguration.ClaimTypesConfigList = new List<AttributeHelper>();
                    foreach (AttributeHelper currentObject in globalConfiguration.ClaimTypesConfigList)
                    {
                        this.CurrentConfiguration.ClaimTypesConfigList.Add(currentObject.CopyPersistedProperties());
                    }
                    this.CurrentConfiguration.LDAPConnectionsProp = new List<LDAPConnection>();
                    foreach (LDAPConnection currentCoco in globalConfiguration.LDAPConnectionsProp)
                    {
                        this.CurrentConfiguration.LDAPConnectionsProp.Add(currentCoco.CopyPersistedProperties());
                    }

                    SetCustomConfiguration(context, entityTypes);
                    if (this.CurrentConfiguration.ClaimTypesConfigList == null)
                    {
                        // this.CurrentConfiguration.AttributesListProp was set to null in SetCustomConfiguration, which is bad
                        LdapcpLogging.Log(String.Format("[{0}] AttributesListProp was not set to null in SetCustomConfiguration, don't set it or set it with actual entries.", ProviderInternalName), TraceSeverity.Unexpected, EventSeverity.Error, LdapcpLogging.Categories.Core);
                        return false;
                    }
                    success = InitializeClaimTypeConfigList(this.CurrentConfiguration.ClaimTypesConfigList);
                }
                catch (Exception ex)
                {
                    success = false;
                    LdapcpLogging.LogException(ProviderInternalName, "in Initialize, while refreshing configuration", LdapcpLogging.Categories.Core, ex);
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
        private bool InitializeClaimTypeConfigList(List<AttributeHelper> nonProcessedClaimTypeConfigList)
        {
            bool success = true;
            try
            {
                bool identityClaimTypeFound = false;
                // Get attributes defined in trust based on their claim type (unique way to map them)
                List<AttributeHelper> claimTypesDefinedInTrust = new List<AttributeHelper>();
                // There is a bug in the SharePoint API: SPTrustedLoginProvider.ClaimTypes should retrieve SPTrustedClaimTypeInformation.MappedClaimType, but it returns SPTrustedClaimTypeInformation.InputClaimType instead, so we cannot rely on it
                //foreach (var attr in attributeHelperCollection.Where(x => SPTrust.ClaimTypes.Contains(x.ClaimTypeProp)))
                //{
                //    attributesDefinedInTrust.Add(attr);
                //}
                foreach (SPTrustedClaimTypeInformation claimTypeInformation in SPTrust.ClaimTypeInformation)
                {
                    //attributesDefinedInTrust.Add(attributeHelperCollection.First(x => String.Equals(x.ClaimTypeProp, ClaimTypeInformation.MappedClaimType, StringComparison.InvariantCultureIgnoreCase) && !x.CreateAsIdentityClaim));
                    List<AttributeHelper> attObjectColl = nonProcessedClaimTypeConfigList.FindAll(x =>
                        String.Equals(x.ClaimType, claimTypeInformation.MappedClaimType, StringComparison.InvariantCultureIgnoreCase) &&
                        !x.CreateAsIdentityClaim &&
                        !String.IsNullOrEmpty(x.LDAPAttribute) &&
                        !String.IsNullOrEmpty(x.LDAPObjectClassProp));
                    AttributeHelper att;
                    if (attObjectColl.Count == 1)
                    {
                        att = attObjectColl.First();
                        claimTypesDefinedInTrust.Add(att);

                        if (String.Equals(SPTrust.IdentityClaimTypeInformation.MappedClaimType, att.ClaimType, StringComparison.InvariantCultureIgnoreCase))
                        {
                            // Identity claim type found, set IdentityAttribute property
                            identityClaimTypeFound = true;
                            IdentityClaimTypeConfig = att;
                        }
                    }
                }

                // Make sure that the identity claim is in this collection. Should always check property SPTrustedClaimTypeInformation.MappedClaimType: http://msdn.microsoft.com/en-us/library/microsoft.sharepoint.administration.claims.sptrustedclaimtypeinformation.mappedclaimtype.aspx
                if (!identityClaimTypeFound)
                {
                    LdapcpLogging.Log(String.Format("[{0}] Impossible to continue because identity claim type \"{1}\" set in the SPTrustedIdentityTokenIssuer \"{2}\" is missing in the LDAPCP claim types list. Add it from central admin > security > claims mapping page", ProviderInternalName, SPTrust.IdentityClaimTypeInformation.MappedClaimType, SPTrust.Name), TraceSeverity.Unexpected, EventSeverity.ErrorCritical, LdapcpLogging.Categories.Core);
                    return false;
                }

                // Check if there are attributes that should be always queried (CreateAsIdentityClaim) to add in the list
                List<AttributeHelper> additionalClaimTypes = new List<AttributeHelper>();
                foreach (var attr in nonProcessedClaimTypeConfigList.Where(x => x.CreateAsIdentityClaim && !claimTypesDefinedInTrust.Contains(x, new LDAPPropertiesComparer())))
                {
                    if (String.Equals(SPTrust.IdentityClaimTypeInformation.MappedClaimType, attr.ClaimType))
                    {
                        // Not a big deal since it's set with identity claim type, so no inconsistent behavior to expect, just record an information
                        LdapcpLogging.Log(String.Format("[{0}] Object with LDAP attribute/class {1}/{2} is set with CreateAsIdentityClaim to true and ClaimTypeProp {3}. Remove ClaimTypeProp property as it is useless.", ProviderInternalName, attr.LDAPAttribute, attr.LDAPObjectClassProp, attr.ClaimType), TraceSeverity.Monitorable, EventSeverity.Information, LdapcpLogging.Categories.Core);
                    }
                    else if (claimTypesDefinedInTrust.Count(x => String.Equals(x.ClaimType, attr.ClaimType)) > 0)
                    {
                        // Same claim type already exists with CreateAsIdentityClaim == false. 
                        // Current object is a bad one and shouldn't be added. Don't add it but continue to build objects list
                        LdapcpLogging.Log(String.Format("[{0}] Claim type {1} is defined twice with CreateAsIdentityClaim set to true and false, which is invalid. Remove entry with CreateAsIdentityClaim set to true.", ProviderInternalName, attr.ClaimType), TraceSeverity.Monitorable, EventSeverity.Information, LdapcpLogging.Categories.Core);
                        continue;
                    }

                    if (attr.LDAPObjectClassProp == IdentityClaimTypeConfig.LDAPObjectClassProp)
                    {
                        // Attribute will be populated with identity claim type information
                        attr.ClaimType = SPTrust.IdentityClaimTypeInformation.MappedClaimType;
                        attr.ClaimEntityType = SPClaimEntityTypes.User;
                        attr.LDAPAttributeToDisplayProp = IdentityClaimTypeConfig.LDAPAttributeToDisplayProp; // Must be set otherwise display text of permissions will be inconsistent
                    }
                    else
                    {
                        // Attribute will be populated with first attribute that matches the LDAP class (and !CreateAsIdentityClaim)
                        var attrReference = claimTypesDefinedInTrust.FirstOrDefault(x => x.LDAPObjectClassProp == attr.LDAPObjectClassProp);
                        if (attrReference != null)
                        {
                            attr.ClaimType = attrReference.ClaimType;
                            attr.ClaimEntityType = attrReference.ClaimEntityType;
                            attr.ShowClaimNameInDisplayText = attrReference.ShowClaimNameInDisplayText;
                        }
                        else
                        {
                            LdapcpLogging.Log(String.Format("[{0}] Entry with LDAP class {1} is defined but it doesn't match any entry with the same LDAP class and a claim type defined. Add an entry with same LDAP object class and a claim type to fix this issue.", ProviderInternalName, attr.LDAPObjectClassProp), TraceSeverity.Monitorable, EventSeverity.Information, LdapcpLogging.Categories.Core);
                            continue;
                        }
                    }
                    additionalClaimTypes.Add(attr);
                }

                this.ProcessedClaimTypesConfig = new List<AttributeHelper>(claimTypesDefinedInTrust.Count + additionalClaimTypes.Count);
                this.ProcessedClaimTypesConfig.AddRange(claimTypesDefinedInTrust);
                this.ProcessedClaimTypesConfig.AddRange(additionalClaimTypes);

                // Parse each attribute to configure its settings from the corresponding claim types defined in the SPTrustedLoginProvider
                foreach (var attr in this.ProcessedClaimTypesConfig)
                {
                    var trustedClaim = SPTrust.GetClaimTypeInformationFromMappedClaimType(attr.ClaimType);
                    // It should never be null
                    if (trustedClaim == null) continue;
                    attr.ClaimTypeMappingName = trustedClaim.DisplayName;
                }

                // Any metadata for a user with at least an LDAP attribute and a LDAP class is valid
                this.UserMetadataClaimTypeConfigList = nonProcessedClaimTypeConfigList.FindAll(x =>
                    !String.IsNullOrEmpty(x.EntityDataKey) &&
                    !String.IsNullOrEmpty(x.LDAPAttribute) &&
                    !String.IsNullOrEmpty(x.LDAPObjectClassProp));// &&                    x.ClaimEntityType == SPClaimEntityTypes.User);
            }
            catch (Exception ex)
            {
                LdapcpLogging.LogException(ProviderInternalName, "in InitializeClaimTypeConfigList", LdapcpLogging.Categories.Core, ex);
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
                LdapcpLogging.Log(String.Format("[{0}] Claims provider {0} is associated to multiple SPTrustedIdentityTokenIssuer, which is not supported because at runtime there is no way to determine what TrustedLoginProvider is currently calling", ProviderInternalName), TraceSeverity.Unexpected, EventSeverity.Error, LdapcpLogging.Categories.Core);

            LdapcpLogging.Log(String.Format("[{0}] Claims provider {0} is not associated with any SPTrustedIdentityTokenIssuer so it cannot create permissions.\r\nVisit http://ldapcp.codeplex.com for installation procedure or set property ClaimProviderName with PowerShell cmdlet Get-SPTrustedIdentityTokenIssuer to create association.", ProviderInternalName), TraceSeverity.High, EventSeverity.Warning, LdapcpLogging.Categories.Core);
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
            LdapcpLogging.LogDebug(String.Format("[{0}] FillResolve(SPClaim) called, incoming claim value: \"{1}\", claim type: \"{2}\", claim issuer: \"{3}\"", ProviderInternalName, resolveInput.Value, resolveInput.ClaimType, resolveInput.OriginalIssuer));

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
                    RequestInformation infos = new RequestInformation(CurrentConfiguration, RequestType.Validation, ProcessedClaimTypesConfig, resolveInput.Value, resolveInput, context, entityTypes, null, Int32.MaxValue);
                    List<PickerEntity> permissions = SearchOrValidate(infos);
                    if (permissions.Count == 1)
                    {
                        resolved.Add(permissions[0]);
                        LdapcpLogging.Log(String.Format("[{0}] Validated permission: claim value: \"{1}\", claim type: \"{2}\"", ProviderInternalName, permissions[0].Claim.Value, permissions[0].Claim.ClaimType),
                            TraceSeverity.Medium, EventSeverity.Information, LdapcpLogging.Categories.Claims_Picking);
                    }
                    else
                    {
                        LdapcpLogging.Log(String.Format("[{0}] Validation of incoming claim returned {1} permissions instead of 1 expected. Aborting operation", ProviderInternalName, permissions.Count.ToString()), TraceSeverity.Unexpected, EventSeverity.Error, LdapcpLogging.Categories.Claims_Picking);
                    }
                }
                catch (Exception ex)
                {
                    LdapcpLogging.LogException(ProviderInternalName, "in FillResolve(SPClaim)", LdapcpLogging.Categories.Claims_Picking, ex);
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
            LdapcpLogging.LogDebug(String.Format("[{0}] FillResolve(string) called, incoming input \"{1}\"", ProviderInternalName, resolveInput));

            SPSecurity.RunWithElevatedPrivileges(delegate ()
            {
                if (!Initialize(context, entityTypes))
                    return;

                this.Lock_Config.EnterReadLock();
                try
                {
                    RequestInformation settings = new RequestInformation(CurrentConfiguration, RequestType.Search, ProcessedClaimTypesConfig, resolveInput, null, context, entityTypes, null, Int32.MaxValue);
                    List<PickerEntity> permissions = SearchOrValidate(settings);
                    FillPermissions(context, entityTypes, resolveInput, ref permissions);
                    foreach (PickerEntity permission in permissions)
                    {
                        resolved.Add(permission);
                        LdapcpLogging.Log(String.Format("[{0}] Added permission: claim value: \"{1}\", claim type: \"{2}\"", ProviderInternalName, permission.Claim.Value, permission.Claim.ClaimType),
                            TraceSeverity.Medium, EventSeverity.Information, LdapcpLogging.Categories.Claims_Picking);
                    }
                }
                catch (Exception ex)
                {
                    LdapcpLogging.LogException(ProviderInternalName, "in FillResolve(string)", LdapcpLogging.Categories.Claims_Picking, ex);
                }
                finally
                {
                    this.Lock_Config.ExitReadLock();
                }
            });
        }

        protected override void FillSearch(Uri context, string[] entityTypes, string searchPattern, string hierarchyNodeID, int maxCount, Microsoft.SharePoint.WebControls.SPProviderHierarchyTree searchTree)
        {
            LdapcpLogging.LogDebug(String.Format("[{0}] FillSearch called, incoming input: \"{1}\"", ProviderInternalName, searchPattern));

            SPSecurity.RunWithElevatedPrivileges(delegate ()
            {
                if (!Initialize(context, entityTypes))
                    return;

                this.Lock_Config.EnterReadLock();
                try
                {
                    RequestInformation settings = new RequestInformation(CurrentConfiguration, RequestType.Search, ProcessedClaimTypesConfig, searchPattern, null, context, entityTypes, hierarchyNodeID, maxCount);
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
                            AttributeHelper attrHelper = ProcessedClaimTypesConfig.FirstOrDefault(x =>
                                !x.CreateAsIdentityClaim &&
                                String.Equals(x.ClaimType, permission.Claim.ClaimType, StringComparison.InvariantCultureIgnoreCase));

                            string nodeName = attrHelper != null ? attrHelper.ClaimTypeMappingName : permission.Claim.ClaimType;
                            matchNode = new SPProviderHierarchyNode(_ProviderInternalName, nodeName, permission.Claim.ClaimType, true);
                            searchTree.AddChild(matchNode);
                        }
                        matchNode.AddEntity(permission);
                        LdapcpLogging.Log(String.Format("[{0}] Added permission: claim value: \"{1}\", claim type: \"{2}\"", ProviderInternalName, permission.Claim.Value, permission.Claim.ClaimType),
                            TraceSeverity.Medium, EventSeverity.Information, LdapcpLogging.Categories.Claims_Picking);
                    }
                }
                catch (Exception ex)
                {
                    LdapcpLogging.LogException(ProviderInternalName, "in FillSearch", LdapcpLogging.Categories.Claims_Picking, ex);
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
        protected virtual List<PickerEntity> SearchOrValidate(RequestInformation requestInfo)
        {
            List<PickerEntity> permissions = new List<PickerEntity>();
            try
            {
                if (this.CurrentConfiguration.BypassLDAPLookup)
                {
                    // Completely bypass LDAP lookp
                    List<PickerEntity> entities = CreatePickerEntityForSpecificClaimTypes(
                        requestInfo.Input,
                        requestInfo.ClaimTypesConfigList.FindAll(x => !x.CreateAsIdentityClaim),
                        false);
                    if (entities != null)
                    {
                        foreach (var entity in entities)
                        {
                            permissions.Add(entity);
                            LdapcpLogging.Log(String.Format("[{0}] Added permission created without LDAP lookup because LDAPCP configured to always resolve input: claim value: {1}, claim type: \"{2}\"", ProviderInternalName, entity.Claim.Value, entity.Claim.ClaimType),
                                TraceSeverity.Verbose, EventSeverity.Information, LdapcpLogging.Categories.Claims_Picking);
                        }
                    }
                    return permissions;
                }

                if (requestInfo.RequestType == RequestType.Search)
                {
                    List<AttributeHelper> attribsMatchInputPrefix = requestInfo.ClaimTypesConfigList.FindAll(x =>
                        !String.IsNullOrEmpty(x.PrefixToBypassLookup) &&
                        requestInfo.Input.StartsWith(x.PrefixToBypassLookup, StringComparison.InvariantCultureIgnoreCase));
                    if (attribsMatchInputPrefix.Count > 0)
                    {
                        // Input has a prefix, so it should be validated with no lookup
                        AttributeHelper attribMatchInputPrefix = attribsMatchInputPrefix.First();
                        if (attribsMatchInputPrefix.Count > 1)
                        {
                            // Multiple attributes have same prefix, which is not allowed
                            LdapcpLogging.Log(String.Format("[{0}] Multiple attributes have same prefix ({1}), which is not allowed.", ProviderInternalName, attribMatchInputPrefix.PrefixToBypassLookup), TraceSeverity.Unexpected, EventSeverity.Error, LdapcpLogging.Categories.Claims_Picking);
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
                            LdapcpLogging.Log(String.Format("[{0}] Added permission created without LDAP lookup because input matches a keyword: claim value: \"{1}\", claim type: \"{2}\"", ProviderInternalName, entity.Claim.Value, entity.Claim.ClaimType),
                                TraceSeverity.Verbose, EventSeverity.Information, LdapcpLogging.Categories.Claims_Picking);
                            return permissions;
                        }
                    }
                    SearchOrValidateWithLDAP(requestInfo, ref permissions);
                }
                else if (requestInfo.RequestType == RequestType.Validation)
                {
                    SearchOrValidateWithLDAP(requestInfo, ref permissions);
                    if (!String.IsNullOrEmpty(requestInfo.IdentityClaimTypeConfig.PrefixToBypassLookup))
                    {
                        // At this stage, it is impossible to know if input was originally created with the keyword that bypasses LDAP lookup
                        // But it should be validated anyway since keyword is set for this claim type
                        // If previous LDAP lookup found the permission, return it as is
                        if (permissions.Count == 1) return permissions;

                        // If we don't get exactly 1 permission, create it manually
                        PickerEntity entity = CreatePickerEntityForSpecificClaimType(
                            requestInfo.Input,
                            requestInfo.IdentityClaimTypeConfig,
                            requestInfo.InputHasKeyword);
                        if (entity != null)
                        {
                            permissions.Add(entity);
                            LdapcpLogging.Log(String.Format("[{0}] Added permission without LDAP lookup because corresponding claim type has a keyword associated. Claim value: \"{1}\", Claim type: \"{2}\"", ProviderInternalName, entity.Claim.Value, entity.Claim.ClaimType),
                                TraceSeverity.Verbose, EventSeverity.Information, LdapcpLogging.Categories.Claims_Picking);
                        }
                        return permissions;
                    }
                }
            }
            catch (Exception ex)
            {
                LdapcpLogging.LogException(ProviderInternalName, "in SearchOrValidate", LdapcpLogging.Categories.Claims_Picking, ex);
            }
            return permissions;
        }

        /// <summary>
        /// Search and validate requests coming from SharePoint with LDAP lookup
        /// </summary>
        /// <param name="requestInfo">Information about current context and operation</param>
        /// <param name="permissions"></param>
        /// <returns></returns>
        protected virtual void SearchOrValidateWithLDAP(RequestInformation requestInfo, ref List<PickerEntity> permissions)
        {
            //DirectoryEntry[] directories = GetLDAPServers(requestInfo);
            LDAPConnection[] connections = GetLDAPServers(requestInfo);
            if (connections == null || connections.Length == 0)
            {
                LdapcpLogging.Log(String.Format("[{0}] No LDAP server is configured.", ProviderInternalName), TraceSeverity.Unexpected, EventSeverity.Error, LdapcpLogging.Categories.Configuration);
                return;
            }

            List<LDAPConnectionSettings> LDAPServers = new List<LDAPConnectionSettings>(connections.Length);
            foreach (LDAPConnection coco in connections)
            {
                LDAPServers.Add(new LDAPConnectionSettings() { Directory = coco.directoryEntry });
            }
            GetLDAPFilter(requestInfo, ref LDAPServers);

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
                if (requestInfo.RequestType == RequestType.Validation
                    && requestInfo.IdentityClaimTypeConfig.PrefixToAddToValueReturnedProp != null)
                {
                    // Extract just the domain from the input
                    bool tokenFound = false;
                    string domainOnly = String.Empty;
                    if (requestInfo.IdentityClaimTypeConfig.PrefixToAddToValueReturnedProp.Contains(Constants.LDAPCPCONFIG_TOKENDOMAINNAME))
                    {
                        tokenFound = true;
                        domainOnly = RequestInformation.GetDomainFromFullAccountName(requestInfo.IncomingEntity.Value);
                    }
                    else if (requestInfo.IdentityClaimTypeConfig.PrefixToAddToValueReturnedProp.Contains(Constants.LDAPCPCONFIG_TOKENDOMAINFQDN))
                    {
                        tokenFound = true;
                        string fqdn = RequestInformation.GetDomainFromFullAccountName(requestInfo.IncomingEntity.Value);
                        domainOnly = RequestInformation.GetFirstSubString(fqdn, ".");
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
                    LdapcpLogging.Log(String.Format("[{0}] Added permission created with LDAP lookup: claim value: \"{1}\", claim type: \"{2}\"", ProviderInternalName, result.PickerEntity.Claim.Value, result.PickerEntity.Claim.ClaimType),
                        TraceSeverity.Verbose, EventSeverity.Information, LdapcpLogging.Categories.Claims_Picking);
                }
            }
        }

        /// <summary>
        /// Processes LDAP results stored in LDAPSearchResultWrappers and returns result in parameter results
        /// </summary>
        /// <param name="requestInfo">Information about current context and operation</param>
        /// <param name="LDAPSearchResultWrappers"></param>
        /// <returns></returns>
        protected virtual ConsolidatedResultCollection ProcessLdapResults(RequestInformation requestInfo, ref List<LDAPSearchResultWrapper> LDAPSearchResultWrappers)
        {
            ConsolidatedResultCollection results = new ConsolidatedResultCollection();
            ResultPropertyCollection resultPropertyCollection;
            List<AttributeHelper> attributes;
            // If exactSearch is true, we don't care about attributes with CreateAsIdentityClaim = true
            if (requestInfo.ExactSearch) attributes = requestInfo.ClaimTypesConfigList.FindAll(x => !x.CreateAsIdentityClaim);
            else attributes = requestInfo.ClaimTypesConfigList;

            foreach (LDAPSearchResultWrapper LDAPresult in LDAPSearchResultWrappers)
            {
                resultPropertyCollection = LDAPresult.SearchResult.Properties;
                // objectclass attribute should never be missing because it is explicitely requested in LDAP query
                if (!resultPropertyCollection.Contains(LDAPObjectClassName))
                {
                    LdapcpLogging.Log(String.Format("[{0}] Property \"{1}\" is missing in LDAP result, this is probably due to insufficient permissions of account doing query in LDAP server {2}.", ProviderInternalName, LDAPObjectClassName, LDAPresult.DomainFQDN), TraceSeverity.Unexpected, EventSeverity.Error, LdapcpLogging.Categories.LDAP_Lookup);
                    continue;
                }

                // Cast collection to be able to use StringComparer.InvariantCultureIgnoreCase for case insensitive search of ldap properties
                var resultPropertyCollectionPropertyNames = resultPropertyCollection.PropertyNames.Cast<string>();

                // Issue https://github.com/Yvand/LDAPCP/issues/16: Ensure identity attribute exists in current LDAP result
                if (resultPropertyCollection[LDAPObjectClassName].Cast<string>().Contains(IdentityClaimTypeConfig.LDAPObjectClassProp, StringComparer.InvariantCultureIgnoreCase))
                {
                    // This is a user: check if his identity LDAP attribute (e.g. mail or sAMAccountName) is present
                    if (!resultPropertyCollectionPropertyNames.Contains(IdentityClaimTypeConfig.LDAPAttribute, StringComparer.InvariantCultureIgnoreCase))
                    {
                        LdapcpLogging.Log(String.Format("[{0}] A user was ignored because it is missing the identity attribute '{1}'", ProviderInternalName, IdentityClaimTypeConfig.LDAPAttribute), TraceSeverity.Medium, EventSeverity.Information, LdapcpLogging.Categories.LDAP_Lookup);
                        continue;
                    }
                }
                else
                {
                    // This is a group: get the identity attribute of groups, and ensure it is present
                    var groupAttribute = attributes.FirstOrDefault(x => resultPropertyCollection[LDAPObjectClassName].Contains(x.LDAPObjectClassProp) && x.ClaimType != null);
                    if (groupAttribute != null && !resultPropertyCollectionPropertyNames.Contains(groupAttribute.LDAPAttribute, StringComparer.InvariantCultureIgnoreCase))
                    {
                        LdapcpLogging.Log(String.Format("[{0}] A group was ignored because it is missing the identity attribute '{1}'", ProviderInternalName, groupAttribute.LDAPAttribute), TraceSeverity.Medium, EventSeverity.Information, LdapcpLogging.Categories.LDAP_Lookup);
                        continue;
                    }
                }

                foreach (var attr in attributes)
                {
                    // Check if current attribute object class matches the current LDAP result
                    if (!resultPropertyCollection[LDAPObjectClassName].Cast<string>().Contains(attr.LDAPObjectClassProp, StringComparer.InvariantCultureIgnoreCase)) continue;

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
                    AttributeHelper objCompare;
                    if (attr.CreateAsIdentityClaim && (String.Equals(attr.LDAPObjectClassProp, IdentityClaimTypeConfig.LDAPObjectClassProp, StringComparison.InvariantCultureIgnoreCase)))
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
                    bool compareWithDomain = HasPrefixToken(attr.PrefixToAddToValueReturnedProp, Constants.LDAPCPCONFIG_TOKENDOMAINNAME) ? true : this.CurrentConfiguration.CompareResultsWithDomainNameProp;
                    if (!compareWithDomain) compareWithDomain = HasPrefixToken(attr.PrefixToAddToValueReturnedProp, Constants.LDAPCPCONFIG_TOKENDOMAINFQDN) ? true : this.CurrentConfiguration.CompareResultsWithDomainNameProp;
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
            LdapcpLogging.Log(String.Format("[{0}] {1} permission(s) to create after filtering", ProviderInternalName, results.Count), TraceSeverity.Medium, EventSeverity.Information, LdapcpLogging.Categories.LDAP_Lookup);
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
        /// <param name="requestInfo">Information about current context and operation</param>
        /// <param name="LDAPServers">List to be populated by this method</param>
        protected virtual void GetLDAPFilter(RequestInformation requestInfo, ref List<LDAPConnectionSettings> LDAPServers)
        {
            string filter = GetLDAPFilterForCurrentRequestInfo(requestInfo);
            foreach (LDAPConnectionSettings ldapServer in LDAPServers)
            {
                ldapServer.Filter = filter.ToString();
            }
        }

        /// <summary>
        /// Returns the LDAP filter based on settings provided
        /// </summary>
        /// <param name="requestInfo">Information about current context and operation</param>
        /// <returns>LDAP filter created from settings provided</returns>
        protected string GetLDAPFilterForCurrentRequestInfo(RequestInformation requestInfo)
        {
            // Build LDAP filter as documented in http://technet.microsoft.com/fr-fr/library/aa996205(v=EXCHG.65).aspx
            StringBuilder filter = new StringBuilder();
            if (this.CurrentConfiguration.FilterEnabledUsersOnlyProp) filter.Append(EnabledUsersOnlyLDAPFilter);
            filter.Append("(| ");

            string searchPattern;
            string input = requestInfo.Input;
            if (requestInfo.ExactSearch) searchPattern = input;
            else searchPattern = this.CurrentConfiguration.AddWildcardAsPrefixOfInput ? "*" + input + "*" : input + "*";

            foreach (var attribute in requestInfo.ClaimTypesConfigList)
            {
                // TEMP TEST TO CHECK IF IT'S POSSIBLE to use a LDAP attribute only in validation, and not in search
                // To be implemented it would need an additional AttributeHelper like "UseOnlyForValidation" or "DoNotUserForSearch"
                /*if (requestInfo.RequestType == RequestType.Search &&
                    attribute.ClaimType == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role" &&
                    !attribute.CreateAsIdentityClaim) continue;*/
                filter.Append(AddAttributeToFilter(attribute, searchPattern));
            }
            if (this.CurrentConfiguration.FilterEnabledUsersOnlyProp) filter.Append(")");
            filter.Append(")");

            return filter.ToString();
        }

        /// <summary>
        /// Query LDAP servers in parallel
        /// </summary>
        /// <param name="LDAPServers">LDAP servers to query</param>
        /// <param name="requestInfo">Information about current context and operation</param>
        /// <param name="LDAPSearchResults">LDAP search results list to be populated by this method</param>
        /// <returns>true if a result was found</returns>
        protected bool QueryLDAPServers(List<LDAPConnectionSettings> LDAPServers, RequestInformation requestInfo, ref List<LDAPSearchResultWrapper> LDAPSearchResults)
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
                    LdapcpLogging.Log(String.Format("[{0}] Skipping query on LDAP Server \"{1}\" because it doesn't have any filter, this usually indicates a problem in method GetLDAPFilter.", ProviderInternalName, LDAPServer.Directory.Path), TraceSeverity.Unexpected, EventSeverity.Information, LdapcpLogging.Categories.LDAP_Lookup);
                    return;
                }
                DirectoryEntry directory = LDAPServer.Directory;
                using (DirectorySearcher ds = new DirectorySearcher(LDAPServer.Filter))
                {
                    ds.SearchRoot = directory;
                    ds.ClientTimeout = new TimeSpan(0, 0, this.CurrentConfiguration.LDAPQueryTimeout); // Set the timeout of the query
                    ds.PropertiesToLoad.Add(LDAPObjectClassName);
                    ds.PropertiesToLoad.Add("nETBIOSName");
                    foreach (var ldapAttribute in ProcessedClaimTypesConfig.Where(x => !String.IsNullOrEmpty(x.LDAPAttribute)))
                    {
                        ds.PropertiesToLoad.Add(ldapAttribute.LDAPAttribute);
                        if (!String.IsNullOrEmpty(ldapAttribute.LDAPAttributeToDisplayProp)) ds.PropertiesToLoad.Add(ldapAttribute.LDAPAttributeToDisplayProp);
                    }
                    // Populate additional attributes that are not part of the filter but are requested in the result
                    foreach (var metadataAttribute in UserMetadataClaimTypeConfigList)
                    {
                        if (!ds.PropertiesToLoad.Contains(metadataAttribute.LDAPAttribute)) ds.PropertiesToLoad.Add(metadataAttribute.LDAPAttribute);
                    }

                    using (new SPMonitoredScope(String.Format("[{0}] Connecting to \"{1}\" with AuthenticationType \"{2}\" and filter \"{3}\"", ProviderInternalName, directory.Path, directory.AuthenticationType.ToString(), ds.Filter), 3000)) // threshold of 3 seconds before it's considered too much. If exceeded it is recorded in a higher logging level
                    {
                        try
                        {
                            LdapcpLogging.Log(String.Format("[{0}] Connecting to \"{1}\" with AuthenticationType \"{2}\" and filter \"{3}\"", ProviderInternalName, directory.Path, directory.AuthenticationType.ToString(), ds.Filter), TraceSeverity.Verbose, EventSeverity.Information, LdapcpLogging.Categories.LDAP_Lookup);
                            Stopwatch stopWatch = new Stopwatch();
                            stopWatch.Start();
                            using (SearchResultCollection directoryResults = ds.FindAll())
                            {
                                stopWatch.Stop();
                                LdapcpLogging.Log(String.Format("[{0}] Got {1} result(s) in {2}ms from \"{3}\" with query \"{4}\"", ProviderInternalName, directoryResults.Count.ToString(), stopWatch.ElapsedMilliseconds.ToString(), directory.Path, ds.Filter), TraceSeverity.Medium, EventSeverity.Information, LdapcpLogging.Categories.LDAP_Lookup);
                                if (directoryResults != null && directoryResults.Count > 0)
                                {
                                    lock (lockResults)
                                    {
                                        // Retrieve FQDN and domain name of current DirectoryEntry
                                        string domainName, domainFQDN = String.Empty;
                                        RequestInformation.GetDomainInformation(directory, out domainName, out domainFQDN);
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
                                    LdapcpLogging.Log(String.Format("[{0}] Got {1} result(s) from {2}", ProviderInternalName, directoryResults.Count.ToString(), directory.Path), TraceSeverity.Verbose, EventSeverity.Information, LdapcpLogging.Categories.LDAP_Lookup);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LdapcpLogging.LogException(ProviderInternalName, "during connection to LDAP server " + directory.Path, LdapcpLogging.Categories.LDAP_Lookup, ex);
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
            LdapcpLogging.Log(String.Format("[{0}] Got {1} result(s) in {2}ms from all servers with query \"{3}\"", ProviderInternalName, LDAPSearchResults.Count, globalStopWatch.ElapsedMilliseconds.ToString(), LDAPServers[0].Filter), TraceSeverity.Verbose, EventSeverity.Information, LdapcpLogging.Categories.LDAP_Lookup);
            return LDAPSearchResults.Count > 0;
        }

        /// <summary>
        /// Override this method to set LDAP connections
        /// </summary>
        /// <param name="requestInfo">Information about current context and operation</param>
        /// <returns>Array of LDAP servers to query</returns>
        protected virtual LDAPConnection[] GetLDAPServers(RequestInformation requestInfo)
        {
            if (this.CurrentConfiguration.LDAPConnectionsProp == null) return null;
            IEnumerable<LDAPConnection> ldapConnections = this.CurrentConfiguration.LDAPConnectionsProp;
            if (requestInfo.RequestType == RequestType.Augmentation) ldapConnections = this.CurrentConfiguration.LDAPConnectionsProp.Where(x => x.AugmentationEnabled);
            LDAPConnection[] connections = new LDAPConnection[ldapConnections.Count()];

            int i = 0;
            foreach (var ldapConnection in ldapConnections)
            {
                LDAPConnection coco = ldapConnection.CopyPersistedProperties();
                if (!ldapConnection.UserServerDirectoryEntry)
                {
                    coco.directoryEntry = new DirectoryEntry(ldapConnection.Path, ldapConnection.Username, ldapConnection.Password, ldapConnection.AuthenticationTypes);
                    string serverType = coco.GetGroupMembershipAsADDomain ? "AD" : "LDAP";
                    LdapcpLogging.Log(String.Format("[{0}] Add {1} server \"{2}\" with AuthenticationType \"{3}\" and credentials \"{4}\".", ProviderInternalName, serverType, ldapConnection.Path, ldapConnection.AuthenticationTypes.ToString(), ldapConnection.Username), TraceSeverity.Verbose, EventSeverity.Information, LdapcpLogging.Categories.LDAP_Lookup);
                }
                else
                {
                    coco.directoryEntry = Domain.GetComputerDomain().GetDirectoryEntry();
                    LdapcpLogging.Log(String.Format("[{0}] Add AD server \"{1}\" with AuthenticationType \"{2}\" and credentials of application pool account.", ProviderInternalName, coco.directoryEntry.Path, coco.directoryEntry.AuthenticationType.ToString()), TraceSeverity.Verbose, EventSeverity.Information, LdapcpLogging.Categories.LDAP_Lookup);
                }
                connections[i++] = coco;
            }
            return connections;
        }

        protected override void FillClaimTypes(List<string> claimTypes)
        {
            if (claimTypes == null)
                throw new ArgumentNullException("claimTypes");

            LdapcpLogging.LogDebug(String.Format("[{0}] FillClaimValueTypes called, ProcessedAttributes null: {1}", ProviderInternalName, ProcessedClaimTypesConfig == null ? true : false));

            if (ProcessedClaimTypesConfig == null)
                return;

            this.Lock_Config.EnterReadLock();
            try
            {
                foreach (var attribute in ProcessedClaimTypesConfig.Where(x => !String.IsNullOrEmpty(x.ClaimType)))
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

            LdapcpLogging.LogDebug(String.Format("[{0}] FillClaimValueTypes called, ProcessedAttributes null: {1}", ProviderInternalName, ProcessedClaimTypesConfig == null ? true : false));

            if (ProcessedClaimTypesConfig == null)
                return;

            this.Lock_Config.EnterReadLock();
            try
            {
                foreach (var attribute in ProcessedClaimTypesConfig.Where(x => !String.IsNullOrEmpty(x.ClaimValueType)))
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
                LdapcpLogging.LogDebug(String.Format("[{0}] Not trying to augment '{1}' because OriginalIssuer is '{2}'.", ProviderInternalName, decodedEntity.Value, decodedEntity.OriginalIssuer));
                return;
            }

            SPSecurity.RunWithElevatedPrivileges(delegate ()
            {
                if (!Initialize(context, null))
                    return;

                this.Lock_Config.EnterReadLock();
                try
                {
                    LdapcpLogging.LogDebug(String.Format("[{0}] Original entity to augment: '{1}', augmentation enabled: {2}.", ProviderInternalName, entity.Value, CurrentConfiguration.EnableAugmentation));
                    if (!this.CurrentConfiguration.EnableAugmentation) return;
                    if (String.IsNullOrEmpty(this.CurrentConfiguration.ClaimTypeUsedForAugmentation))
                    {
                        LdapcpLogging.Log(String.Format("[{0}] Augmentation is enabled but no claim type is configured.", ProviderInternalName),
                            TraceSeverity.High, EventSeverity.Error, LdapcpLogging.Categories.Augmentation);
                        return;
                    }
                    var groupAttribute = this.ProcessedClaimTypesConfig.FirstOrDefault(x => String.Equals(x.ClaimType, this.CurrentConfiguration.ClaimTypeUsedForAugmentation, StringComparison.InvariantCultureIgnoreCase) && !x.CreateAsIdentityClaim);
                    if (groupAttribute == null)
                    {
                        LdapcpLogging.Log(String.Format("[{0}] Settings for claim type \"{1}\" cannot be found, its entry may have been deleted from claims mapping table.", ProviderInternalName, this.CurrentConfiguration.ClaimTypeUsedForAugmentation),
                            TraceSeverity.High, EventSeverity.Error, LdapcpLogging.Categories.Augmentation);
                        return;
                    }

                    RequestInformation infos = new RequestInformation(CurrentConfiguration, RequestType.Augmentation, ProcessedClaimTypesConfig, null, decodedEntity, context, null, null, Int32.MaxValue);
                    LDAPConnection[] connections = GetLDAPServers(infos);
                    if (connections == null || connections.Length == 0)
                    {
                        LdapcpLogging.Log(String.Format("[{0}] No LDAP server is enabled for augmentation", ProviderInternalName), TraceSeverity.High, EventSeverity.Error, LdapcpLogging.Categories.Augmentation);
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
                            directoryGroups = GetGroupsFromADDirectory(coco.directoryEntry, infos, groupAttribute);
                        else
                            directoryGroups = GetGroupsFromLDAPDirectory(coco.directoryEntry, infos, groupAttribute);

                        lock (lockResults)
                        {
                            groups.AddRange(directoryGroups);
                        }
                    });
                    stopWatch.Stop();
                    LdapcpLogging.Log(String.Format("[{0}] LDAP queries to get group membership on all servers completed in {1}ms",
                        ProviderInternalName, stopWatch.ElapsedMilliseconds.ToString()),
                        TraceSeverity.Verbose, EventSeverity.Information, LdapcpLogging.Categories.Augmentation);

                    foreach (SPClaim group in groups)
                    {
                        claims.Add(group);
                        LdapcpLogging.Log(String.Format("[{0}] Added group \"{1}\" to user \"{2}\"", ProviderInternalName, group.Value, infos.IncomingEntity.Value),
                            TraceSeverity.Verbose, EventSeverity.Information, LdapcpLogging.Categories.Augmentation);
                    }
                    if (groups.Count > 0)
                        LdapcpLogging.Log(String.Format("[{0}] User '{1}' was augmented with {2} groups of claim type '{3}'", ProviderInternalName, infos.IncomingEntity.Value, groups.Count.ToString(), groupAttribute.ClaimType),
                            TraceSeverity.Medium, EventSeverity.Information, LdapcpLogging.Categories.Augmentation);
                    else
                        LdapcpLogging.Log(String.Format("[{0}] No group found for user '{1}' during augmentation", ProviderInternalName, infos.IncomingEntity.Value),
                            TraceSeverity.Medium, EventSeverity.Information, LdapcpLogging.Categories.Augmentation);
                }
                catch (Exception ex)
                {
                    LdapcpLogging.LogException(ProviderInternalName, "in AugmentEntity", LdapcpLogging.Categories.Augmentation, ex);
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
        protected virtual List<SPClaim> GetGroupsFromADDirectory(DirectoryEntry directory, RequestInformation requestInfo, AttributeHelper groupAttribute)
        {
            List<SPClaim> groups = new List<SPClaim>();
            using (new SPMonitoredScope(String.Format("[{0}] Getting AD group membership of user {1} in {2}", ProviderInternalName, requestInfo.IncomingEntity.Value, directory.Path), 2000))
            {
                UserPrincipal adUser = null;
                try
                {
                    string directoryDomainName, directoryDomainFqdn;
                    RequestInformation.GetDomainInformation(directory, out directoryDomainName, out directoryDomainFqdn);
                    Stopwatch stopWatch = new Stopwatch();
                    stopWatch.Start();

                    using (PrincipalContext principalContext = new PrincipalContext(ContextType.Domain, directoryDomainFqdn))
                    {
                        // https://github.com/Yvand/LDAPCP/issues/22
                        // UserPrincipal.FindByIdentity() doesn't support emails, so if IncomingEntity is an email, user needs to be retrieved in a different way
                        if (String.Equals(requestInfo.IncomingEntity.ClaimType, WIF.ClaimTypes.Email, StringComparison.InvariantCultureIgnoreCase))
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
                            RequestInformation.GetDomainInformation(group.DistinguishedName, out groupDomainName, out groupDomainFqdn);
                            string claimValue = group.Name;
                            if (!String.IsNullOrEmpty(groupAttribute.PrefixToAddToValueReturnedProp) && groupAttribute.PrefixToAddToValueReturnedProp.Contains(Constants.LDAPCPCONFIG_TOKENDOMAINNAME))
                                claimValue = groupAttribute.PrefixToAddToValueReturnedProp.Replace(Constants.LDAPCPCONFIG_TOKENDOMAINNAME, groupDomainName) + group.Name;
                            else if (!String.IsNullOrEmpty(groupAttribute.PrefixToAddToValueReturnedProp) && groupAttribute.PrefixToAddToValueReturnedProp.Contains(Constants.LDAPCPCONFIG_TOKENDOMAINFQDN))
                                claimValue = groupAttribute.PrefixToAddToValueReturnedProp.Replace(Constants.LDAPCPCONFIG_TOKENDOMAINFQDN, groupDomainFqdn) + group.Name;
                            SPClaim claim = CreateClaim(groupAttribute.ClaimType, claimValue, groupAttribute.ClaimValueType, false);
                            groups.Add(claim);
                        }
                    }
                }
                catch (PrincipalOperationException ex)
                {
                    LdapcpLogging.LogException(ProviderInternalName, String.Format("while getting AD group membership of user {0} in {1} using UserPrincipal.GetAuthorizationGroups(). This is likely due to a bug in .NET framework in UserPrincipal.GetAuthorizationGroups (as of v4.6.1), especially if user is member (directly or not) of a group either in a child domain that was migrated, or a group that has special (deny) permissions.", requestInfo.IncomingEntity.Value, directory.Path), LdapcpLogging.Categories.Augmentation, ex);
                    // In this case, fallback to LDAP method to get group membership.
                    return GetGroupsFromLDAPDirectory(directory, requestInfo, groupAttribute);
                }
                catch (PrincipalServerDownException ex)
                {
                    LdapcpLogging.LogException(ProviderInternalName, String.Format("while getting AD group membership of user {0} in {1} using UserPrincipal.GetAuthorizationGroups(). Is this server an Active Directory server?", requestInfo.IncomingEntity.Value, directory.Path), LdapcpLogging.Categories.Augmentation, ex);
                }
                catch (Exception ex)
                {
                    LdapcpLogging.LogException(ProviderInternalName, String.Format("while getting AD group membership of user {0} in {1} using UserPrincipal.GetAuthorizationGroups()", requestInfo.IncomingEntity.Value, directory.Path), LdapcpLogging.Categories.Augmentation, ex);
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
        protected virtual List<SPClaim> GetGroupsFromLDAPDirectory(DirectoryEntry directory, RequestInformation requestInfo, AttributeHelper groupAttribute)
        {
            List<SPClaim> groups = new List<SPClaim>();
            using (new SPMonitoredScope(String.Format("[{0}] Getting LDAP group membership of user {1} in {2}", ProviderInternalName, requestInfo.IncomingEntity.Value, directory.Path), 2000))
            {
                try
                {
                    string directoryDomainName, directoryDomainFqdn;
                    RequestInformation.GetDomainInformation(directory, out directoryDomainName, out directoryDomainFqdn);
                    Stopwatch stopWatch = new Stopwatch();

                    using (DirectorySearcher searcher = new DirectorySearcher(directory))
                    {
                        searcher.ClientTimeout = new TimeSpan(0, 0, this.CurrentConfiguration.LDAPQueryTimeout); // Set the timeout of the query
                        searcher.Filter = string.Format("(&(ObjectClass={0})({1}={2}){3})", IdentityClaimTypeConfig.LDAPObjectClassProp, IdentityClaimTypeConfig.LDAPAttribute, requestInfo.IncomingEntity.Value, IdentityClaimTypeConfig.AdditionalLDAPFilterProp);
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
                            string claimValue = RequestInformation.GetValueFromDistinguishedName(groupDN);
                            if (String.IsNullOrEmpty(claimValue)) continue;

                            string groupDomainName, groupDomainFqdn;
                            RequestInformation.GetDomainInformation(groupDN, out groupDomainName, out groupDomainFqdn);
                            if (!String.IsNullOrEmpty(groupAttribute.PrefixToAddToValueReturnedProp) && groupAttribute.PrefixToAddToValueReturnedProp.Contains(Constants.LDAPCPCONFIG_TOKENDOMAINNAME))
                                claimValue = groupAttribute.PrefixToAddToValueReturnedProp.Replace(Constants.LDAPCPCONFIG_TOKENDOMAINNAME, groupDomainName) + claimValue;
                            else if (!String.IsNullOrEmpty(groupAttribute.PrefixToAddToValueReturnedProp) && groupAttribute.PrefixToAddToValueReturnedProp.Contains(Constants.LDAPCPCONFIG_TOKENDOMAINFQDN))
                                claimValue = groupAttribute.PrefixToAddToValueReturnedProp.Replace(Constants.LDAPCPCONFIG_TOKENDOMAINFQDN, groupDomainFqdn) + claimValue;
                            SPClaim claim = CreateClaim(groupAttribute.ClaimType, claimValue, groupAttribute.ClaimValueType, false);
                            groups.Add(claim);
                        }
                    }
                    LdapcpLogging.Log(String.Format("[{0}] Domain {1} returned {2} groups for user {3}. Lookup took {4}ms on LDAP server '{5}'",
                        ProviderInternalName, directoryDomainFqdn, groups.Count, requestInfo.IncomingEntity.Value, stopWatch.ElapsedMilliseconds.ToString(), directory.Path),
                        TraceSeverity.Medium, EventSeverity.Information, LdapcpLogging.Categories.Augmentation);
                }
                catch (Exception ex)
                {
                    LdapcpLogging.LogException(ProviderInternalName, String.Format("while getting LDAP group membership of user {0} in {1}. This is likely due to a bug in .NET framework in UserPrincipal.GetAuthorizationGroups (as of v4.6.1), especially if user is member (directly or not) of a group either in a child domain that was migrated, or a group that has special (deny) permissions.", requestInfo.IncomingEntity.Value, directory.Path), LdapcpLogging.Categories.Augmentation, ex);
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
            LdapcpLogging.LogDebug(String.Format("[{0}] FillEntityTypes called, ProcessedAttributes null: {1}", ProviderInternalName, ProcessedClaimTypesConfig == null ? true : false));

            if (ProcessedClaimTypesConfig == null)
                return;

            this.Lock_Config.EnterReadLock();
            try
            {
                var spUniqueEntitytypes = from attributes in ProcessedClaimTypesConfig
                                          where attributes.ClaimEntityType != null
                                          group attributes by new { claimEntityType = attributes.ClaimEntityType } into groupedByEntityType
                                          select new { value = groupedByEntityType.Key.claimEntityType };

                if (null == spUniqueEntitytypes) return;

                foreach (var spEntityType in spUniqueEntitytypes)
                {
                    entityTypes.Add(spEntityType.value);
                }
            }
            finally
            {
                this.Lock_Config.ExitReadLock();
            }
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
            LdapcpLogging.LogDebug(String.Format("[{0}] FillHierarchy called", ProviderInternalName));

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
                        foreach (var attribute in ProcessedClaimTypesConfig.Where(x => !x.CreateAsIdentityClaim && entityTypes.Contains(x.ClaimEntityType)))
                        {
                            hierarchy.AddChild(
                                new Microsoft.SharePoint.WebControls.SPProviderHierarchyNode(
                                    _ProviderInternalName,
                                    attribute.ClaimTypeMappingName,
                                    attribute.ClaimType,
                                    true));
                        }
                    }
                }
                catch (Exception ex)
                {
                    LdapcpLogging.LogException(ProviderInternalName, "in FillHierarchy", LdapcpLogging.Categories.Claims_Picking, ex);
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

        protected virtual string AddAttributeToFilter(AttributeHelper attribute, string searchPattern)
        {
            string filter = String.Empty;
            string additionalFilter = String.Empty;

            if (this.CurrentConfiguration.FilterSecurityGroupsOnlyProp && String.Equals(attribute.LDAPObjectClassProp, "group", StringComparison.OrdinalIgnoreCase))
                additionalFilter = FilterSecurityGroupsOnlyLDAPFilter;

            if (!String.IsNullOrEmpty(attribute.AdditionalLDAPFilterProp))
                additionalFilter += attribute.AdditionalLDAPFilterProp;

            filter = String.Format(LDAPFilter, attribute.LDAPAttribute, searchPattern, attribute.LDAPObjectClassProp, additionalFilter);
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
            var attr = ProcessedClaimTypesConfig.FirstOrDefault(x => String.Equals(x.ClaimType, type, StringComparison.InvariantCultureIgnoreCase));
            //if (inputHasKeyword && attr.DoNotAddPrefixIfInputHasKeywordProp)
            if ((!inputHasKeyword || !attr.DoNotAddPrefixIfInputHasKeywordProp) &&
                !HasPrefixToken(attr.PrefixToAddToValueReturnedProp, Constants.LDAPCPCONFIG_TOKENDOMAINNAME) &&
                !HasPrefixToken(attr.PrefixToAddToValueReturnedProp, Constants.LDAPCPCONFIG_TOKENDOMAINFQDN)
            )
                claimValue = attr.PrefixToAddToValueReturnedProp;

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
                || result.Attribute.CreateAsIdentityClaim) && result.Attribute.LDAPObjectClassProp == IdentityClaimTypeConfig.LDAPObjectClassProp)
            {
                isIdentityClaimType = true;
            }

            if (result.Attribute.CreateAsIdentityClaim && result.Attribute.LDAPObjectClassProp != IdentityClaimTypeConfig.LDAPObjectClassProp)
            {
                // Get reference attribute to use to create actual permission (claim type and its LDAPAttribute) from current result
                AttributeHelper attribute = ProcessedClaimTypesConfig.FirstOrDefault(x => !x.CreateAsIdentityClaim && x.LDAPObjectClassProp == result.Attribute.LDAPObjectClassProp);
                if (attribute != null)
                {
                    permissionClaimType = attribute.ClaimType;
                    result.Attribute.ClaimType = attribute.ClaimType;
                    result.Attribute.ClaimEntityType = attribute.ClaimEntityType;
                    result.Attribute.ClaimTypeMappingName = attribute.ClaimTypeMappingName;
                    permissionValue = result.LDAPResults[attribute.LDAPAttribute][0].ToString();    // Pick value of current result from actual LDAP attribute to use (which is not the LDAP attribute that matches input)
                    result.Attribute.LDAPAttributeToDisplayProp = attribute.LDAPAttributeToDisplayProp;
                    result.Attribute.PrefixToAddToValueReturnedProp = attribute.PrefixToAddToValueReturnedProp;
                    result.Attribute.PrefixToBypassLookup = attribute.PrefixToBypassLookup;
                }
            }

            if (result.Attribute.CreateAsIdentityClaim && result.Attribute.LDAPObjectClassProp == IdentityClaimTypeConfig.LDAPObjectClassProp)
            {
                // This attribute is not directly linked to a claim type, so permission is created with identity claim type
                permissionClaimType = IdentityClaimTypeConfig.ClaimType;
                permissionValue = FormatPermissionValue(permissionClaimType, result.LDAPResults[IdentityClaimTypeConfig.LDAPAttribute][0].ToString(), result.DomainName, result.DomainFQDN, isIdentityClaimType, result);
                claim = CreateClaim(
                    permissionClaimType,
                    permissionValue,
                    IdentityClaimTypeConfig.ClaimValueType,
                    false);
                pe.EntityType = IdentityClaimTypeConfig.ClaimEntityType;
            }
            else
            {
                permissionValue = FormatPermissionValue(result.Attribute.ClaimType, permissionValue, result.DomainName, result.DomainFQDN, isIdentityClaimType, result);
                claim = CreateClaim(
                    permissionClaimType,
                    permissionValue,
                    result.Attribute.ClaimValueType,
                    false);
                pe.EntityType = result.Attribute.ClaimEntityType;
            }

            int nbMetadata = 0;
            // Populate metadata attributes of permission created
            // Change condition to fix bug http://ldapcp.codeplex.com/discussions/653087
            // We don't care about the claim entity type, it must be unique based on the LDAP class
            //foreach (var entityAttrib in MetadataAttributes.Where(x => x.ClaimEntityType == result.Attribute.ClaimEntityType))
            foreach (var entityAttrib in UserMetadataClaimTypeConfigList.Where(x => String.Equals(x.LDAPObjectClassProp, result.Attribute.LDAPObjectClassProp, StringComparison.InvariantCultureIgnoreCase)))
            {
                // if there is actally a value in the LDAP result, then it can be set
                if (result.LDAPResults.Contains(entityAttrib.LDAPAttribute) && result.LDAPResults[entityAttrib.LDAPAttribute].Count > 0)
                {
                    pe.EntityData[entityAttrib.EntityDataKey] = result.LDAPResults[entityAttrib.LDAPAttribute][0].ToString();
                    nbMetadata++;
                    LdapcpLogging.Log(String.Format("[{0}] Added metadata \"{1}\" with value \"{2}\" to permission", ProviderInternalName, entityAttrib.EntityDataKey, pe.EntityData[entityAttrib.EntityDataKey]), TraceSeverity.Verbose, EventSeverity.Information, LdapcpLogging.Categories.Claims_Picking);
                }
            }

            pe.Claim = claim;
            pe.IsResolved = true;
            pe.EntityGroupName = this.CurrentConfiguration.PickerEntityGroupNameProp;
            pe.Description = String.Format(
                PickerEntityOnMouseOver,
                result.Attribute.LDAPAttribute,
                result.Value);

            pe.DisplayText = FormatPermissionDisplayText(permissionClaimType, permissionValue, isIdentityClaimType, result, pe);

            LdapcpLogging.Log(String.Format("[{0}] Created permission: display text: \"{1}\", value: \"{2}\", claim type: \"{3}\", and filled with {4} metadata.", ProviderInternalName, pe.DisplayText, pe.Claim.Value, pe.Claim.ClaimType, nbMetadata.ToString()), TraceSeverity.Verbose, EventSeverity.Information, LdapcpLogging.Categories.Claims_Picking);
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

            var attr = ProcessedClaimTypesConfig.FirstOrDefault(x => String.Equals(x.ClaimType, claimType, StringComparison.InvariantCultureIgnoreCase));
            if (HasPrefixToken(attr.PrefixToAddToValueReturnedProp, Constants.LDAPCPCONFIG_TOKENDOMAINNAME))
                value = string.Format("{0}{1}", attr.PrefixToAddToValueReturnedProp.Replace(Constants.LDAPCPCONFIG_TOKENDOMAINNAME, domainName), value);

            if (HasPrefixToken(attr.PrefixToAddToValueReturnedProp, Constants.LDAPCPCONFIG_TOKENDOMAINFQDN))
                value = string.Format("{0}{1}", attr.PrefixToAddToValueReturnedProp.Replace(Constants.LDAPCPCONFIG_TOKENDOMAINFQDN, domainFQDN), value);

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
            if (HasPrefixToken(attr.PrefixToAddToValueReturnedProp, Constants.LDAPCPCONFIG_TOKENDOMAINNAME))
                prefixToAdd = string.Format("{0}", attr.PrefixToAddToValueReturnedProp.Replace(Constants.LDAPCPCONFIG_TOKENDOMAINNAME, result.DomainName));

            if (HasPrefixToken(attr.PrefixToAddToValueReturnedProp, Constants.LDAPCPCONFIG_TOKENDOMAINFQDN))
                prefixToAdd = string.Format("{0}", attr.PrefixToAddToValueReturnedProp.Replace(Constants.LDAPCPCONFIG_TOKENDOMAINFQDN, result.DomainFQDN));

            if (isIdentityClaimType) displayLdapMatchForIdentityClaimType = this.CurrentConfiguration.DisplayLdapMatchForIdentityClaimTypeProp;

            if (!String.IsNullOrEmpty(result.Attribute.LDAPAttributeToDisplayProp) && result.LDAPResults.Contains(result.Attribute.LDAPAttributeToDisplayProp))
            {   // AttributeHelper is set to use a specific LDAP attribute as display text of permission
                if (!isIdentityClaimType && result.Attribute.ShowClaimNameInDisplayText)
                    permissionDisplayText = "(" + result.Attribute.ClaimTypeMappingName + ") ";
                permissionDisplayText += prefixToAdd;
                permissionDisplayText += valueDisplayedInPermission = result.LDAPResults[result.Attribute.LDAPAttributeToDisplayProp][0].ToString();
            }
            else
            {   // AttributeHelper is set to use its actual LDAP attribute as display text of permission
                if (!isIdentityClaimType)
                {
                    valueDisplayedInPermission = claimValue.StartsWith(prefixToAdd) ? claimValue : prefixToAdd + claimValue;
                    if (result.Attribute.ShowClaimNameInDisplayText)
                    {
                        permissionDisplayText = String.Format(
                            PickerEntityDisplayText,
                            result.Attribute.ClaimTypeMappingName,
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
        protected virtual PickerEntity CreatePickerEntityForSpecificClaimType(string input, AttributeHelper claimTypesToResolve, bool inputHasKeyword)
        {
            List<PickerEntity> entities = CreatePickerEntityForSpecificClaimTypes(
                input,
                new List<AttributeHelper>()
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
        protected virtual List<PickerEntity> CreatePickerEntityForSpecificClaimTypes(string input, List<AttributeHelper> claimTypesToResolve, bool inputHasKeyword)
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
                        PickerEntityDisplayText,
                        claimTypeToResolve.ClaimTypeMappingName,
                        input);
                }

                pe.EntityType = claimTypeToResolve.ClaimEntityType;
                pe.Description = String.Format(
                    PickerEntityOnMouseOver,
                    claimTypeToResolve.LDAPAttribute,
                    input);

                pe.Claim = claim;
                pe.IsResolved = true;
                pe.EntityGroupName = this.CurrentConfiguration.PickerEntityGroupNameProp;

                if (claimTypeToResolve.ClaimEntityType == SPClaimEntityTypes.User && !String.IsNullOrEmpty(claimTypeToResolve.EntityDataKey))
                {
                    pe.EntityData[claimTypeToResolve.EntityDataKey] = pe.Claim.Value;
                    LdapcpLogging.Log(String.Format("[{0}] Added metadata \"{1}\" with value \"{2}\" to permission", ProviderInternalName, claimTypeToResolve.EntityDataKey, pe.EntityData[claimTypeToResolve.EntityDataKey]), TraceSeverity.Verbose, EventSeverity.Information, LdapcpLogging.Categories.Claims_Picking);
                }
                entities.Add(pe);
                LdapcpLogging.Log(String.Format("[{0}] Created permission: display text: \"{1}\", value: \"{2}\", claim type: \"{3}\".", ProviderInternalName, pe.DisplayText, pe.Claim.Value, pe.Claim.ClaimType), TraceSeverity.Verbose, EventSeverity.Information, LdapcpLogging.Categories.Claims_Picking);
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
            LdapcpLogging.LogDebug(String.Format("[{0}] GetClaimTypeForUserKey called", ProviderInternalName));

            if (!Initialize(null, null))
                return null;

            this.Lock_Config.EnterReadLock();
            try
            {
                return IdentityClaimTypeConfig.ClaimType;
            }
            catch (Exception ex)
            {
                LdapcpLogging.LogException(ProviderInternalName, "in GetClaimTypeForUserKey", LdapcpLogging.Categories.Rehydration, ex);
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
            LdapcpLogging.LogDebug(String.Format("[{0}] GetUserKeyForEntity called, incoming claim value: \"{1}\", claim type: \"{2}\", claim issuer: \"{3}\"", ProviderInternalName, entity.Value, entity.ClaimType, entity.OriginalIssuer));

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
                LdapcpLogging.Log(String.Format("[{0}] Return user key for user \"{1}\"", ProviderInternalName, entity.Value),
                    TraceSeverity.Verbose, EventSeverity.Information, LdapcpLogging.Categories.Rehydration);
                return CreateClaim(IdentityClaimTypeConfig.ClaimType, curUser.Value, curUser.ValueType);
            }
            catch (Exception ex)
            {
                LdapcpLogging.LogException(ProviderInternalName, "in GetUserKeyForEntity", LdapcpLogging.Categories.Rehydration, ex);
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
        public AttributeHelper Attribute;
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

    /// <summary>
    /// Check if 2 attributes are identical (sams class and same name) to not add duplicates to the attributes list
    /// http://msdn.microsoft.com/en-us/library/bb338049
    /// </summary>
    class LDAPPropertiesComparer : IEqualityComparer<AttributeHelper>
    {
        // LDAP Attributes are equal if they have same LDAPAttribute and same LDAPObjectClassProp
        public bool Equals(AttributeHelper x, AttributeHelper y)
        {
            // Check whether the compared objects reference the same data.
            if (Object.ReferenceEquals(x, y)) return true;

            //Check whether any of the compared objects is null.
            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
                return false;

            // Check if they have same LDAPAttribute and same LDAPObjectClassProp
            //return ((String.Compare(x.LDAPAttribute, y.LDAPAttribute, true) == 0) && (String.Compare(x.LDAPObjectClassProp, y.LDAPObjectClassProp, true) == 0));
            return ((String.Equals(x.LDAPAttribute, y.LDAPAttribute, StringComparison.OrdinalIgnoreCase)) && (String.Equals(x.LDAPObjectClassProp, y.LDAPObjectClassProp, StringComparison.OrdinalIgnoreCase)));
        }

        // If Equals() returns true for a pair of objects 
        // then GetHashCode() must return the same value for these objects.
        public int GetHashCode(AttributeHelper attribute)
        {
            //Check whether the object is null
            if (Object.ReferenceEquals(attribute, null)) return 0;

            // Add an extra space so that string can never be null and GetHashCode will never fail
            return (attribute.LDAPAttribute + " " + attribute.LDAPObjectClassProp).GetHashCode();
        }
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
        public bool Contains(LDAPSearchResultWrapper result, AttributeHelper attribute, bool compareWithDomain)
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
