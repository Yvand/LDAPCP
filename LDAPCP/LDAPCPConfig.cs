using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
using Microsoft.SharePoint.WebControls;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.DirectoryServices;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using WIF = System.Security.Claims;
using WIF3_5 = Microsoft.IdentityModel.Claims;

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
        string ClaimTypeUsedForAugmentation { get; set; }
    }

    public class Constants
    {
        public const string LDAPCPCONFIG_ID = "5D306A02-A262-48AC-8C44-BDB927620227";
        public const string LDAPCPCONFIG_NAME = "LdapcpConfig";
        public const string LDAPCPCONFIG_TOKENDOMAINNAME = "{domain}";
        public const string LDAPCPCONFIG_TOKENDOMAINFQDN = "{fqdn}";
        public const int LDAPCPCONFIG_TIMEOUT = 10;
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
                if (innerClaimTypes == null)
                {
                    innerClaimTypes = new ClaimTypeConfigCollection(ref this._ClaimTypesCollection);
                }
                //else
                //{
                //    _ClaimTypesCollection = innerClaimTypes.innerCol;
                //}
                return innerClaimTypes;
            }
            set
            {
                innerClaimTypes = value;
                _ClaimTypesCollection = value.innerCol;
            }
        }
        [Persisted]
        private Collection<ClaimTypeConfig> _ClaimTypesCollection;

        private ClaimTypeConfigCollection innerClaimTypes;

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

        //[Persisted]
        //public SPOriginalIssuerType LDAPCPIssuerType;

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
        /// Set to true to enable augmentation. Property ClaimTypeUsedForAugmentation must also be set for augmentation to work.
        /// </summary>
        public bool EnableAugmentation
        {
            get { return AugmentationEnabled; }
            set { AugmentationEnabled = value; }
        }
        [Persisted]
        private bool AugmentationEnabled;

        /// <summary>
        /// Claim type to use for the groups created by LDAPCP during augmentation
        /// </summary>
        public string ClaimTypeUsedForAugmentation
        {
            get { return AugmentationClaimType; }
            set { AugmentationClaimType = value; }
        }
        [Persisted]
        private string AugmentationClaimType;

        public LDAPCPConfig(string name, SPPersistedObject parent) : base(name, parent)
        { }

        public LDAPCPConfig()
        { }

        /// <summary>
        /// Override this method to allow more users to update the object. True specifies that more users can update the object; otherwise, false. The default value is false.
        /// </summary>
        /// <returns></returns>
        protected override bool HasAdditionalUpdateAccess()
        {
            return false;
        }

        /// <summary>
        /// Return configuration of LDAPCP
        /// </summary>
        /// <returns></returns>
        public static LDAPCPConfig GetConfiguration()
        {
            return GetConfiguration(Constants.LDAPCPCONFIG_NAME);
        }

        /// <summary>
        /// Return configuration specified by persistedObjectName
        /// </summary>
        /// <param name="persistedObjectName">Name of the persisted object that holds configuration to return</param>
        /// <returns></returns>
        public static LDAPCPConfig GetConfiguration(string persistedObjectName)
        {
            SPPersistedObject parent = SPFarm.Local;
            try
            {
                LDAPCPConfig persistedObject = parent.GetChild<LDAPCPConfig>(persistedObjectName);
                if (persistedObject != null)
                {
                    if (persistedObject.LDAPConnectionsProp == null)
                    {
                        // persistedObject.LDAPConnections introduced in v2.1 (SP2013)
                        // This can happen if LDAPCP was migrated from a previous version and LDAPConnections didn't exist yet in persisted object
                        persistedObject.LDAPConnectionsProp = GetDefaultLDAPConnection();
                        LdapcpLogging.Log($"LDAP connections list was missing in the persisted object {persistedObjectName} and default configuration was used. Visit LDAPCP admin page and validate it to create the list.",
                            TraceSeverity.High, EventSeverity.Information, LdapcpLogging.Categories.Configuration);
                    }

                    if (persistedObject.ClaimTypes == null)
                    {
                        // Breaking change in v10: ClaimTypes implementation changed with new name/type/propertyNames, so persisted object from previous versions cannot be read anymore
                        persistedObject.ClaimTypes = GetDefaultClaimTypesConfig();
                        LdapcpLogging.Log($"ClaimTypes configuration list was missing in the persisted object {persistedObjectName} and default configuration was applied. Visit LDAPCP claims configuration page to check and edit the list.",
                            TraceSeverity.High, EventSeverity.Information, LdapcpLogging.Categories.Configuration);
                    }
                }
                return persistedObject;
            }
            catch (Exception ex)
            {
                LdapcpLogging.LogException(LDAPCP._ProviderInternalName, $"Error while retrieving SPPersistedObject {persistedObjectName}", LdapcpLogging.Categories.Core, ex);
            }
            return null;
        }

        /// <summary>
        /// Commit changes in configuration database
        /// </summary>
        public override void Update()
        {
            base.Update();
            LdapcpLogging.Log($"PersistedObject {base.DisplayName} was updated successfully.",
                TraceSeverity.Medium, EventSeverity.Information, LdapcpLogging.Categories.Core);
        }

        public LDAPCPConfig CloneInReadOnlyObject()
        {
            //return this.Clone() as LDAPCPConfig;
            LDAPCPConfig newConfig = new LDAPCPConfig();
            newConfig.BypassLDAPLookup = this.BypassLDAPLookup;
            newConfig.AddWildcardAsPrefixOfInput = this.AddWildcardAsPrefixOfInput;
            newConfig.PickerEntityGroupNameProp = this.PickerEntityGroupNameProp;
            newConfig.DisplayLdapMatchForIdentityClaimTypeProp = this.DisplayLdapMatchForIdentityClaimTypeProp;
            newConfig.FilterEnabledUsersOnlyProp = this.FilterEnabledUsersOnlyProp;
            newConfig.FilterSecurityGroupsOnlyProp = this.FilterSecurityGroupsOnlyProp;
            newConfig.FilterExactMatchOnlyProp = this.FilterExactMatchOnlyProp;
            newConfig.LDAPQueryTimeout = this.LDAPQueryTimeout;
            newConfig.EnableAugmentation = this.EnableAugmentation;
            newConfig.ClaimTypeUsedForAugmentation = this.ClaimTypeUsedForAugmentation;
            newConfig.ClaimTypes = new ClaimTypeConfigCollection();
            foreach (ClaimTypeConfig currentObject in this.ClaimTypes)
            {
                newConfig.ClaimTypes.Add(currentObject.CopyPersistedProperties());
            }
            newConfig.LDAPConnectionsProp = new List<LDAPConnection>();
            foreach (LDAPConnection currentCoco in this.LDAPConnectionsProp)
            {
                newConfig.LDAPConnectionsProp.Add(currentCoco.CopyPersistedProperties());
            }
            return newConfig;
        }

        public void ResetClaimTypesList()
        {
            ClaimTypes.Clear();
            ClaimTypes = GetDefaultClaimTypesConfig();
            LdapcpLogging.Log($"Claim types list of PersistedObject {Name} was successfully reset to default configuration",
                TraceSeverity.High, EventSeverity.Information, LdapcpLogging.Categories.Core);
        }

        /// <summary>
        /// Create a persisted object initialized with default configuration
        /// </summary>
        /// <param name="persistedObjectID">GUID of persisted object</param>
        /// <param name="persistedObjectName">Name of persisted object</param>
        /// <returns></returns>
        public static LDAPCPConfig CreatePersistedObject(string persistedObjectID, string persistedObjectName)
        {
            LdapcpLogging.Log($"Creating persisted object {persistedObjectName} with ID {persistedObjectID}...", TraceSeverity.Medium, EventSeverity.Error, LdapcpLogging.Categories.Core);
            LDAPCPConfig PersistedObject = GetDefaultConfiguration(persistedObjectName);
            PersistedObject.Id = new Guid(persistedObjectID);
            try
            {
                PersistedObject.Update();
            }
            catch (ArgumentException argExc)
            {
                // This exception is recorded when persisted object already exists in config database. Let's get it and return it.
                LdapcpLogging.LogException(String.Empty, $": Could not create persisted object {persistedObjectName} and save it in configuration database because it already exists. Returning the one already created in configuration database...", LdapcpLogging.Categories.Core, argExc);
                return GetConfiguration(persistedObjectName);
            }
            catch (NullReferenceException nullex)
            {
                // This exception occurs if an older version of the persisted object lives in the config database with a schema that doesn't match current one
                string stsadmcmd = String.Format("SELECT * FROM Objects WHERE Id LIKE '{0}'", persistedObjectID);
                string error = $"Unable to create PersistedObject '{persistedObjectName}'. This usually occurs because a persisted object with the same Id is used by another assembly (could be a previous version). Object is impossible to update or delete from Object Model unless you add the missing assembly to the GAC. You can see this object by running this query: \"{stsadmcmd}\"";
                LdapcpLogging.LogException(String.Empty, error, LdapcpLogging.Categories.Configuration, nullex);

                // Tyy to delete it... but OM doesn't manage to get the object
                SPPersistedObject staleObject = SPFarm.Local.GetObject(new Guid(persistedObjectID));
                if (staleObject != null)
                {
                    staleObject.Delete();
                    PersistedObject.Update();
                }
                else
                {
                    throw new Exception(error, nullex);
                }
            }
            catch (Exception ex)
            {
                // Catch all other exception types and log them
                LdapcpLogging.LogException(String.Empty, $": Could not create persisted object {persistedObjectName} and save it in configuration database.", LdapcpLogging.Categories.Core, ex);
                return null;
            }

            LdapcpLogging.Log($"Created PersistedObject {PersistedObject.Name} with Id {PersistedObject.Id}",
                TraceSeverity.Verbose, EventSeverity.Information, LdapcpLogging.Categories.Core);

            return PersistedObject;
        }

        /// <summary>
        /// Delete persisted object from configuration database
        /// </summary>
        /// <param name="persistedObjectName">Name of persisted object to delete</param>
        public static void DeleteLDAPCPConfig(string persistedObjectName)
        {
            LDAPCPConfig LdapcpConfig = LDAPCPConfig.GetConfiguration(persistedObjectName);
            if (LdapcpConfig == null)
            {
                LdapcpLogging.Log($"Persisted object {persistedObjectName} was not found in configuration database", TraceSeverity.Medium, EventSeverity.Error, LdapcpLogging.Categories.Core);
                return;
            }
            LdapcpConfig.Delete();
            LdapcpLogging.Log($"Persisted object {persistedObjectName} was successfully deleted from configuration database", TraceSeverity.Medium, EventSeverity.Error, LdapcpLogging.Categories.Core);
        }

        /// <summary>
        /// Return default configuration in a in-memory only object. It won't be saved in configuration database unless Update() is called, but property Id should be set with a unique Guid before.
        /// </summary>
        /// <returns></returns>
        public static LDAPCPConfig GetDefaultConfiguration(string persistedObjectName)
        {
            LDAPCPConfig PersistedObject = new LDAPCPConfig(persistedObjectName, SPFarm.Local);
            PersistedObject.ClaimTypes = GetDefaultClaimTypesConfig();
            PersistedObject.LDAPConnections = GetDefaultLDAPConnection();
            PersistedObject.PickerEntityGroupName = "Results";
            PersistedObject.AlwaysResolveUserInput = false;
            PersistedObject.AddWildcardInFrontOfQuery = false;
            PersistedObject.FilterEnabledUsersOnly = false;
            PersistedObject.FilterSecurityGroupsOnly = false;
            //PersistedObject.LDAPCPIssuerType = SPOriginalIssuerType.TrustedProvider;
            PersistedObject.FilterExactMatchOnly = false;
            PersistedObject.Timeout = Constants.LDAPCPCONFIG_TIMEOUT;
            PersistedObject.AugmentationEnabled = false;
            PersistedObject.AugmentationClaimType = String.Empty;
            return PersistedObject;
        }

        /// <summary>
        /// Return default claim type configuration list
        /// </summary>
        /// <returns></returns>
        public static ClaimTypeConfigCollection GetDefaultClaimTypesConfig()
        {
            return new ClaimTypeConfigCollection
            {
                new ClaimTypeConfig{LDAPAttribute="mail", LDAPClass="user", ClaimType=WIF.ClaimTypes.Email, ClaimEntityType = SPClaimEntityTypes.User, EntityDataKey=PeopleEditorEntityDataKeys.Email},
                new ClaimTypeConfig{LDAPAttribute="sAMAccountName", LDAPClass="user", ClaimType=WIF.ClaimTypes.WindowsAccountName, ClaimEntityType = SPClaimEntityTypes.User, AdditionalLDAPFilter="(!(objectClass=computer))"},
                new ClaimTypeConfig{LDAPAttribute="userPrincipalName", LDAPClass="user", ClaimType=WIF.ClaimTypes.Upn, ClaimEntityType = SPClaimEntityTypes.User},
                new ClaimTypeConfig{LDAPAttribute="givenName", LDAPClass="user", ClaimType=WIF.ClaimTypes.GivenName, ClaimEntityType = SPClaimEntityTypes.User},
                new ClaimTypeConfig{LDAPAttribute="sAMAccountName", LDAPClass="group", ClaimType=WIF.ClaimTypes.Role, ClaimEntityType = SPClaimEntityTypes.FormsRole, ClaimValuePrefix=@"{fqdn}\"},
                new ClaimTypeConfig{LDAPAttribute="displayName", LDAPClass="user", CreateAsIdentityClaim=true, EntityDataKey=PeopleEditorEntityDataKeys.DisplayName},
                new ClaimTypeConfig{LDAPAttribute="cn", LDAPClass="user", CreateAsIdentityClaim=true, AdditionalLDAPFilter="(!(objectClass=computer))"},
                new ClaimTypeConfig{LDAPAttribute="sn", LDAPClass="user", CreateAsIdentityClaim=true},
                new ClaimTypeConfig{LDAPAttribute="physicalDeliveryOfficeName", LDAPClass="user", EntityDataKey=PeopleEditorEntityDataKeys.Location},
                new ClaimTypeConfig{LDAPAttribute="title", LDAPClass="user", EntityDataKey=PeopleEditorEntityDataKeys.JobTitle},
                new ClaimTypeConfig{LDAPAttribute="msRTCSIP-PrimaryUserAddress", LDAPClass="user", EntityDataKey=PeopleEditorEntityDataKeys.SIPAddress},
                new ClaimTypeConfig{LDAPAttribute="telephoneNumber", LDAPClass="user", EntityDataKey=PeopleEditorEntityDataKeys.WorkPhone},
                new ClaimTypeConfig{LDAPAttribute="displayName", LDAPClass="group", CreateAsIdentityClaim=true, EntityDataKey=PeopleEditorEntityDataKeys.DisplayName},
            };
        }

        /// <summary>
        /// Return default LDAP connection list
        /// </summary>
        /// <returns></returns>
        public static List<LDAPConnection> GetDefaultLDAPConnection()
        {
            return new List<LDAPConnection>
            {
                new LDAPConnection{UserServerDirectoryEntry=true}
            };
        }
    }



    public class LDAPConnection : SPAutoSerializingObject
    {
        [Persisted]
        internal Guid Id = Guid.NewGuid();
        public Guid IdProp
        {
            get { return Id; }
            set { Id = value; }
        }

        [Persisted]
        internal string Path;
        public string PathProp
        {
            get { return Path; }
            set { Path = value; }
        }

        [Persisted]
        internal string Username;

        [Persisted]
        internal string Password;

        [Persisted]
        internal string Metadata;

        /// <summary>
        /// Specifies the types of authentication
        /// http://msdn.microsoft.com/en-us/library/system.directoryservices.authenticationtypes(v=vs.110).aspx
        /// </summary>
        [Persisted]
        internal AuthenticationTypes AuthenticationTypes;

        [Persisted]
        internal bool UserServerDirectoryEntry;

        /// <summary>
        /// If true: this server will be queried to perform augmentation
        /// </summary>
        [Persisted]
        public bool AugmentationEnabled;
        public bool AugmentationEnabledProp
        {
            get { return AugmentationEnabled; }
            set { AugmentationEnabled = value; }
        }


        /// <summary>
        /// If true: get group membership with UserPrincipal.GetAuthorizationGroups()
        /// If false: get group membership with LDAP queries
        /// </summary>
        [Persisted]
        public bool GetGroupMembershipAsADDomain = true;
        public bool GetGroupMembershipAsADDomainProp
        {
            get { return GetGroupMembershipAsADDomain; }
            set { GetGroupMembershipAsADDomain = value; }
        }

        /// <summary>
        /// DirectoryEntry used to make LDAP queries
        /// </summary>
        public DirectoryEntry LDAPServer;

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
    /// Contains information about current request
    /// </summary>
    public class RequestInformation
    {
        public static string RegexAccountFromFullAccountName { get { return ".*\\\\(.*)"; } }
        //public static string RegexDomainFromFullAccountName { get { return "(.*)\\\\(.*)"; } }
        public static string RegexDomainFromFullAccountName { get { return "(.*)\\\\.*"; } }
        public static string RegexFullDomainFromEmail { get { return ".*@(.*)"; } }

        /// <summary>
        /// Current LDAPCP configuration
        /// </summary>
        //public ILDAPCPConfiguration CurrentConfiguration;

        /// <summary>
        /// Indicates what kind of operation SharePoint is sending to LDAPCP
        /// </summary>
        public RequestType RequestType;

        /// <summary>
        /// SPClaim sent in parameter to LDAPCP. Can be null
        /// </summary>
        public SPClaim IncomingEntity;

        /// <summary>
        /// User submitting the query in the poeple picker, retrieved from HttpContext. Can be null
        /// </summary>
        public SPClaim UserInHttpContext;

        public Uri Context;
        public string[] EntityTypes;
        private string OriginalInput;
        public string HierarchyNodeID;
        public int MaxCount;

        public string Input;
        public bool InputHasKeyword;
        public bool ExactSearch;
        public ClaimTypeConfig IdentityClaimTypeConfig;
        public List<ClaimTypeConfig> ClaimTypesConfigList;

        public RequestInformation(ILDAPCPConfiguration currentConfiguration, RequestType currentRequestType, List<ClaimTypeConfig> processedClaimTypeConfigList, string input, SPClaim incomingEntity, Uri context, string[] entityTypes, string hierarchyNodeID, int maxCount)
        {
            //this.CurrentConfiguration = currentConfiguration;
            this.RequestType = currentRequestType;
            this.OriginalInput = input;
            this.IncomingEntity = incomingEntity;
            this.Context = context;
            this.EntityTypes = entityTypes;
            this.HierarchyNodeID = hierarchyNodeID;
            this.MaxCount = maxCount;

            HttpContext httpctx = HttpContext.Current;
            if (httpctx != null)
            {
                WIF3_5.ClaimsPrincipal cp = httpctx.User as WIF3_5.ClaimsPrincipal;
                // cp is typically null in central administration
                if (cp != null) this.UserInHttpContext = SPClaimProviderManager.Local.DecodeClaimFromFormsSuffix(cp.Identity.Name);
            }

            if (currentRequestType == RequestType.Validation)
            {
                this.InitializeValidation(processedClaimTypeConfigList);
            }
            else if (currentRequestType == RequestType.Search)
            {
                this.InitializeSearch(processedClaimTypeConfigList, currentConfiguration.FilterExactMatchOnlyProp);
            }
            else if (currentRequestType == RequestType.Augmentation)
            {
                this.InitializeAugmentation(processedClaimTypeConfigList);
            }
        }

        /// <summary>
        /// Validation is when SharePoint expects LDAPCP to return 1 PickerEntity from a given SPClaim
        /// </summary>
        /// <param name="processedClaimTypeConfigList"></param>
        protected void InitializeValidation(List<ClaimTypeConfig> processedClaimTypeConfigList)
        {
            if (this.IncomingEntity == null) throw new ArgumentNullException("claimToValidate");
            this.IdentityClaimTypeConfig = FindClaimTypeConfig(processedClaimTypeConfigList, this.IncomingEntity.ClaimType);
            if (this.IdentityClaimTypeConfig == null) return;
            this.ClaimTypesConfigList = new List<ClaimTypeConfig>() { this.IdentityClaimTypeConfig };
            this.ExactSearch = true;
            this.Input = (!String.IsNullOrEmpty(IdentityClaimTypeConfig.ClaimValuePrefix) && this.IncomingEntity.Value.StartsWith(IdentityClaimTypeConfig.ClaimValuePrefix, StringComparison.InvariantCultureIgnoreCase)) ?
                this.IncomingEntity.Value.Substring(IdentityClaimTypeConfig.ClaimValuePrefix.Length) : this.IncomingEntity.Value;

            // When working with domain tokens remove the domain part of the input so it can be found in AD
            if (IdentityClaimTypeConfig.ClaimValuePrefix != null && (
                    IdentityClaimTypeConfig.ClaimValuePrefix.Contains(Constants.LDAPCPCONFIG_TOKENDOMAINNAME) ||
                    IdentityClaimTypeConfig.ClaimValuePrefix.Contains(Constants.LDAPCPCONFIG_TOKENDOMAINFQDN)
                ))
                Input = GetAccountFromFullAccountName(Input);

            this.InputHasKeyword = (!String.IsNullOrEmpty(IdentityClaimTypeConfig.ClaimValuePrefix) && !IncomingEntity.Value.StartsWith(IdentityClaimTypeConfig.ClaimValuePrefix, StringComparison.InvariantCultureIgnoreCase) && IdentityClaimTypeConfig.DoNotAddClaimValuePrefixIfBypassLookup) ? true : false;
        }

        /// <summary>
        /// Search is when SharePoint expects LDAPCP to return all PickerEntity that match input provided
        /// </summary>
        /// <param name="processedClaimTypeConfigList"></param>
        protected void InitializeSearch(List<ClaimTypeConfig> processedClaimTypeConfigList, bool exactSearch)
        {
            this.ExactSearch = exactSearch;
            this.Input = this.OriginalInput;
            if (!String.IsNullOrEmpty(this.HierarchyNodeID))
            {
                // Restrict search to attributes currently selected in the hierarchy (may return multiple results if identity claim type)
                ClaimTypesConfigList = processedClaimTypeConfigList.FindAll(x =>
                    String.Equals(x.ClaimType, this.HierarchyNodeID, StringComparison.InvariantCultureIgnoreCase) &&
                    this.EntityTypes.Contains(x.ClaimEntityType));
            }
            else
            {
                // List<T>.FindAll returns an empty list if no result found: http://msdn.microsoft.com/en-us/library/fh1w7y8z(v=vs.110).aspx
                ClaimTypesConfigList = processedClaimTypeConfigList.FindAll(x => this.EntityTypes.Contains(x.ClaimEntityType));
            }
        }

        protected void InitializeAugmentation(List<ClaimTypeConfig> processedClaimTypeConfigList)
        {
            if (this.IncomingEntity == null) throw new ArgumentNullException("claimToValidate");
            this.IdentityClaimTypeConfig = FindClaimTypeConfig(processedClaimTypeConfigList, this.IncomingEntity.ClaimType);
            if (this.IdentityClaimTypeConfig == null) return;
        }

        public static ClaimTypeConfig FindClaimTypeConfig(List<ClaimTypeConfig> processedClaimTypeConfigList, string claimType)
        {
            //FINDTOWHERE
            var Attributes = processedClaimTypeConfigList.Where(x =>
                String.Equals(x.ClaimType, claimType, StringComparison.InvariantCultureIgnoreCase)
                && !x.CreateAsIdentityClaim);
            if (Attributes.Count() != 1)
            {
                // Should always find only 1 attribute at this stage
                LdapcpLogging.Log(String.Format("[{0}] Found {1} attributes that match the claim type \"{2}\", but exactly 1 is expected. Verify that there is no duplicate claim type. Aborting operation.", LDAPCP._ProviderInternalName, Attributes.Count(), claimType), TraceSeverity.Unexpected, EventSeverity.Error, LdapcpLogging.Categories.Claims_Picking);
                return null;
            }
            return Attributes.First();
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

            //// This change is in order to implement the same logic as in LDAPCP.SearchOrValidateWithLDAP()
            //domainName = RequestInformation.GetFirstSubString(domainFQDN, ".");
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

    public enum RequestType
    {
        Search,
        Validation,
        Augmentation,
    }

    public class LDAPConnectionSettings
    {
        public DirectoryEntry Directory;
        public string Filter;
    }
}
