using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
using Microsoft.SharePoint.WebControls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.DirectoryServices;
using System.Linq;
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
    }

    public class ClaimsProviderConstants
    {
        public const string LDAPCPCONFIG_ID = "5D306A02-A262-48AC-8C44-BDB927620227";
        public const string LDAPCPCONFIG_NAME = "LdapcpConfig";
        public const string LDAPCPCONFIG_TOKENDOMAINNAME = "{domain}";
        public const string LDAPCPCONFIG_TOKENDOMAINFQDN = "{fqdn}";
        public const int LDAPCPCONFIG_TIMEOUT = 10;
        public static string GroupClaimEntityType = SPClaimEntityTypes.FormsRole;
        public const bool EnforceOnly1ClaimTypeForGroup = false;    // In LDAPCP, multiple claim types can be used to create group permissions
        public const string DefaultMainGroupClaimType = WIF4_5.ClaimTypes.Role;
        public const string PUBLICSITEURL = "https://ldapcp.com";
    }

    public class LDAPCPConfig : SPPersistedObject, ILDAPCPConfiguration
    {
        /// <summary>
        /// List of LDAP servers to query
        /// </summary>
        public List<LDAPConnection> LDAPConnectionsProp
        {
            get { return LDAPConnections; }
            set { LDAPConnections = value; }
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
                _ClaimTypesCollection = value.innerCol;
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
            get { return AlwaysResolveUserInput; }
            set { AlwaysResolveUserInput = value; }
        }
        [Persisted]
        private bool AlwaysResolveUserInput;

        /// <summary>
        /// NOT RECOMMENDED: Change filter to query "*input*" instead of "input*". This may have a strong negative impact on performance
        /// </summary>
        public bool AddWildcardAsPrefixOfInput
        {
            get { return AddWildcardInFrontOfQuery; }
            set { AddWildcardInFrontOfQuery = value; }
        }
        [Persisted]
        private bool AddWildcardInFrontOfQuery;

        public bool DisplayLdapMatchForIdentityClaimTypeProp
        {
            get { return DisplayLdapMatchForIdentityClaimType; }
            set { DisplayLdapMatchForIdentityClaimType = value; }
        }
        [Persisted]
        private bool DisplayLdapMatchForIdentityClaimType;

        public string PickerEntityGroupNameProp
        {
            get { return PickerEntityGroupName; }
            set { PickerEntityGroupName = value; }
        }
        [Persisted]
        private string PickerEntityGroupName;

        public bool FilterEnabledUsersOnlyProp
        {
            get { return FilterEnabledUsersOnly; }
            set { FilterEnabledUsersOnly = value; }
        }
        [Persisted]
        private bool FilterEnabledUsersOnly;

        public bool FilterSecurityGroupsOnlyProp
        {
            get { return FilterSecurityGroupsOnly; }
            set { FilterSecurityGroupsOnly = value; }
        }
        [Persisted]
        private bool FilterSecurityGroupsOnly;

        /// <summary>
        /// If true, LDAPCP will only return results that match exactly the input
        /// </summary>
        public bool FilterExactMatchOnlyProp
        {
            get { return FilterExactMatchOnly; }
            set { FilterExactMatchOnly = value; }
        }
        [Persisted]
        private bool FilterExactMatchOnly;

        /// <summary>
        /// Timeout in seconds to wait for LDAP to return the result
        /// </summary>
        public int LDAPQueryTimeout
        {
            get { return Timeout; }
            set { Timeout = value; }
        }
        [Persisted]
        private int Timeout;

        /// <summary>
        /// Should we care about the LDAP netbios name to consider 2 results as identical
        /// </summary>
        public bool CompareResultsWithDomainNameProp
        {
            get { return CompareResultsWithDomainName; }
            set { CompareResultsWithDomainName = value; }
        }
        [Persisted]
        private bool CompareResultsWithDomainName = false;

        /// <summary>
        /// Set to true to enable augmentation. Property MainGroupClaimType must also be set for augmentation to work.
        /// </summary>
        public bool EnableAugmentation
        {
            get { return AugmentationEnabled; }
            set { AugmentationEnabled = value; }
        }
        [Persisted]
        private bool AugmentationEnabled;

        /// <summary>
        /// Set the claim type that LDAPCP will use to create claims with the group membership of users during augmentation
        /// </summary>
        public string MainGroupClaimType
        {
            get { return AugmentationClaimType; }
            set { AugmentationClaimType = value; }
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
        private string SPTrustName;

        private SPTrustedLoginProvider _SPTrust;
        private SPTrustedLoginProvider SPTrust
        {
            get
            {
                if (_SPTrust == null) _SPTrust = SPSecurityTokenServiceManager.Local.TrustedLoginProviders.GetProviderByName(SPTrustName);
                return _SPTrust;
            }
        }

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
        /// Returns configuration of LDAPCP
        /// </summary>
        /// <returns></returns>
        public static LDAPCPConfig GetConfiguration()
        {
            return GetConfiguration(ClaimsProviderConstants.LDAPCPCONFIG_NAME);
        }

        /// <summary>
        /// Returns configuration specified by persistedObjectName
        /// </summary>
        /// <param name="persistedObjectName">Name of the persisted object that holds configuration to return</param>
        /// <returns></returns>
        public static LDAPCPConfig GetConfiguration(string persistedObjectName)
        {
            SPPersistedObject parent = SPFarm.Local;
            try
            {
                LDAPCPConfig persistedObject = parent.GetChild<LDAPCPConfig>(persistedObjectName);
                persistedObject.CheckAndCleanConfiguration(String.Empty);
                return persistedObject;
            }
            catch (Exception ex)
            {
                ClaimsProviderLogging.Log($"Error while retrieving configuration '{persistedObjectName}': {ex.Message}", TraceSeverity.Unexpected, EventSeverity.Error, TraceCategory.Core);
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
            LDAPCPConfig previousConfig = GetConfiguration(persistedObjectName);
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

        public void ApplyConfiguration(LDAPCPConfig configToApply)
        {
            this.LDAPConnectionsProp = configToApply.LDAPConnectionsProp;
            this.ClaimTypes = configToApply.ClaimTypes;
            this.BypassLDAPLookup = configToApply.BypassLDAPLookup;
            this.AddWildcardAsPrefixOfInput = configToApply.AddWildcardAsPrefixOfInput;
            this.DisplayLdapMatchForIdentityClaimTypeProp = configToApply.DisplayLdapMatchForIdentityClaimTypeProp;
            this.PickerEntityGroupNameProp = configToApply.PickerEntityGroupNameProp;
            this.FilterEnabledUsersOnlyProp = configToApply.FilterEnabledUsersOnlyProp;
            this.FilterSecurityGroupsOnlyProp = configToApply.FilterSecurityGroupsOnlyProp;
            this.FilterExactMatchOnlyProp = configToApply.FilterExactMatchOnlyProp;
            this.LDAPQueryTimeout = configToApply.LDAPQueryTimeout;
            this.CompareResultsWithDomainNameProp = configToApply.CompareResultsWithDomainNameProp;
            this.EnableAugmentation = configToApply.EnableAugmentation;
            this.MainGroupClaimType = configToApply.MainGroupClaimType;
            this.EntityDisplayTextPrefix = configToApply.EntityDisplayTextPrefix;
        }

        public LDAPCPConfig CopyPersistedProperties()
        {
            LDAPCPConfig copy = new LDAPCPConfig(this.Name, this.Parent, this.SPTrustName);
            copy.LDAPConnectionsProp = new List<LDAPConnection>();
            foreach (LDAPConnection currentCoco in this.LDAPConnectionsProp)
            {
                copy.LDAPConnectionsProp.Add(currentCoco.CopyPersistedProperties());
            }
            copy.ClaimTypes = new ClaimTypeConfigCollection();
            foreach (ClaimTypeConfig currentObject in this.ClaimTypes)
            {
                copy.ClaimTypes.Add(currentObject.CopyPersistedProperties(), false);
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
            return copy;
        }

        public void ResetClaimTypesList()
        {
            ClaimTypes.Clear();
            ClaimTypes = ReturnDefaultClaimTypesConfig();
            MainGroupClaimType = ClaimsProviderConstants.DefaultMainGroupClaimType;
            ClaimsProviderLogging.Log($"Claim types list of configuration '{Name}' was successfully reset to default configuration",
                TraceSeverity.High, EventSeverity.Information, TraceCategory.Core);
        }

        /// <summary>
        /// Create a persisted object with default configuration of LDAPCP.
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
            LDAPCPConfig existingConfig = LDAPCPConfig.GetConfiguration(persistedObjectName);
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
            defaultConfig.ClaimTypes = ReturnDefaultClaimTypesConfig();
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
        public static ClaimTypeConfigCollection ReturnDefaultClaimTypesConfig()
        {
            return new ClaimTypeConfigCollection
            {
                // Claim types most liekly to be set as identity claim types
                new ClaimTypeConfig{EntityType = DirectoryObjectType.User, LDAPClass = "user", LDAPAttribute = "mail", ClaimType = WIF4_5.ClaimTypes.Email, EntityDataKey = PeopleEditorEntityDataKeys.Email},
                new ClaimTypeConfig{EntityType = DirectoryObjectType.User, LDAPClass = "user", LDAPAttribute = "userPrincipalName", ClaimType = WIF4_5.ClaimTypes.Upn},
                new ClaimTypeConfig{EntityType = DirectoryObjectType.User, LDAPClass = "user", LDAPAttribute = "sAMAccountName", ClaimType = WIF4_5.ClaimTypes.WindowsAccountName, AdditionalLDAPFilter = "(!(objectClass=computer))"},

                // Additional properties to find user and create entity with the identity claim type (UseMainClaimTypeOfDirectoryObject=true)
                new ClaimTypeConfig{EntityType = DirectoryObjectType.User, LDAPClass = "user", LDAPAttribute = "displayName", UseMainClaimTypeOfDirectoryObject = true, EntityDataKey = PeopleEditorEntityDataKeys.DisplayName},
                new ClaimTypeConfig{EntityType = DirectoryObjectType.User, LDAPClass = "user", LDAPAttribute = "cn", UseMainClaimTypeOfDirectoryObject = true, AdditionalLDAPFilter = "(!(objectClass=computer))"},
                new ClaimTypeConfig{EntityType = DirectoryObjectType.User, LDAPClass = "user", LDAPAttribute = "sn", UseMainClaimTypeOfDirectoryObject = true},

                // Additional properties to populate metadata of entity created: no claim type set, EntityDataKey is set and UseMainClaimTypeOfDirectoryObject = false (default value)
                new ClaimTypeConfig{EntityType = DirectoryObjectType.User, LDAPClass = "user", LDAPAttribute="physicalDeliveryOfficeName", EntityDataKey = PeopleEditorEntityDataKeys.Location},
                new ClaimTypeConfig{EntityType = DirectoryObjectType.User, LDAPClass = "user", LDAPAttribute="title", EntityDataKey = PeopleEditorEntityDataKeys.JobTitle},
                new ClaimTypeConfig{EntityType = DirectoryObjectType.User, LDAPClass = "user", LDAPAttribute="msRTCSIP-PrimaryUserAddress", EntityDataKey = PeopleEditorEntityDataKeys.SIPAddress},
                new ClaimTypeConfig{EntityType = DirectoryObjectType.User, LDAPClass = "user", LDAPAttribute="telephoneNumber", EntityDataKey = PeopleEditorEntityDataKeys.WorkPhone},

                // Group
                new ClaimTypeConfig{EntityType = DirectoryObjectType.Group, LDAPClass = "group", LDAPAttribute="sAMAccountName", ClaimType = ClaimsProviderConstants.DefaultMainGroupClaimType, ClaimValuePrefix = @"{fqdn}\"},
                new ClaimTypeConfig{EntityType = DirectoryObjectType.Group, LDAPClass = "group", LDAPAttribute="displayName", UseMainClaimTypeOfDirectoryObject = true, EntityDataKey = PeopleEditorEntityDataKeys.DisplayName},
                new ClaimTypeConfig{EntityType = DirectoryObjectType.Group, LDAPClass = "user", LDAPAttribute="primaryGroupID", ClaimType = WIF4_5.ClaimTypes.PrimaryGroupSid, SupportsWildcard = false},
            };
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
            LDAPCPConfig config = LDAPCPConfig.GetConfiguration(persistedObjectName);
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
            bool objectCleaned = false;

            if (!String.IsNullOrEmpty(spTrustName) && !String.Equals(this.SPTrustName, spTrustName, StringComparison.InvariantCultureIgnoreCase))
            {
                this.SPTrustName = spTrustName;
                ClaimsProviderLogging.Log($"Updated the SPTrustedLoginProvider name to '{this.SPTrustName}' in configuration '{base.DisplayName}'.",
                    TraceSeverity.Medium, EventSeverity.Information, TraceCategory.Core);
                objectCleaned = true;
            }

            try
            {
                // If LDAPCP is updated from a version < v10, this.ClaimTypes.Count will throw a NullReferenceException
                int testClaimTypeCollection = this.ClaimTypes.Count;
            }
            catch (NullReferenceException ex)
            {
                this.ClaimTypes = LDAPCPConfig.ReturnDefaultClaimTypesConfig();
                objectCleaned = true;
            }

            if (this.LDAPConnectionsProp == null)
            {
                // LDAPConnections was introduced in v2.1 (SP2013). if LDAPCP is updated from an earlier version, LDAPConnections doesn't exist yet
                this.LDAPConnectionsProp = ReturnDefaultLDAPConnection();
                objectCleaned = true;
            }

            if (!String.IsNullOrEmpty(this.SPTrustName))
            {
                this.ClaimTypes.SPTrust = this.SPTrust;
            }

            if (objectCleaned)
            {
                if (Version > 0)
                {
                    try
                    {
                        SPContext.Current.Web.AllowUnsafeUpdates = true;
                        this.Update();
                        ClaimsProviderLogging.Log($"Configuration '{this.Name}' was not compatible with current version of LDAPCP and was updated in configuration database. Some settings were reset to their default configuration",
                            TraceSeverity.High, EventSeverity.Information, TraceCategory.Core);
                    }
                    catch (Exception ex)
                    {
                        // It may fail if current user doesn't have permission to update the object in configuration database
                        ClaimsProviderLogging.Log($"Configuration '{this.Name}' is not compatible with current version of LDAPCP and was updated locally, but change could not be applied in configuration database. Please visit admin pages in central administration to fix configuration globally.",
                            TraceSeverity.High, EventSeverity.Information, TraceCategory.Core);
                    }
                    finally
                    {
                        SPContext.Current.Web.AllowUnsafeUpdates = false;
                    }
                }
            }
            return objectCleaned;
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

        [Persisted]
        public string Path;
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
        public bool GetGroupMembershipAsADDomainProp
        {
            get => GetGroupMembershipAsADDomain;
            set => GetGroupMembershipAsADDomain = value;
        }

        /// <summary>
        /// DirectoryEntry used to make LDAP queries
        /// </summary>
        public DirectoryEntry Directory;

        public string Filter;

        public LDAPConnection()
        {
        }

        internal LDAPConnection CopyPersistedProperties()
        {
            return new LDAPConnection()
            {
                Id = this.Id,
                Path = this.Path,
                Username = this.Username,
                Password = this.Password,
                Metadata = this.Metadata,
                AuthenticationTypes = this.AuthenticationTypes,
                UserServerDirectoryEntry = this.UserServerDirectoryEntry,
                AugmentationEnabled = this.AugmentationEnabled,
                GetGroupMembershipAsADDomain = this.GetGroupMembershipAsADDomain,
            };
        }
    }

    /// <summary>
    /// Contains information about current operation
    /// </summary>
    public class OperationContext
    {
        public const string RegexAccountFromFullAccountName = ".*\\\\(.*)";
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
        /// If request is a validation: contains the value of the SPClaim. If request is a search: contains the input
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
            if (this.IncomingEntityClaimTypeConfig == null) return;

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
                Input = GetAccountFromFullAccountName(Input);
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
        }

        public static string GetAccountFromFullAccountName(string fullAccountName)
        {
            return Regex.Replace(fullAccountName, RegexAccountFromFullAccountName, "$1", RegexOptions.None);
        }

        /// <summary>
        /// Returns the string before the '\'
        /// </summary>
        /// <param name="fullAccountName">e.g. "mylds.local\ldsgroup1"</param>
        /// <returns>e.g. "mylds.local"</returns>
        public static string GetDomainFromFullAccountName(string fullAccountName)
        {
            return Regex.Replace(fullAccountName, RegexDomainFromFullAccountName, "$1", RegexOptions.None);
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

        public static void GetDomainInformation(DirectoryEntry directory, out string domainName, out string domainFQDN)
        {
            string distinguishedName = String.Empty;
            domainName = domainFQDN = String.Empty;
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

            // OLD LOGIC
            //// Retrieve FQDN and domain name of current DirectoryEntry
            //domainFQDN = String.Empty;
            //if (directory.Properties.Contains("distinguishedName"))
            //{
            //    distinguishedName = directory.Properties["distinguishedName"].Value.ToString();
            //    if (distinguishedName.Contains("DC="))
            //    {
            //        int start = distinguishedName.IndexOf("DC=", StringComparison.InvariantCultureIgnoreCase);
            //        string[] dnSplitted = distinguishedName.Substring(start).Split(new string[] { "DC=" }, StringSplitOptions.RemoveEmptyEntries);
            //        foreach (string dc in dnSplitted)
            //        {
            //            domainFQDN += dc.Replace(',', '.');
            //        }
            //    }
            //}

            //// This change is in order to implement the same logic as in LDAPCP.SearchOrValidateInLDAP()
            //domainName = OperationContext.GetFirstSubString(domainFQDN, ".");
            ////if (directory.Properties.Contains("name")) domainName = directory.Properties["name"].Value.ToString();
            ////else if (directory.Properties.Contains("cn")) domainName = directory.Properties["cn"].Value.ToString(); // Tivoli sets domain name in cn property (property name does not exist)
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
