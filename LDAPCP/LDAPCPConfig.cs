using Microsoft.SharePoint;
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
using System.Text.RegularExpressions;
using System.Web;
using static ldapcp.ClaimsProviderLogging;
using WIF4_5 = System.Security.Claims;

namespace ldapcp
{
    public interface ILDAPCPConfiguration
    {
        List<LDAPConnection> LDAPConnectionsProp { get; set; }
        ClaimTypeConfigCollection ClaimTypes { get; set; }
        bool BypassLDAPLookup { get; set; }
        bool AddWildcardAsPrefixOfInput { get; set; }
        bool DisplayLdapMatchForIdentityClaimTypeProp { get; set; }
        string PickerEntityGroupNameProp { get; set; }
        bool FilterEnabledUsersOnlyProp { get; set; }
        bool FilterSecurityGroupsOnlyProp { get; set; }
        bool FilterExactMatchOnlyProp { get; set; }
        int LDAPQueryTimeout { get; set; }
        bool CompareResultsWithDomainNameProp { get; set; }
        bool EnableAugmentation { get; set; }
        string MainGroupClaimType { get; set; }
        string EntityDisplayTextPrefix { get; set; }
        string CustomData { get; set; }
        int MaxSearchResultsCount { get; set; }
    }

    public class ClaimsProviderConstants
    {
        public const string CONFIG_ID = "5D306A02-A262-48AC-8C44-BDB927620227";
        public const string CONFIG_NAME = "LdapcpConfig";
        public const string LDAPCPCONFIG_TOKENDOMAINNAME = "{domain}";
        public const string LDAPCPCONFIG_TOKENDOMAINFQDN = "{fqdn}";
        public const int LDAPCPCONFIG_TIMEOUT = 10;
        public static string GroupClaimEntityType = SPClaimEntityTypes.FormsRole;
        public const bool EnforceOnly1ClaimTypeForGroup = false;    // In LDAPCP, multiple claim types can be used to create group permissions
        public const string DefaultMainGroupClaimType = WIF4_5.ClaimTypes.Role;
        public const string PUBLICSITEURL = "https://ldapcp.com";
        private static object Sync_SetClaimsProviderVersion = new object();
        private static string _ClaimsProviderVersion;
        public static string ClaimsProviderVersion
        {
            get
            {
                if (!String.IsNullOrEmpty(_ClaimsProviderVersion))
                    return _ClaimsProviderVersion;

                // Method FileVersionInfo.GetVersionInfo() may hang and block all LDAPCP threads, so it is read only 1 time
                lock (Sync_SetClaimsProviderVersion)
                {
                    if (!String.IsNullOrEmpty(_ClaimsProviderVersion))
                        return _ClaimsProviderVersion;

                    try
                    {
                        _ClaimsProviderVersion = FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(LDAPCP)).Location).FileVersion;
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

        /// <summary>
        /// Escape characters to use for special characters in LDAP filters, as documented in https://ldap.com/ldap-filters/
        /// </summary>
        public static Dictionary<string, string> SpecialCharacters = new Dictionary<string, string>()
            {
                { @"\", @"\5C" },   // '\' must be the 1st to be evaluated because '\' is also present in the escape characters themselves
                { "*", @"\2A" },
                { "(", @"\28" },
                { ")", @"\29" },
            };
    }

    public class LDAPCPConfig : SPPersistedObject, ILDAPCPConfiguration
    {
        /// <summary>
        /// List of LDAP servers to query
        /// </summary>
        public List<LDAPConnection> LDAPConnectionsProp
        {
            get => LDAPConnections;
            set => LDAPConnections = value;
        }
        [Persisted]
        private List<LDAPConnection> LDAPConnections;

        /// <summary>
        /// Configuration of claim types and their mapping with LDAP attribute/class
        /// </summary>
        public ClaimTypeConfigCollection ClaimTypes
        {
            get
            {
                if (_ClaimTypes == null)
                {
                    _ClaimTypes = new ClaimTypeConfigCollection(ref this._ClaimTypesCollection);
                }
                //else
                //{
                //    _ClaimTypesCollection = _ClaimTypes.innerCol;
                //}
                return _ClaimTypes;
            }
            set
            {
                _ClaimTypes = value;
                _ClaimTypesCollection = value == null ? null : value.innerCol;
            }
        }
        [Persisted]
        private Collection<ClaimTypeConfig> _ClaimTypesCollection;

        private ClaimTypeConfigCollection _ClaimTypes;

        /// <summary>
        /// If true, LDAPCP will validate the input as is, with no LDAP query
        /// </summary>
        public bool BypassLDAPLookup
        {
            get => AlwaysResolveUserInput;
            set => AlwaysResolveUserInput = value;
        }
        [Persisted]
        private bool AlwaysResolveUserInput;

        /// <summary>
        /// NOT RECOMMENDED: Change filter to query "*input*" instead of "input*". This may have a strong negative impact on performance
        /// </summary>
        public bool AddWildcardAsPrefixOfInput
        {
            get => AddWildcardInFrontOfQuery;
            set => AddWildcardInFrontOfQuery = value;
        }
        [Persisted]
        private bool AddWildcardInFrontOfQuery;

        public bool DisplayLdapMatchForIdentityClaimTypeProp
        {
            get => DisplayLdapMatchForIdentityClaimType;
            set => DisplayLdapMatchForIdentityClaimType = value;
        }
        [Persisted]
        private bool DisplayLdapMatchForIdentityClaimType;

        public string PickerEntityGroupNameProp
        {
            get => PickerEntityGroupName;
            set => PickerEntityGroupName = value;
        }
        [Persisted]
        private string PickerEntityGroupName;

        public bool FilterEnabledUsersOnlyProp
        {
            get => FilterEnabledUsersOnly;
            set => FilterEnabledUsersOnly = value;
        }
        [Persisted]
        private bool FilterEnabledUsersOnly;

        public bool FilterSecurityGroupsOnlyProp
        {
            get => FilterSecurityGroupsOnly;
            set => FilterSecurityGroupsOnly = value;
        }
        [Persisted]
        private bool FilterSecurityGroupsOnly;

        /// <summary>
        /// If true, LDAPCP will only return results that match exactly the input
        /// </summary>
        public bool FilterExactMatchOnlyProp
        {
            get => FilterExactMatchOnly;
            set => FilterExactMatchOnly = value;
        }
        [Persisted]
        private bool FilterExactMatchOnly;

        /// <summary>
        /// Timeout in seconds to wait for LDAP to return the result
        /// </summary>
        public int LDAPQueryTimeout
        {
            get => Timeout;
            set => Timeout = value;
        }
        [Persisted]
        private int Timeout;

        /// <summary>
        /// Should we care about the LDAP netbios name to consider 2 results as identical
        /// </summary>
        public bool CompareResultsWithDomainNameProp
        {
            get => CompareResultsWithDomainName;
            set => CompareResultsWithDomainName = value;
        }
        [Persisted]
        private bool CompareResultsWithDomainName = false;

        /// <summary>
        /// Set to true to enable augmentation. Property MainGroupClaimType must also be set for augmentation to work.
        /// </summary>
        public bool EnableAugmentation
        {
            get => AugmentationEnabled;
            set => AugmentationEnabled = value;
        }
        [Persisted]
        private bool AugmentationEnabled;

        /// <summary>
        /// Set the claim type that LDAPCP will use to create claims with the group membership of users during augmentation
        /// </summary>
        public string MainGroupClaimType
        {
            get => AugmentationClaimType;
            set => AugmentationClaimType = value;
        }
        [Persisted]
        private string AugmentationClaimType;

        public string EntityDisplayTextPrefix
        {
            get => _EntityDisplayTextPrefix;
            set => _EntityDisplayTextPrefix = value;
        }
        [Persisted]
        private string _EntityDisplayTextPrefix;

        /// <summary>
        /// Name of the SPTrustedLoginProvider where LDAPCP is enabled
        /// </summary>
        [Persisted]
        public string SPTrustName;

        private SPTrustedLoginProvider _SPTrust;
        private SPTrustedLoginProvider SPTrust
        {
            get
            {
                if (_SPTrust == null) _SPTrust = SPSecurityTokenServiceManager.Local.TrustedLoginProviders.GetProviderByName(SPTrustName);
                return _SPTrust;
            }
        }

        [Persisted]
        private string ClaimsProviderVersion;

        /// <summary>
        /// This property is not used by AzureCP and is available to developers for their own needs
        /// </summary>
        public string CustomData
        {
            get => _CustomData;
            set => _CustomData = value;
        }
        [Persisted]
        private string _CustomData;

        /// <summary>
        /// Limit number of results returned to SharePoint during a search
        /// </summary>
        public int MaxSearchResultsCount
        {
            get => _MaxSearchResultsCount;
            set => _MaxSearchResultsCount = value;
        }
        [Persisted]
        private int _MaxSearchResultsCount = 30; // SharePoint sets maxCount to 30 in method FillSearch

        public LDAPCPConfig(string persistedObjectName, SPPersistedObject parent, string spTrustName) : base(persistedObjectName, parent)
        {
            this.SPTrustName = spTrustName;
        }

        public LDAPCPConfig() { }

        /// <summary>
        /// Override this method to allow more users to update the object. True specifies that more users can update the object; otherwise, false. The default value is false.
        /// </summary>
        /// <returns></returns>
        protected override bool HasAdditionalUpdateAccess()
        {
            return false;
        }

        /// <summary>
        /// Returns the configuration of LDAPCP
        /// </summary>
        /// <returns></returns>
        public static LDAPCPConfig GetConfiguration()
        {
            return GetConfiguration(ClaimsProviderConstants.CONFIG_NAME, String.Empty);
        }

        /// <summary>
        /// Returns the configuration of LDAPCP
        /// </summary>
        /// <param name="persistedObjectName"></param>
        /// <returns></returns>
        public static LDAPCPConfig GetConfiguration(string persistedObjectName)
        {
            return GetConfiguration(persistedObjectName, String.Empty);
        }

        /// <summary>
        /// Returns the configuration of LDAPCP
        /// </summary>
        /// <param name="persistedObjectName">Name of the configuration</param>
        /// <param name="spTrustName">Name of the SPTrustedLoginProvider using the claims provider</param>
        public static LDAPCPConfig GetConfiguration(string persistedObjectName, string spTrustName)
        {
            SPPersistedObject parent = SPFarm.Local;
            try
            {
                LDAPCPConfig persistedObject = parent.GetChild<LDAPCPConfig>(persistedObjectName);
                if (persistedObject != null)
                {
                    persistedObject.CheckAndCleanConfiguration(spTrustName);
                    return persistedObject;
                }
            }
            catch (Exception ex)
            {
                ClaimsProviderLogging.LogException(String.Empty, $"while retrieving configuration '{persistedObjectName}'", TraceCategory.Configuration, ex);
            }
            return null;
        }

        /// <summary>
        /// Commit changes to configuration database
        /// </summary>
        public override void Update()
        {
            // In case ClaimTypes collection was modified, test if it is still valid before committed changes to database
            try
            {
                ClaimTypeConfigCollection testUpdateCollection = new ClaimTypeConfigCollection();
                testUpdateCollection.SPTrust = this.SPTrust;
                foreach (ClaimTypeConfig curCTConfig in this.ClaimTypes)
                {
                    testUpdateCollection.Add(curCTConfig, false);
                }
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException("Some changes made to list ClaimTypes are invalid and cannot be committed to configuration database. Inspect inner exception for more details about the error.", ex);
            }

            base.Update();
            ClaimsProviderLogging.Log($"Configuration '{base.DisplayName}' was updated successfully to version {base.Version} in configuration database.",
                TraceSeverity.Medium, EventSeverity.Information, TraceCategory.Core);
        }

        public static LDAPCPConfig ResetConfiguration(string persistedObjectName)
        {
            LDAPCPConfig previousConfig = GetConfiguration(persistedObjectName, String.Empty);
            if (previousConfig == null) return null;
            Guid configId = previousConfig.Id;
            string spTrustName = previousConfig.SPTrustName;
            DeleteConfiguration(persistedObjectName);
            LDAPCPConfig newConfig = CreateConfiguration(configId.ToString(), persistedObjectName, spTrustName);
            ClaimsProviderLogging.Log($"Configuration '{persistedObjectName}' was successfully reset to its default configuration",
                TraceSeverity.High, EventSeverity.Information, TraceCategory.Core);
            return newConfig;
        }

        /// <summary>
        /// Set properties of current configuration to their default values
        /// </summary>
        /// <returns></returns>
        public void ResetCurrentConfiguration()
        {
            LDAPCPConfig defaultConfig = ReturnDefaultConfiguration(this.SPTrustName) as LDAPCPConfig;
            ApplyConfiguration(defaultConfig);
            CheckAndCleanConfiguration(String.Empty);
        }

        /// <summary>
        /// Apply configuration in parameter to current object. It does not copy SharePoint base class members
        /// </summary>
        /// <param name="configToApply"></param>
        public void ApplyConfiguration(LDAPCPConfig configToApply)
        {
            // Copy non-inherited public fields
            // This copies persisted field SPTrustName (it doesn't have a corresponding property).
            // Private fields should not be retrieved here, since their corresponding properties are retrieved just after.
            FieldInfo[] fieldsToCopy = this.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (FieldInfo field in fieldsToCopy)
            {
                field.SetValue(this, field.GetValue(configToApply));
            }
            // Copy non-inherited public properties
            PropertyInfo[] propertiesToCopy = this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (PropertyInfo property in propertiesToCopy)
            {
                if (property.CanWrite)
                {
                    object value = property.GetValue(configToApply);
                    if (value != null)
                        property.SetValue(this, value);
                }
            }
        }

        /// <summary>
        /// Returns a copy of the current object. This copy does not have any member of the base SharePoint base class set
        /// </summary>
        /// <returns></returns>
        public LDAPCPConfig CopyConfiguration()
        {
            // Cannot use reflection here to copy object because of the calls to methods CopyConfiguration() on some properties
            LDAPCPConfig copy = new LDAPCPConfig();
            copy.SPTrustName = this.SPTrustName;
            copy.LDAPConnectionsProp = new List<LDAPConnection>();
            foreach (LDAPConnection currentCoco in this.LDAPConnectionsProp)
            {
                copy.LDAPConnectionsProp.Add(currentCoco.CopyConfiguration());
            }
            copy.ClaimTypes = new ClaimTypeConfigCollection();
            foreach (ClaimTypeConfig currentObject in this.ClaimTypes)
            {
                copy.ClaimTypes.Add(currentObject.CopyConfiguration(), false);
            }
            copy.BypassLDAPLookup = this.BypassLDAPLookup;
            copy.AddWildcardAsPrefixOfInput = this.AddWildcardAsPrefixOfInput;
            copy.DisplayLdapMatchForIdentityClaimTypeProp = this.DisplayLdapMatchForIdentityClaimTypeProp;
            copy.PickerEntityGroupNameProp = this.PickerEntityGroupNameProp;
            copy.FilterEnabledUsersOnlyProp = this.FilterEnabledUsersOnlyProp;
            copy.FilterSecurityGroupsOnlyProp = this.FilterSecurityGroupsOnlyProp;
            copy.FilterExactMatchOnlyProp = this.FilterExactMatchOnlyProp;
            copy.LDAPQueryTimeout = this.LDAPQueryTimeout;
            copy.CompareResultsWithDomainNameProp = this.CompareResultsWithDomainNameProp;
            copy.EnableAugmentation = this.EnableAugmentation;
            copy.MainGroupClaimType = this.MainGroupClaimType;
            copy.EntityDisplayTextPrefix = this.EntityDisplayTextPrefix;
            copy.CustomData = this.CustomData;
            copy.MaxSearchResultsCount = this.MaxSearchResultsCount;
            return copy;
        }

        public void ResetClaimTypesList()
        {
            ClaimTypes.Clear();
            ClaimTypes = ReturnDefaultClaimTypesConfig(this.SPTrustName);
            MainGroupClaimType = ClaimsProviderConstants.DefaultMainGroupClaimType;
            ClaimsProviderLogging.Log($"Claim types list of configuration '{Name}' was successfully reset to default configuration",
                TraceSeverity.High, EventSeverity.Information, TraceCategory.Core);
        }

        /// <summary>
        /// Create a persisted object with default configuration of LDAPCP. If it already exists, it will be deleted.
        /// </summary>
        /// <param name="persistedObjectID">GUID of persisted object</param>
        /// <param name="persistedObjectName">Name of persisted object</param>
        /// <returns></returns>
        public static LDAPCPConfig CreateConfiguration(string persistedObjectID, string persistedObjectName, string spTrustName)
        {
            if (String.IsNullOrEmpty(spTrustName))
            {
                throw new ArgumentNullException("spTrust");
            }

            // Ensure it doesn't already exists and delete it if so
            LDAPCPConfig existingConfig = LDAPCPConfig.GetConfiguration(persistedObjectName, String.Empty);
            if (existingConfig != null)
            {
                DeleteConfiguration(persistedObjectName);
            }

            ClaimsProviderLogging.Log($"Creating configuration '{persistedObjectName}' with Id {persistedObjectID}...", TraceSeverity.VerboseEx, EventSeverity.Error, TraceCategory.Core);
            LDAPCPConfig PersistedObject = new LDAPCPConfig(persistedObjectName, SPFarm.Local, spTrustName);
            PersistedObject.ResetCurrentConfiguration();
            PersistedObject.Id = new Guid(persistedObjectID);
            PersistedObject.Update();
            ClaimsProviderLogging.Log($"Created configuration '{persistedObjectName}' with Id {PersistedObject.Id}", TraceSeverity.Medium, EventSeverity.Information, TraceCategory.Core);
            return PersistedObject;
        }

        public static ILDAPCPConfiguration ReturnDefaultConfiguration(string spTrustName)
        {
            LDAPCPConfig defaultConfig = new LDAPCPConfig();
            defaultConfig.SPTrustName = spTrustName;
            defaultConfig.LDAPConnections = ReturnDefaultLDAPConnection();
            defaultConfig.ClaimTypes = ReturnDefaultClaimTypesConfig(spTrustName);
            defaultConfig.PickerEntityGroupNameProp = "Results";
            defaultConfig.BypassLDAPLookup = false;
            defaultConfig.AddWildcardAsPrefixOfInput = false;
            defaultConfig.FilterEnabledUsersOnlyProp = false;
            defaultConfig.FilterSecurityGroupsOnlyProp = false;
            defaultConfig.FilterExactMatchOnlyProp = false;
            defaultConfig.LDAPQueryTimeout = ClaimsProviderConstants.LDAPCPCONFIG_TIMEOUT;
            defaultConfig.EnableAugmentation = false;
            defaultConfig.MainGroupClaimType = ClaimsProviderConstants.DefaultMainGroupClaimType;
            return defaultConfig;
        }

        /// <summary>
        /// Generate and return default claim types configuration list
        /// </summary>
        /// <returns></returns>
        public static ClaimTypeConfigCollection ReturnDefaultClaimTypesConfig(string spTrustName)
        {
            if (String.IsNullOrWhiteSpace(spTrustName)) throw new ArgumentNullException("spTrustName cannot be null.");

            SPTrustedLoginProvider spTrust = SPSecurityTokenServiceManager.Local.TrustedLoginProviders.GetProviderByName(spTrustName);
            if (spTrust == null)
            {
                ClaimsProviderLogging.Log($"SPTrustedLoginProvider '{spTrustName}' was not found ", TraceSeverity.Unexpected, EventSeverity.Error, TraceCategory.Core);
                return null;
            }

            ClaimTypeConfigCollection newCTConfigCollection = new ClaimTypeConfigCollection()
            {
                // Claim types most liekly to be set as identity claim types
                new ClaimTypeConfig{EntityType = DirectoryObjectType.User, LDAPClass = "user", LDAPAttribute = "mail", ClaimType = WIF4_5.ClaimTypes.Email, EntityDataKey = PeopleEditorEntityDataKeys.Email},
                new ClaimTypeConfig{EntityType = DirectoryObjectType.User, LDAPClass = "user", LDAPAttribute = "userPrincipalName", ClaimType = WIF4_5.ClaimTypes.Upn},
                new ClaimTypeConfig{EntityType = DirectoryObjectType.User, LDAPClass = "user", LDAPAttribute = "sAMAccountName", ClaimType = WIF4_5.ClaimTypes.WindowsAccountName, AdditionalLDAPFilter = "(!(objectClass=computer))"},

                // Additional properties to find user and create entity with the identity claim type (UseMainClaimTypeOfDirectoryObject=true)
                new ClaimTypeConfig{EntityType = DirectoryObjectType.User, LDAPClass = "user", LDAPAttribute = "displayName", UseMainClaimTypeOfDirectoryObject = true, EntityDataKey = PeopleEditorEntityDataKeys.DisplayName},
                new ClaimTypeConfig{EntityType = DirectoryObjectType.User, LDAPClass = "user", LDAPAttribute = "cn", UseMainClaimTypeOfDirectoryObject = true, AdditionalLDAPFilter = "(!(objectClass=computer))"},
                new ClaimTypeConfig{EntityType = DirectoryObjectType.User, LDAPClass = "user", LDAPAttribute = "sn", UseMainClaimTypeOfDirectoryObject = true},
                new ClaimTypeConfig{EntityType = DirectoryObjectType.User, LDAPClass = "user", LDAPAttribute = "givenName", UseMainClaimTypeOfDirectoryObject = true},  // First name

                // Additional properties to populate metadata of entity created: no claim type set, EntityDataKey is set and UseMainClaimTypeOfDirectoryObject = false (default value)
                new ClaimTypeConfig{EntityType = DirectoryObjectType.User, LDAPClass = "user", LDAPAttribute="physicalDeliveryOfficeName", EntityDataKey = PeopleEditorEntityDataKeys.Location},
                new ClaimTypeConfig{EntityType = DirectoryObjectType.User, LDAPClass = "user", LDAPAttribute="title", EntityDataKey = PeopleEditorEntityDataKeys.JobTitle},
                new ClaimTypeConfig{EntityType = DirectoryObjectType.User, LDAPClass = "user", LDAPAttribute="msRTCSIP-PrimaryUserAddress", EntityDataKey = PeopleEditorEntityDataKeys.SIPAddress},
                new ClaimTypeConfig{EntityType = DirectoryObjectType.User, LDAPClass = "user", LDAPAttribute="telephoneNumber", EntityDataKey = PeopleEditorEntityDataKeys.WorkPhone},

                // Group
                new ClaimTypeConfig{EntityType = DirectoryObjectType.Group, LDAPClass = "group", LDAPAttribute="sAMAccountName", ClaimType = ClaimsProviderConstants.DefaultMainGroupClaimType, ClaimValuePrefix = @"{fqdn}\"},
                new ClaimTypeConfig{EntityType = DirectoryObjectType.Group, LDAPClass = "group", LDAPAttribute="displayName", UseMainClaimTypeOfDirectoryObject = true, EntityDataKey = PeopleEditorEntityDataKeys.DisplayName},
                new ClaimTypeConfig{EntityType = DirectoryObjectType.Group, LDAPClass = "user", LDAPAttribute="primaryGroupID", ClaimType = WIF4_5.ClaimTypes.PrimaryGroupSid, SupportsWildcard = false},
                new ClaimTypeConfig{EntityType = DirectoryObjectType.Group, LDAPClass = "group", LDAPAttribute="mail", EntityDataKey = PeopleEditorEntityDataKeys.Email},
            };
            newCTConfigCollection.SPTrust = spTrust;
            return newCTConfigCollection;
        }

        /// <summary>
        /// Return default LDAP connection list
        /// </summary>
        /// <returns></returns>
        public static List<LDAPConnection> ReturnDefaultLDAPConnection()
        {
            return new List<LDAPConnection>
            {
                new LDAPConnection{UserServerDirectoryEntry = true}
            };
        }

        /// <summary>
        /// Delete persisted object from configuration database
        /// </summary>
        /// <param name="persistedObjectName">Name of persisted object to delete</param>
        public static void DeleteConfiguration(string persistedObjectName)
        {
            LDAPCPConfig config = LDAPCPConfig.GetConfiguration(persistedObjectName, String.Empty);
            if (config == null)
            {
                ClaimsProviderLogging.Log($"Configuration '{persistedObjectName}' was not found in configuration database", TraceSeverity.Medium, EventSeverity.Error, TraceCategory.Core);
                return;
            }
            config.Delete();
            ClaimsProviderLogging.Log($"Configuration '{persistedObjectName}' was successfully deleted from configuration database", TraceSeverity.High, EventSeverity.Information, TraceCategory.Core);
        }

        /// <summary>
        /// Check if current configuration is compatible with current version of AzureCP, and fix it if not. If object comes from configuration database, changes are committed in configuration database
        /// </summary>
        /// <param name="spTrustName">Name of the SPTrust if it changed, null or empty string otherwise</param>
        /// <returns>Bollean indicates whether the configuration was updated in configuration database</returns>
        public bool CheckAndCleanConfiguration(string spTrustName)
        {
            // ClaimsProviderConstants.ClaimsProviderVersion can be null if assembly was removed from GAC
            if (String.IsNullOrEmpty(ClaimsProviderConstants.ClaimsProviderVersion))
                return false;

            bool configUpdated = false;

            if (!String.IsNullOrEmpty(spTrustName) && !String.Equals(this.SPTrustName, spTrustName, StringComparison.InvariantCultureIgnoreCase))
            {
                ClaimsProviderLogging.Log($"Updated property SPTrustName from \"{this.SPTrustName}\" to \"{spTrustName}\" in configuration \"{base.DisplayName}\".",
                    TraceSeverity.Medium, EventSeverity.Information, TraceCategory.Core);
                this.SPTrustName = spTrustName;
                configUpdated = true;
            }

            if (!String.Equals(this.ClaimsProviderVersion, ClaimsProviderConstants.ClaimsProviderVersion, StringComparison.InvariantCultureIgnoreCase))
            {
                // Detect if current assembly has a version different than AzureCPConfig.ClaimsProviderVersion. If so, config needs a sanity check
                ClaimsProviderLogging.Log($"Updated property ClaimsProviderVersion from \"{this.ClaimsProviderVersion}\" to \"{ClaimsProviderConstants.ClaimsProviderVersion}\" in configuration \"{base.DisplayName}\".",
                    TraceSeverity.Medium, EventSeverity.Information, TraceCategory.Core);
                this.ClaimsProviderVersion = ClaimsProviderConstants.ClaimsProviderVersion;
                configUpdated = true;
            }
            else if (!String.IsNullOrEmpty(this.SPTrustName))
            {
                // ClaimTypeConfigCollection.SPTrust is not persisted so it should always be set explicitely
                // Done in "else if" to not set this.ClaimTypes.SPTrust if we are not sure that this.ClaimTypes is in a good state
                this.ClaimTypes.SPTrust = this.SPTrust;
            }

            // Either claims provider was associated to a new SPTrustedLoginProvider
            // Or version of the current assembly changed (upgrade)
            // So let's do a sanity check of the configuration
            if (configUpdated)
            {
                try
                {
                    // If LDAPCP is updated from a version < v10, this.ClaimTypes.Count will throw a NullReferenceException
                    int testClaimTypeCollection = this.ClaimTypes.Count;
                }
                catch (NullReferenceException)
                {
                    this.ClaimTypes = LDAPCPConfig.ReturnDefaultClaimTypesConfig(this.SPTrustName);
                    configUpdated = true;
                }

                if (this.LDAPConnectionsProp == null)
                {
                    // LDAPConnections was introduced in v2.1 (SP2013). if LDAPCP is updated from an earlier version, LDAPConnections doesn't exist yet
                    this.LDAPConnectionsProp = ReturnDefaultLDAPConnection();
                    configUpdated = true;
                }

                if (!String.IsNullOrEmpty(this.SPTrustName))
                {
                    this.ClaimTypes.SPTrust = this.SPTrust;
                }

                // Changed in v11: 
                // Added property SPTrust / SPTrustName (SPTrustName must be set before this function runs to avoid deleting identity claim type)
                // Adding 2 times a ClaimTypeConfig with the same EntityType and same LDAPClass/LDAPAttribute now throws an InvalidOperationException
                // But this was possible before, so list this.ClaimTypes must be checked to be sure we are not in this scenario, and cleaned if so
                foreach (DirectoryObjectType entityType in Enum.GetValues(typeof(DirectoryObjectType)))
                {
                    var duplicatedPropertiesList = this.ClaimTypes.Where(x => x.EntityType == entityType)   // Check 1 EntityType
                                                              .GroupBy(x => new
                                                              {                           // Group by LDAPClass/LDAPAttribute
                                                                  x.LDAPClass,
                                                                  x.LDAPAttribute
                                                              })
                                                              .Select(x => new
                                                              {
                                                                  LDAPProperties = x.Key,
                                                                  ObjectCount = x.Count()       // For each LDAPClass/LDAPAttribute, how many items found
                                                              })
                                                              .Where(x => x.ObjectCount > 1);               // Keep only LDAPClass/LDAPAttribute found more than 1 time (for a given EntityType)
                    foreach (var duplicatedProperty in duplicatedPropertiesList)
                    {
                        ClaimTypeConfig ctConfigToDelete = null;
                        if (SPTrust != null && entityType == DirectoryObjectType.User)
                            ctConfigToDelete = this.ClaimTypes.FirstOrDefault(x => x.LDAPClass == duplicatedProperty.LDAPProperties.LDAPClass && x.LDAPAttribute == duplicatedProperty.LDAPProperties.LDAPAttribute && x.EntityType == entityType && !String.Equals(x.ClaimType, SPTrust.IdentityClaimTypeInformation.MappedClaimType, StringComparison.InvariantCultureIgnoreCase));
                        else if (entityType == DirectoryObjectType.Group && !String.IsNullOrEmpty(this.MainGroupClaimType))
                            ctConfigToDelete = this.ClaimTypes.FirstOrDefault(x => x.LDAPClass == duplicatedProperty.LDAPProperties.LDAPClass && x.LDAPAttribute == duplicatedProperty.LDAPProperties.LDAPAttribute && x.EntityType == entityType && !String.Equals(x.ClaimType, this.MainGroupClaimType, StringComparison.InvariantCultureIgnoreCase));
                        else
                            ctConfigToDelete = this.ClaimTypes.FirstOrDefault(x => x.LDAPClass == duplicatedProperty.LDAPProperties.LDAPClass && x.LDAPAttribute == duplicatedProperty.LDAPProperties.LDAPAttribute && x.EntityType == entityType);

                        this.ClaimTypes.Remove(ctConfigToDelete);
                        ClaimsProviderLogging.Log($"Removed claim type '{ctConfigToDelete.ClaimType}' from claim types configuration list because it duplicates LDAP attribute {ctConfigToDelete.LDAPAttribute} and LDAP class {ctConfigToDelete.LDAPClass}",
                           TraceSeverity.High, EventSeverity.Information, TraceCategory.Core);
                    }
                }

                if (Version > 0)
                {
                    try
                    {
                        // SPContext may be null if code does not run in a SharePoint process (e.g. in unit tests process)
                        if (SPContext.Current != null) SPContext.Current.Web.AllowUnsafeUpdates = true;
                        this.Update();
                        ClaimsProviderLogging.Log($"Configuration '{this.Name}' was upgraded in configuration database and some settings were updated or reset to their default configuration",
                            TraceSeverity.High, EventSeverity.Information, TraceCategory.Core);
                    }
                    catch (Exception)
                    {
                        // It may fail if current user doesn't have permission to update the object in configuration database
                        ClaimsProviderLogging.Log($"Configuration '{this.Name}' was upgraded locally, but changes could not be applied in configuration database. Please visit admin pages in central administration to upgrade configuration globally.",
                            TraceSeverity.High, EventSeverity.Information, TraceCategory.Core);
                    }
                    finally
                    {
                        if (SPContext.Current != null) SPContext.Current.Web.AllowUnsafeUpdates = false;
                    }
                }
            }
            return configUpdated;
        }
    }

    public class LDAPConnection : SPAutoSerializingObject
    {
        [Persisted]
        public Guid Id = Guid.NewGuid();
        public Guid IdProp
        {
            get => Id;
            set => Id = value;
        }

        /// <summary>
        /// LDAP Path of the connection LDAP://contoso.local:port/DC=contoso,DC=local
        /// </summary>
        [Persisted]
        public string Path;
        /// <summary>
        /// Internal only: shadow property of Path required for ASP.NET server side controls in admin pages.
        /// </summary>
        public string PathProp
        {
            get => Path;
            set => Path = value;
        }

        [Persisted]
        public string Username;

        [Persisted]
        public string Password;

        [Persisted]
        internal string Metadata;

        [Persisted]
        public AuthenticationTypes AuthenticationTypes;

        [Persisted]
        public bool UserServerDirectoryEntry;

        /// <summary>
        /// If true: this LDAPConnection will be be used for augmentation
        /// </summary>
        [Persisted]
        public bool AugmentationEnabled;
        /// <summary>
        /// Internal only: shadow property of AugmentationEnabled required for ASP.NET server side controls in admin pages.
        /// </summary>
        public bool AugmentationEnabledProp
        {
            get => AugmentationEnabled;
            set => AugmentationEnabled = value;
        }

        /// <summary>
        /// If true: get group membership with UserPrincipal.GetAuthorizationGroups()
        /// If false: get group membership with LDAP queries
        /// </summary>
        [Persisted]
        public bool GetGroupMembershipAsADDomain = true;
        /// <summary>
        /// Internal only: shadow property of GetGroupMembershipAsADDomain required for ASP.NET server side controls in admin pages.
        /// </summary>
        public bool GetGroupMembershipAsADDomainProp
        {
            get => GetGroupMembershipAsADDomain;
            set => GetGroupMembershipAsADDomain = value;
        }

        [Persisted]
        public string[] GroupMembershipAttributes = new string[] { "memberOf", "uniquememberof" };

        /// <summary>
        /// DirectoryEntry used to make LDAP queries
        /// </summary>
        public DirectoryEntry Directory;

        public string Filter;
        /// <summary>
        /// The domain name, for example "contoso"
        /// </summary>
        public string DomainName;

        /// <summary>
        /// The fully qualified domain name, for example "contoso.local"
        /// </summary>
        public string DomainFQDN;

        /// <summary>
        /// The container on the store to use as the root of the context, for example "DC=contoso,DC=local"
        /// </summary>
        public string RootContainer;

        public LDAPConnection()
        {
        }

        /// <summary>
        /// Returns a copy of the current object. This copy does not have any member of the base SharePoint base class set
        /// </summary>
        /// <returns></returns>
        internal LDAPConnection CopyConfiguration()
        {
            LDAPConnection copy = new LDAPConnection();
            // Copy non-inherited public fields
            FieldInfo[] fieldsToCopy = this.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (FieldInfo field in fieldsToCopy)
            {
                field.SetValue(copy, field.GetValue(this));
            }
            return copy;
        }
    }

    /// <summary>
    /// Contains information about current operation
    /// </summary>
    public class OperationContext
    {
        public const string RegexDomainFromFullAccountName = "(.*)\\\\.*";
        public const string RegexFullDomainFromEmail = ".*@(.*)";

        /// <summary>
        /// Indicates what kind of operation SharePoint is requesting
        /// </summary>
        public OperationType OperationType;

        /// <summary>
        /// Set only if request is a validation or an augmentation, to the incoming entity provided by SharePoint
        /// </summary>
        public SPClaim IncomingEntity;

        /// <summary>
        /// User submitting the query in the poeple picker, retrieved from HttpContext. Can be null
        /// </summary>
        public SPClaim UserInHttpContext;

        /// <summary>
        /// Uri provided by SharePoint
        /// </summary>
        public Uri UriContext;

        /// <summary>
        /// EntityTypes expected by SharePoint in the entities returned
        /// </summary>
        public DirectoryObjectType[] DirectoryObjectTypes;

        public string HierarchyNodeID;
        public int MaxCount;

        /// <summary>
        /// If search: it contains the raw input. If validation: it contains the incoming SPClaim value processed to be searchable against LDAP servers (domain tokens removed). LDAP special characters are NOT escaped
        /// </summary>
        public string Input;

        public bool InputHasKeyword;

        /// <summary>
        /// Indicates if search operation should return only results that exactly match the Input
        /// </summary>
        public bool ExactSearch;

        /// <summary>
        /// Set only if request is a validation or an augmentation, to the ClaimTypeConfig that matches the ClaimType of the incoming entity
        /// </summary>
        public ClaimTypeConfig IncomingEntityClaimTypeConfig;

        /// <summary>
        /// Contains the relevant list of ClaimTypeConfig for every type of request. In case of validation or augmentation, it will contain only 1 item.
        /// </summary>
        public List<ClaimTypeConfig> CurrentClaimTypeConfigList;

        public OperationContext(ILDAPCPConfiguration currentConfiguration, OperationType currentRequestType, List<ClaimTypeConfig> processedClaimTypeConfigList, string input, SPClaim incomingEntity, Uri context, string[] entityTypes, string hierarchyNodeID, int maxCount)
        {
            this.OperationType = currentRequestType;
            this.Input = input;
            this.IncomingEntity = incomingEntity;
            this.UriContext = context;
            this.HierarchyNodeID = hierarchyNodeID;
            this.MaxCount = maxCount;

            if (entityTypes != null)
            {
                List<DirectoryObjectType> aadEntityTypes = new List<DirectoryObjectType>();
                if (entityTypes.Contains(SPClaimEntityTypes.User))
                    aadEntityTypes.Add(DirectoryObjectType.User);
                if (entityTypes.Contains(ClaimsProviderConstants.GroupClaimEntityType))
                    aadEntityTypes.Add(DirectoryObjectType.Group);
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
                this.InitializeValidation(processedClaimTypeConfigList);
            }
            else if (currentRequestType == OperationType.Search)
            {
                this.InitializeSearch(processedClaimTypeConfigList, currentConfiguration.FilterExactMatchOnlyProp);
            }
            else if (currentRequestType == OperationType.Augmentation)
            {
                this.InitializeAugmentation(processedClaimTypeConfigList);
            }
        }

        /// <summary>
        /// Validation is when SharePoint expects exactly 1 PickerEntity from the incoming SPClaim
        /// </summary>
        /// <param name="processedClaimTypeConfigList"></param>
        protected void InitializeValidation(List<ClaimTypeConfig> processedClaimTypeConfigList)
        {
            if (this.IncomingEntity == null) throw new ArgumentNullException("IncomingEntity");
            this.IncomingEntityClaimTypeConfig = processedClaimTypeConfigList.FirstOrDefault(x =>
               String.Equals(x.ClaimType, this.IncomingEntity.ClaimType, StringComparison.InvariantCultureIgnoreCase) &&
               !x.UseMainClaimTypeOfDirectoryObject);

            if (this.IncomingEntityClaimTypeConfig == null)
            {
                ClaimsProviderLogging.Log($"[{LDAPCP._ProviderInternalName}] Unable to validate entity \"{this.IncomingEntity.Value}\" because its claim type \"{this.IncomingEntity.ClaimType}\" was not found in the ClaimTypes list of current configuration.", TraceSeverity.Unexpected, EventSeverity.Error, TraceCategory.Configuration);
                throw new InvalidOperationException($"[{LDAPCP._ProviderInternalName}] Unable validate entity \"{this.IncomingEntity.Value}\" because its claim type \"{this.IncomingEntity.ClaimType}\" was not found in the ClaimTypes list of current configuration.");
            }

            // CurrentClaimTypeConfigList must also be set
            this.CurrentClaimTypeConfigList = new List<ClaimTypeConfig>(1);
            this.CurrentClaimTypeConfigList.Add(this.IncomingEntityClaimTypeConfig);
            this.ExactSearch = true;
            this.Input = (!String.IsNullOrEmpty(IncomingEntityClaimTypeConfig.ClaimValuePrefix) && this.IncomingEntity.Value.StartsWith(IncomingEntityClaimTypeConfig.ClaimValuePrefix, StringComparison.InvariantCultureIgnoreCase)) ?
                this.IncomingEntity.Value.Substring(IncomingEntityClaimTypeConfig.ClaimValuePrefix.Length) : this.IncomingEntity.Value;

            // When working with domain tokens remove the domain part of the input so it can be found in AD
            if (IncomingEntityClaimTypeConfig.ClaimValuePrefix != null && (
                    IncomingEntityClaimTypeConfig.ClaimValuePrefix.Contains(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINNAME) ||
                    IncomingEntityClaimTypeConfig.ClaimValuePrefix.Contains(ClaimsProviderConstants.LDAPCPCONFIG_TOKENDOMAINFQDN)
                ))
            {
                this.Input = GetAccountFromFullAccountName(this.Input);
            }

            this.InputHasKeyword = (!String.IsNullOrEmpty(IncomingEntityClaimTypeConfig.ClaimValuePrefix) && !IncomingEntity.Value.StartsWith(IncomingEntityClaimTypeConfig.ClaimValuePrefix, StringComparison.InvariantCultureIgnoreCase) && IncomingEntityClaimTypeConfig.DoNotAddClaimValuePrefixIfBypassLookup) ? true : false;
        }

        /// <summary>
        /// Search is when SharePoint expects a list of any PickerEntity that match input provided
        /// </summary>
        /// <param name="processedClaimTypeConfigList"></param>
        protected void InitializeSearch(List<ClaimTypeConfig> processedClaimTypeConfigList, bool exactSearch)
        {
            this.ExactSearch = exactSearch;
            if (!String.IsNullOrEmpty(this.HierarchyNodeID))
            {
                // Restrict search to ClaimType currently selected in the hierarchy (may return multiple results if identity claim type)
                CurrentClaimTypeConfigList = processedClaimTypeConfigList.FindAll(x =>
                    String.Equals(x.ClaimType, this.HierarchyNodeID, StringComparison.InvariantCultureIgnoreCase) &&
                    this.DirectoryObjectTypes.Contains(x.EntityType));
            }
            else
            {
                // List<T>.FindAll returns an empty list if no result found: http://msdn.microsoft.com/en-us/library/fh1w7y8z(v=vs.110).aspx
                CurrentClaimTypeConfigList = processedClaimTypeConfigList.FindAll(x => this.DirectoryObjectTypes.Contains(x.EntityType));
            }
        }

        protected void InitializeAugmentation(List<ClaimTypeConfig> processedClaimTypeConfigList)
        {
            if (this.IncomingEntity == null) throw new ArgumentNullException("IncomingEntity");
            this.IncomingEntityClaimTypeConfig = processedClaimTypeConfigList.FirstOrDefault(x =>
               String.Equals(x.ClaimType, this.IncomingEntity.ClaimType, StringComparison.InvariantCultureIgnoreCase) &&
               !x.UseMainClaimTypeOfDirectoryObject);

            if (this.IncomingEntityClaimTypeConfig == null)
            {
                ClaimsProviderLogging.Log($"[{LDAPCP._ProviderInternalName}] Unable to augment entity \"{this.IncomingEntity.Value}\" because its claim type \"{this.IncomingEntity.ClaimType}\" was not found in the ClaimTypes list of current configuration.", TraceSeverity.Unexpected, EventSeverity.Error, TraceCategory.Configuration);
                throw new InvalidOperationException($"[{LDAPCP._ProviderInternalName}] Unable to augment entity \"{this.IncomingEntity.Value}\" because its claim type \"{this.IncomingEntity.ClaimType}\" was not found in the ClaimTypes list of current configuration.");
            }
        }

        /// <summary>
        /// Returns the account from "domain\account"
        /// </summary>
        /// <param name="fullAccountName">e.g. "contoso.local\account"</param>
        /// <returns>account</returns>
        public static string GetAccountFromFullAccountName(string fullAccountName)
        {
            if (fullAccountName.Contains(@"\"))
                return fullAccountName.Split(new char[] { '\\' }, 2)[1];
            else
                return fullAccountName;
        }

        /// <summary>
        /// Returns the domain from "domain\account"
        /// </summary>
        /// <param name="fullAccountName">e.g. "contoso.local\account"</param>
        /// <returns>e.g. "contoso.local"</returns>
        public static string GetDomainFromFullAccountName(string fullAccountName)
        {
            if (fullAccountName.Contains(@"\"))
                return fullAccountName.Split(new char[] { '\\' }, 2)[0];
            else
                return fullAccountName;
        }

        /// <summary>
        /// Return the domain FQDN from the given email
        /// </summary>
        /// <param name="email">e.g. yvand@contoso.local</param>
        /// <returns>e.g. contoso.local</returns>
        public static string GetFQDNFromEmail(string email)
        {
            return Regex.Replace(email, RegexFullDomainFromEmail, "$1", RegexOptions.None);
        }

        public static string GetFirstSubString(string value, string separator)
        {
            int stop = value.IndexOf(separator);
            return (stop > -1) ? value.Substring(0, stop) : string.Empty;
        }

        /// <summary>
        /// Extract domain name information from the distinguishedName supplied
        /// </summary>
        /// <param name="distinguishedName">distinguishedName to use to extract domain name information</param>
        /// <param name="domainName">Domain name</param>
        /// <param name="domainFQDN">Fully qualified domain name</param>
        public static void GetDomainInformation(string distinguishedName, out string domainName, out string domainFQDN)
        {
            // Retrieve FQDN and domain name of current DirectoryEntry
            domainName = domainFQDN = String.Empty;
            if (distinguishedName.Contains("DC="))
            {
                int start = distinguishedName.IndexOf("DC=", StringComparison.InvariantCultureIgnoreCase);
                string[] dnSplitted = distinguishedName.Substring(start).Split(new string[] { "DC=" }, StringSplitOptions.RemoveEmptyEntries);
                bool setDomainName = true;
                foreach (string dc in dnSplitted)
                {
                    domainFQDN += dc.Replace(',', '.');
                    if (setDomainName)
                    {
                        domainName = dc.Trim(new char[] { ',' });
                        setDomainName = false;
                    }
                }
            }
        }

        /// <summary>
        /// Query LDAP server to retrieve domain name information
        /// </summary>
        /// <param name="directory">LDAP Server to query</param>
        /// <param name="domainName">Domain name</param>
        /// <param name="domainFQDN">Fully qualified domain name</param>
        public static void GetDomainInformation(DirectoryEntry directory, out string domainName, out string domainFQDN)
        {
            string distinguishedName = String.Empty;
            domainName = domainFQDN = String.Empty;
            // Method PropertyCollection.Contains("distinguishedName") does a LDAP bind behind the scene
            if (directory.Properties.Contains("distinguishedName"))
            {
                distinguishedName = directory.Properties["distinguishedName"].Value.ToString();
                GetDomainInformation(distinguishedName, out domainName, out domainFQDN);
            }
            else
            {
                // This logic to get the domainName may not work with AD LDS:
                // if distinguishedName = "CN=Partition1,DC=MyLDS,DC=local", then both "name" and "cn" = "Partition1", while we expect "MyLDS"
                // So now it's only made if the distinguishedName is not available (very unlikely codepath)
                if (directory.Properties.Contains("name")) domainName = directory.Properties["name"].Value.ToString();
                else if (directory.Properties.Contains("cn")) domainName = directory.Properties["cn"].Value.ToString(); // Tivoli sets domain name in cn property (property name does not exist)
            }
        }

        /// <summary>
        /// Return the value from a distinguished name, or an empty string if not found.
        /// </summary>
        /// <param name="distinguishedNameValue">e.g. "CN=group1,CN=Users,DC=contoso,DC=local"</param>
        /// <returns>e.g. "group1", or an empty string if not found</returns>
        public static string GetValueFromDistinguishedName(string distinguishedNameValue)
        {
            int equalsIndex = distinguishedNameValue.IndexOf("=", 1);
            int commaIndex = distinguishedNameValue.IndexOf(",", 1);
            if (equalsIndex != -1 && commaIndex != -1)
                return distinguishedNameValue.Substring(equalsIndex + 1, commaIndex - equalsIndex - 1);
            else
                return String.Empty;
        }

        public static string EscapeSpecialCharacters(string stringWithSpecialChars)
        {
            string result = stringWithSpecialChars;
            foreach (KeyValuePair<string, string> kvp in ClaimsProviderConstants.SpecialCharacters)
            {
                result = result.Replace(kvp.Key, kvp.Value);
            }
            return result;
        }

        //public static string UnescapeSpecialCharacters(string stringWithEscapedChars)
        //{
        //    string result = stringWithEscapedChars;
        //    foreach (KeyValuePair<string, string> kvp in ClaimsProviderConstants.SpecialCharacters)
        //    {
        //        result = result.Replace(kvp.Value, kvp.Key);
        //    }
        //    return result;
        //}
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
}
