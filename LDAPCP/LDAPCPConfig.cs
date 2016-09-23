using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
using Microsoft.SharePoint.WebControls;
using System;
using System.Collections.Generic;
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
        List<AttributeHelper> AttributesListProp { get; set; }
        bool AlwaysResolveUserInputProp { get; set; }
        bool AddWildcardInFrontOfQueryProp { get; set; }
        bool DisplayLdapMatchForIdentityClaimTypeProp { get; set; }
        string PickerEntityGroupNameProp { get; set; }
        bool FilterEnabledUsersOnlyProp { get; set; }
        bool FilterSecurityGroupsOnlyProp { get; set; }
        bool FilterExactMatchOnlyProp { get; set; }
        int TimeoutProp { get; set; }
        bool CompareResultsWithDomainNameProp { get; set; }
        bool AugmentationEnabledProp { get; set; }
        string AugmentationClaimTypeProp { get; set; }
    }

    
    public class LDAPCPConfig : SPPersistedObject, ILDAPCPConfiguration
    {
        public List<LDAPConnection> LDAPConnectionsProp
        {
            get { return LDAPConnections; }
            set { LDAPConnections = value; }
        }
        [Persisted]
        private List<LDAPConnection> LDAPConnections;

        public List<AttributeHelper> AttributesListProp
        {
            get { return AttributesList; }
            set { AttributesList = value; }
        }
        [Persisted]
        private List<AttributeHelper> AttributesList;

        public bool AlwaysResolveUserInputProp
        {
            get { return AlwaysResolveUserInput; }
            set { AlwaysResolveUserInput = value; }
        }
        [Persisted]
        private bool AlwaysResolveUserInput;

        public bool AddWildcardInFrontOfQueryProp
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

        public bool FilterExactMatchOnlyProp
        {
            get { return FilterExactMatchOnly; }
            set { FilterExactMatchOnly = value; }
        }
        [Persisted]
        private bool FilterExactMatchOnly;

        public int TimeoutProp
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

        public bool AugmentationEnabledProp
        {
            get { return AugmentationEnabled; }
            set { AugmentationEnabled = value; }
        }
        [Persisted]
        private bool AugmentationEnabled;

        public string AugmentationClaimTypeProp
        {
            get { return AugmentationClaimType; }
            set { AugmentationClaimType = value; }
        }
        [Persisted]
        private string AugmentationClaimType;

        public LDAPCPConfig(SPPersistedObject parent) : base(Constants.LDAPCPCONFIG_NAME, parent)
        { }

        public LDAPCPConfig(string name, SPPersistedObject parent) : base(name, parent)
        { }

        public LDAPCPConfig()
        { }

        protected override bool HasAdditionalUpdateAccess()
        {
            return false;
        }

        public static LDAPCPConfig GetFromConfigDB()
        {
            SPPersistedObject parent = SPFarm.Local;
            try
            {
                LDAPCPConfig persistedObject = parent.GetChild<LDAPCPConfig>(Constants.LDAPCPCONFIG_NAME);
                if (persistedObject != null)
                {
                    if (persistedObject.LDAPConnectionsProp == null)
                    {
                        // persistedObject.LDAPConnections introduced in v2.1 (SP2013)
                        // This can happen if LDAPCP was migrated from a previous version and LDAPConnections didn't exist yet in persisted object
                        persistedObject.LDAPConnectionsProp = GetDefaultLDAPConnection();
                        LdapcpLogging.Log(
                            String.Format("LDAP connections array is missing in the persisted object {0} and default connection was used. Visit LDAPCP admin page and validate it to create the array.", Constants.LDAPCPCONFIG_NAME),
                            TraceSeverity.High,
                            EventSeverity.Information,
                            LdapcpLogging.Categories.Configuration);
                    }
                }
                return persistedObject;
            }
            catch (Exception ex)
            {
                LdapcpLogging.LogException(LDAPCP._ProviderInternalName, String.Format("Error while retrieving SPPersistedObject {0}", Constants.LDAPCPCONFIG_NAME), LdapcpLogging.Categories.Core, ex);
            }
            return null;
        }

        public static void ResetClaimsList()
        {
            LDAPCPConfig persistedObject = GetFromConfigDB();
            if (persistedObject != null)
            {
                persistedObject.AttributesListProp.Clear();
                persistedObject.AttributesListProp = GetDefaultAttributesList();
                persistedObject.Update();

                LdapcpLogging.Log(
                    String.Format("Claims list of PersistedObject {0} was successfully reset to default relationship table", Constants.LDAPCPCONFIG_NAME),
                    TraceSeverity.High, EventSeverity.Information, LdapcpLogging.Categories.Core);
            }
            return;
        }

        /// <summary>
        /// Create the persisted object that contains default configuration of LDAPCP.
        /// It should be created only in central administration with application pool credentials
        /// because this is the only place where we are sure user has the permission to write in the config database
        /// </summary>
        public static LDAPCPConfig CreatePersistedObject()
        {
            LDAPCPConfig PersistedObject = GetDefaultConfiguration();
            PersistedObject.Id = new Guid(Constants.LDAPCPCONFIG_ID);
            try
            {
                PersistedObject.Update();
            }
            catch (NullReferenceException nullex)
            {
                // This exception occurs if an older version of the persisted object lives in the config database with a schema that doesn't match current one
                string stsadmcmd = String.Format("SELECT * FROM Objects WHERE Id LIKE '{0}'", Constants.LDAPCPCONFIG_ID);
                string error = String.Format("Unable to create PersistedObject {0}. This usually occurs because a persisted object with the same Id is used by another assembly (could be a previous version). Object is impossible to update or delete from Object Model unless you add the missing assembly to the GAC. You can see this object by running this query: \"{1}\"", PersistedObject.Name, stsadmcmd);

                LdapcpLogging.Log(error, TraceSeverity.Unexpected, EventSeverity.Error, LdapcpLogging.Categories.Core);

                // Tyy to delete it... but OM doesn't manage to get the object
                SPPersistedObject staleObject = SPFarm.Local.GetObject(new Guid(Constants.LDAPCPCONFIG_ID));
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

            LdapcpLogging.Log(
                String.Format("Created PersistedObject {0} with Id {1}", PersistedObject.Name, PersistedObject.Id),
                TraceSeverity.Medium, EventSeverity.Information, LdapcpLogging.Categories.Core);

            return PersistedObject;
        }

        public static void DeleteLDAPCPConfig()
        {
            LDAPCPConfig LdapcpConfig = LDAPCPConfig.GetFromConfigDB();
            if (LdapcpConfig != null) LdapcpConfig.Delete();
        }

        /// <summary>
        /// Creates a persisted object with default LDAPCP configuration. It won't be saved in configuration database unless Update() is called, but property Id should be set with a unique Guid before.
        /// </summary>
        /// <returns></returns>
        public static LDAPCPConfig GetDefaultConfiguration()
        {
            LDAPCPConfig PersistedObject = new LDAPCPConfig(SPFarm.Local);
            PersistedObject.AttributesList = GetDefaultAttributesList();
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

        public static List<AttributeHelper> GetDefaultAttributesList()
        {
            return new List<AttributeHelper>
            {
                new AttributeHelper{LDAPAttribute="mail", LDAPObjectClassProp="user", ClaimType=WIF.ClaimTypes.Email, ClaimEntityType = SPClaimEntityTypes.User, EntityDataKey=PeopleEditorEntityDataKeys.Email},
                new AttributeHelper{LDAPAttribute="sAMAccountName", LDAPObjectClassProp="user", ClaimType=WIF.ClaimTypes.WindowsAccountName, ClaimEntityType = SPClaimEntityTypes.User, AdditionalLDAPFilterProp="(!(objectClass=computer))"},
                new AttributeHelper{LDAPAttribute="userPrincipalName", LDAPObjectClassProp="user", ClaimType=WIF.ClaimTypes.Upn, ClaimEntityType = SPClaimEntityTypes.User},
                new AttributeHelper{LDAPAttribute="givenName", LDAPObjectClassProp="user", ClaimType=WIF.ClaimTypes.GivenName, ClaimEntityType = SPClaimEntityTypes.User},
                new AttributeHelper{LDAPAttribute="sAMAccountName", LDAPObjectClassProp="group", ClaimType=WIF.ClaimTypes.Role, ClaimEntityType = SPClaimEntityTypes.FormsRole, PrefixToAddToValueReturnedProp=@"{fqdn}\"},
                new AttributeHelper{LDAPAttribute="displayName", LDAPObjectClassProp="user", CreateAsIdentityClaim=true, EntityDataKey=PeopleEditorEntityDataKeys.DisplayName},
                new AttributeHelper{LDAPAttribute="cn", LDAPObjectClassProp="user", CreateAsIdentityClaim=true, AdditionalLDAPFilterProp="(!(objectClass=computer))"},
                new AttributeHelper{LDAPAttribute="sn", LDAPObjectClassProp="user", CreateAsIdentityClaim=true},
                new AttributeHelper{LDAPAttribute="physicalDeliveryOfficeName", LDAPObjectClassProp="user", EntityDataKey=PeopleEditorEntityDataKeys.Location},
                new AttributeHelper{LDAPAttribute="title", LDAPObjectClassProp="user", EntityDataKey=PeopleEditorEntityDataKeys.JobTitle},
                new AttributeHelper{LDAPAttribute="msRTCSIP-PrimaryUserAddress", LDAPObjectClassProp="user", EntityDataKey=PeopleEditorEntityDataKeys.SIPAddress},
                new AttributeHelper{LDAPAttribute="telephoneNumber", LDAPObjectClassProp="user", EntityDataKey=PeopleEditorEntityDataKeys.WorkPhone},
                new AttributeHelper{LDAPAttribute="displayName", LDAPObjectClassProp="group", CreateAsIdentityClaim=true, EntityDataKey=PeopleEditorEntityDataKeys.DisplayName},
            };
        }

        public static List<LDAPConnection> GetDefaultLDAPConnection()
        {
            return new List<LDAPConnection>
            {
                new LDAPConnection{UserServerDirectoryEntry=true}
            };
        }
    }

    //public interface IQueryObject
    //{
    //    string LDAPAttribute { get; set; }
    //    string LDAPObjectClassProp { get; set; }
    //    string ClaimTypeProp { get; set; }
    //    string ClaimEntityTypeProp { get; set; }
    //    string EntityDataKey { get; set; }
    //    bool CreateAsIdentityClaim { get; set; }
    //    string ClaimTypeMappingName { get; set; }
    //    string ClaimValueTypeProp { get; set; }
    //    string PrefixToAddToValueReturnedProp { get; set; }
    //    bool DoNotAddPrefixIfInputHasKeywordProp { get; set; }
    //    string PrefixToBypassLookup { get; set; }
    //    string LDAPAttributeToDisplayProp { get; set; }
    //    bool FilterExactMatchOnlyProp { get; set; }
    //    string AdditionalLDAPFilterProp { get; set; }
    //}

    /// <summary>
    /// Defines an attribute persisted in config database
    /// </summary>
    public class AttributeHelper : SPAutoSerializingObject
    {
        /// <summary>
        /// Name of the attribute in LDAP
        /// </summary>
        public string LDAPAttribute
        {
            get { return LDAPAttributeName; }
            set { LDAPAttributeName = value; }
        }
        [Persisted]
        private string LDAPAttributeName;

        /// <summary>
        /// Class of the attribute in LDAP, typically user or group
        /// </summary>
        public string LDAPObjectClassProp
        {
            get { return LDAPObjectClass; }
            set { LDAPObjectClass = value; }
        }
        [Persisted]
        private string LDAPObjectClass;

        /// <summary>
        /// define the claim type associated with the attribute that must map the claim type defined in the sp trust
        /// for example "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"
        /// </summary>
        public string ClaimType
        {
            get { return claimType; }
            set { claimType = value; }
        }
        [Persisted]
        private string claimType;

        /// <summary>
        /// SPClaimEntityTypes enum that represents the type of permission (a user, a role, a security group, etc...)
        /// </summary>
        public string ClaimEntityType
        {
            get { return claimEntityType; }
            set { claimEntityType = value; }
        }
        [Persisted]
        private string claimEntityType = SPClaimEntityTypes.User;

        /// <summary>
        /// When creating a PickerEntry, it's possible to populate entry with additional attributes stored in EntityData hash table
        /// </summary>
        public string EntityDataKey
        {
            get { return peopleEditorEntityDataKey; }
            set { peopleEditorEntityDataKey = value; }
        }
        [Persisted]
        private string peopleEditorEntityDataKey;

        /// <summary>
        /// Set to true if the attribute should always be queried in LDAP even if it is not defined in the SP trust (typically displayName and cn attributes)
        /// </summary>
        public bool CreateAsIdentityClaim
        {
            get { return ResolveAsIdentityClaim; }
            set { ResolveAsIdentityClaim = value; }
        }
        [Persisted]
        private bool ResolveAsIdentityClaim = false;

        /// <summary>
        /// This attribute is not intended to be used or modified in your code
        /// </summary>
        public string ClaimTypeMappingName
        {
            get { return peoplePickerAttributeDisplayName; }
            set { peoplePickerAttributeDisplayName = value; }
        }
        [Persisted]
        private string peoplePickerAttributeDisplayName;

        /// <summary>
        /// Every claim value type is a string by default
        /// </summary>
        public string ClaimValueType
        {
            get { return claimValueType; }
            set { claimValueType = value; }
        }
        [Persisted]
        private string claimValueType = WIF.ClaimValueTypes.String;

        /// <summary>
        /// This prefix is added to the value of the permission created. This is useful to add a domain name before a group name (for example "domain\group" instead of "group")
        /// </summary>
        public string PrefixToAddToValueReturnedProp
        {
            get { return PrefixToAddToValueReturned; }
            set { PrefixToAddToValueReturned = value; }
        }
        [Persisted]
        private string PrefixToAddToValueReturned;

        /// <summary>
        /// If set to true: permission created without LDAP lookup (possible if KeywordToValidateInputWithoutLookup is set and user typed this keyword in the input) should not contain the prefix (set in PrefixToAddToValueReturned) in the value
        /// </summary>
        public bool DoNotAddPrefixIfInputHasKeywordProp
        {
            get { return DoNotAddPrefixIfInputHasKeyword; }
            set { DoNotAddPrefixIfInputHasKeyword = value; }
        }
        [Persisted]
        private bool DoNotAddPrefixIfInputHasKeyword;

        /// <summary>
        /// Set this to tell LDAPCP to validate user input (and create the permission) without LDAP lookup if it contains this keyword at the beginning
        /// </summary>
        public string PrefixToBypassLookup
        {
            get { return KeywordToValidateInputWithoutLookup; }
            set { KeywordToValidateInputWithoutLookup = value; }
        }
        [Persisted]
        private string KeywordToValidateInputWithoutLookup;

        /// <summary>
        /// Set this property to customize display text of the permission with a specific LDAP attribute (different than LDAPAttributeName, that is the actual value of the permission)
        /// </summary>
        public string LDAPAttributeToDisplayProp
        {
            get { return LDAPAttributeToDisplay; }
            set { LDAPAttributeToDisplay = value; }
        }
        [Persisted]
        private string LDAPAttributeToDisplay;

        /// <summary>
        /// Set to only return values that exactly match the user input
        /// </summary>
        public bool FilterExactMatchOnlyProp
        {
            get { return FilterExactMatchOnly; }
            set { FilterExactMatchOnly = value; }
        }
        [Persisted]
        private bool FilterExactMatchOnly = false;

        /// <summary>
        /// Set this property to specify make LDAP lookup on this attribute more restrictive
        /// </summary>
        public string AdditionalLDAPFilterProp
        {
            get { return AdditionalLDAPFilter; }
            set { AdditionalLDAPFilter = value; }
        }
        [Persisted]
        private string AdditionalLDAPFilter;

        /// <summary>
        /// Set to true to show the display name of claim type in parenthesis in display text of permission
        /// </summary>
        public bool ShowClaimNameInDisplayText
        {
            get { return showClaimNameInDisplayText; }
            set { showClaimNameInDisplayText = value; }
        }
        [Persisted]
        private bool showClaimNameInDisplayText = true;

        public AttributeHelper()
        {
        }

        public AttributeHelper CopyPersistedProperties()
        {
            AttributeHelper newAtt = new AttributeHelper()
            {
                AdditionalLDAPFilter = this.AdditionalLDAPFilter,
                claimEntityType = this.claimEntityType,
                claimType = this.claimType,
                claimValueType = this.claimValueType,
                DoNotAddPrefixIfInputHasKeyword = this.DoNotAddPrefixIfInputHasKeyword,
                FilterExactMatchOnly = this.FilterExactMatchOnly,
                KeywordToValidateInputWithoutLookup = this.KeywordToValidateInputWithoutLookup,
                LDAPAttributeName = this.LDAPAttributeName,
                LDAPAttributeToDisplay = this.LDAPAttributeToDisplay,
                LDAPObjectClass = this.LDAPObjectClass,
                peopleEditorEntityDataKey = this.peopleEditorEntityDataKey,
                peoplePickerAttributeDisplayName = this.peoplePickerAttributeDisplayName,
                PrefixToAddToValueReturned = this.PrefixToAddToValueReturned,
                ResolveAsIdentityClaim = this.ResolveAsIdentityClaim,
                showClaimNameInDisplayText = this.showClaimNameInDisplayText,
            };
            return newAtt;
        }

        //protected override void OnDeserialization()
        //{
        //    base.OnDeserialization();
        //}

        //public LDAPClaimProviderTrustConfiguration(string name, SPPersistedObject parent)
        //    : base(
        //{
        //}
    }

    public class LDAPConnection : SPAutoSerializingObject
    {
        [Persisted]
        public Guid Id = Guid.NewGuid();
        public Guid IdProp
        {
            get { return Id; }
            set { Id = value; }
        }

        [Persisted]
        public string Path;
        public string PathProp
        {
            get { return Path; }
            set { Path = value; }
        }

        [Persisted]
        public string Username;

        [Persisted]
        public string Password;

        [Persisted]
        public string Metadata;

        [Persisted]
        public List<string> ResolvedNetBiosDomainNames;

        /// <summary>
        /// Specifies the types of authentication
        /// http://msdn.microsoft.com/en-us/library/system.directoryservices.authenticationtypes(v=vs.110).aspx
        /// </summary>
        [Persisted]
        public AuthenticationTypes AuthenticationTypes;

        [Persisted]
        public bool UserServerDirectoryEntry;

        [Persisted]
        public bool AugmentationEnabled;
        public bool AugmentationEnabledProp
        {
            get { return AugmentationEnabled; }
            set { AugmentationEnabled = value; }
        }

        public LDAPConnection()
        {
        }

        internal LDAPConnection CopyPersistedProperties()
        {
            LDAPConnection copy = new LDAPConnection()
            {
                Id = this.Id,
                Path = this.Path,
                Username = this.Username,
                Password = this.Password,
                Metadata = this.Metadata,
                AuthenticationTypes = this.AuthenticationTypes,
                UserServerDirectoryEntry = this.UserServerDirectoryEntry,
                AugmentationEnabled = this.AugmentationEnabled,
                ResolvedNetBiosDomainNames = this.ResolvedNetBiosDomainNames
            };
            return copy;
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
        public ILDAPCPConfiguration CurrentConfiguration;

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
        public AttributeHelper Attribute;
        public List<AttributeHelper> Attributes;

        public RequestInformation(ILDAPCPConfiguration currentConfiguration, RequestType currentRequestType, List<AttributeHelper> processedAttributes, string input, SPClaim incomingEntity, Uri context, string[] entityTypes, string hierarchyNodeID, int maxCount)
        {
            this.CurrentConfiguration = currentConfiguration;
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
                this.InitializeValidation(processedAttributes);
            }
            else if (currentRequestType == RequestType.Search)
            {
                this.InitializeSearch(processedAttributes);
            }
            else if (currentRequestType == RequestType.Augmentation)
            {
                this.InitializeAugmentation(processedAttributes);
            }
        }

        /// <summary>
        /// Validation is when SharePoint asks LDAPCP to return 1 PickerEntity from a given SPClaim
        /// </summary>
        /// <param name="ProcessedAttributes"></param>
        protected void InitializeValidation(List<AttributeHelper> ProcessedAttributes)
        {
            if (this.IncomingEntity == null) throw new ArgumentNullException("claimToValidate");
            this.Attribute = FindAttribute(ProcessedAttributes, this.IncomingEntity.ClaimType);
            if (this.Attribute == null) return;
            this.Attributes = new List<AttributeHelper>() { this.Attribute };
            this.ExactSearch = true;
            this.Input = (!String.IsNullOrEmpty(Attribute.PrefixToAddToValueReturnedProp) && this.IncomingEntity.Value.StartsWith(Attribute.PrefixToAddToValueReturnedProp, StringComparison.InvariantCultureIgnoreCase)) ?
                this.IncomingEntity.Value.Substring(Attribute.PrefixToAddToValueReturnedProp.Length) : this.IncomingEntity.Value;

            // When working with domain tokens remove the domain part of the input so it can be found in AD
            if (Attribute.PrefixToAddToValueReturnedProp != null && (
                    Attribute.PrefixToAddToValueReturnedProp.Contains(Constants.LDAPCPCONFIG_TOKENDOMAINNAME) ||
                    Attribute.PrefixToAddToValueReturnedProp.Contains(Constants.LDAPCPCONFIG_TOKENDOMAINFQDN)
                ))
                Input = GetAccountFromFullAccountName(Input);

            this.InputHasKeyword = (!String.IsNullOrEmpty(Attribute.PrefixToAddToValueReturnedProp) && !IncomingEntity.Value.StartsWith(Attribute.PrefixToAddToValueReturnedProp, StringComparison.InvariantCultureIgnoreCase) && Attribute.DoNotAddPrefixIfInputHasKeywordProp) ? true : false;
        }

        /// <summary>
        /// Search is when SharePoint asks LDAPCP to return all PickerEntity that match input provided
        /// </summary>
        /// <param name="ProcessedAttributes"></param>
        protected void InitializeSearch(List<AttributeHelper> ProcessedAttributes)
        {
            this.ExactSearch = this.CurrentConfiguration.FilterExactMatchOnlyProp;
            this.Input = this.OriginalInput;
            if (!String.IsNullOrEmpty(this.HierarchyNodeID))
            {
                // Restrict search to attributes currently selected in the hierarchy (may return multiple results if identity claim type)
                Attributes = ProcessedAttributes.FindAll(x =>
                    String.Equals(x.ClaimType, this.HierarchyNodeID, StringComparison.InvariantCultureIgnoreCase) &&
                    this.EntityTypes.Contains(x.ClaimEntityType));
            }
            else
            {
                // List<T>.FindAll returns an empty list if no result found: http://msdn.microsoft.com/en-us/library/fh1w7y8z(v=vs.110).aspx
                Attributes = ProcessedAttributes.FindAll(x => this.EntityTypes.Contains(x.ClaimEntityType));
            }
        }

        protected void InitializeAugmentation(List<AttributeHelper> ProcessedAttributes)
        {
            if (this.IncomingEntity == null) throw new ArgumentNullException("claimToValidate");
            this.Attribute = FindAttribute(ProcessedAttributes, this.IncomingEntity.ClaimType);
            if (this.Attribute == null) return;
        }

        public static AttributeHelper FindAttribute(List<AttributeHelper> processedAttributes, string claimType)
        {
            var Attributes = processedAttributes.FindAll(x =>
                String.Equals(x.ClaimType, claimType, StringComparison.InvariantCultureIgnoreCase)
                && !x.CreateAsIdentityClaim);
            if (Attributes.Count != 1)
            {
                // Should always find only 1 attribute at this stage
                LdapcpLogging.Log(String.Format("[{0}] Found {1} attributes that match the claim type \"{2}\", but exactly 1 is expected. Verify that there is no duplicate claim type. Aborting operation.", LDAPCP._ProviderInternalName, Attributes.Count.ToString(), claimType), TraceSeverity.Unexpected, EventSeverity.Error, LdapcpLogging.Categories.Claims_Picking);
                return null;
            }
            return Attributes.First();
        }

        public static string GetAccountFromFullAccountName(string fullAccountName)
        {
            return Regex.Replace(fullAccountName, RegexAccountFromFullAccountName, "$1", RegexOptions.None);
        }

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
            // Retrieve FQDN and domain name of current DirectoryEntry
            string distinguishedName = String.Empty;
            domainFQDN = String.Empty;
            if (directory.Properties.Contains("distinguishedName"))
            {
                distinguishedName = directory.Properties["distinguishedName"].Value.ToString();
                if (distinguishedName.Contains("DC="))
                {
                    int start = distinguishedName.IndexOf("DC=", StringComparison.InvariantCultureIgnoreCase);
                    string[] dnSplitted = distinguishedName.Substring(start).Split(new string[] { "DC=" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string dc in dnSplitted)
                    {
                        domainFQDN += dc.Replace(',', '.');
                    }
                }
            }
            domainName = String.Empty;
            if (directory.Properties.Contains("name")) domainName = directory.Properties["name"].Value.ToString();
            else if (directory.Properties.Contains("cn")) domainName = directory.Properties["cn"].Value.ToString(); // Tivoli sets domain name in cn property (property name does not exist)
        }

        public static DirectorySearcher ResolveRootDirectorySearcher(DirectoryEntry directoryEntry, string distinguishedName, string provider, string dnsDomainName, string username, string password, AuthenticationTypes authenticationType)
        {
            DirectoryEntry searchRoot = null;

            if (directoryEntry.Properties["distinguishedName"].Value != null)
            {
                distinguishedName = directoryEntry.Properties["distinguishedName"].Value.ToString();
            }

            if (distinguishedName.ToUpper().Contains("OU="))
            {
                // distinguished name contains OU (Organizational Units), so we need to parse to only have DC (Domain Components) elements in our DirectoryEntry path
                var domainComponents = ResolveDnsDomainName(distinguishedName).Split('.');
                distinguishedName = string.Empty;
                var componentCount = 1;
                foreach (var component in domainComponents)
                {
                    distinguishedName += "DC=" + component + (componentCount < domainComponents.Length ? "," : "");
                    componentCount++;
                }
            }

            // Every AD forest does have Configuration Node. Here is how we target it e.g. LDAP://contoso.com/cn=Partitions,cn=Configuration,dn=contoso,dn=com
            searchRoot = new DirectoryEntry(String.Format("{0}://{1}/cn=Partitions,cn=Configuration,{2}", provider, dnsDomainName, distinguishedName), username, password, authenticationType);

            var searcher = new DirectorySearcher(searchRoot);
            return searcher;
        }

        public static string ResolveDomainFromDirectoryPath(string directory)
        {
            var dnsDomainName = String.Empty;

            if (directory.Contains("/"))
            {
                var domainConfiguration = directory.Split('/')[0];
                // example for validating connection string similar to following: <domain>/ou=<some_value>,ou=<some_value>,dc=<subdomain>,dc=<domain>,dc=<ch>
                if (!IsValidDomain(domainConfiguration) && (domainConfiguration.Contains("DC") || (domainConfiguration.Contains("dc"))))
                {
                    // it is not a domain name, resolve all DC (Domain Component) parameters as a valid domain and ignore all the rest
                    dnsDomainName = ResolveDnsDomainName(domainConfiguration);
                }
                else
                {
                    // it is valid domain name, extract it
                    dnsDomainName = domainConfiguration;
                }
            }
            else
            {
                if (!IsValidDomain(directory))
                {
                    // it is not a domain name, resolve all DC (Domain Component) parameters as a valid domain and ignore all the rest
                    dnsDomainName = ResolveDnsDomainName(directory);
                }
                else
                {
                    // it is valid domain name, extract it
                    dnsDomainName = directory;
                }
            }
            return dnsDomainName;
        }

        public static bool IsValidDomain(string directoryPath)
        {
            if (Regex.IsMatch(directoryPath, @" # Rev:2013-03-26
                      # Match DNS host domain having one or more subdomains.
                      # Top level domain subset taken from IANA.ORG. See:
                      # http://data.iana.org/TLD/tlds-alpha-by-domain.txt
                      ^                  # Anchor to start of string.
                      (?!.{256})         # Whole domain must be 255 or less.
                      (?:                # Group for one or more sub-domains.
                        [a-z0-9]         # Either subdomain length from 2-63.
                        [a-z0-9-]{0,61}  # Middle part may have dashes.
                        [a-z0-9]         # Starts and ends with alphanum.
                        \.               # Dot separates subdomains.
                      | [a-z0-9]         # or subdomain length == 1 char.
                        \.               # Dot separates subdomains.
                      )+                 # One or more sub-domains.
                      (?:                # Top level domain alternatives.
                        [a-z]{2}         # Either any 2 char country code,
                      | AERO|ARPA|ASIA|BIZ|CAT|COM|COOP|EDU|  # or TLD 
                        GOV|INFO|INT|JOBS|MIL|MOBI|MUSEUM|    # from list.
                        NAME|NET|ORG|POST|PRO|TEL|TRAVEL  # IANA.ORG
                      )                  # End group of TLD alternatives.
                      $                  # Anchor to end of string.",
                RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace))
            {
                return true;
            }
            return false;
        }

        private static string ResolveDnsDomainName(string configuration)
        {
            string pattern = @"(s*)dc=([^,]+)";
            string output = string.Empty;

            MatchCollection matches = Regex.Matches(configuration, pattern, RegexOptions.IgnoreCase);

            var matchCount = 1;
            foreach (Match match in matches)
            {
                output += match.Value.Split(new[] { "Dc=", "DC=", "dc=", "dC=" }, StringSplitOptions.None)[1] + (matchCount < matches.Count ? "," : "");
                matchCount++;
            }

            if (output.Contains(","))
            {
                var components = output.Split(',');
                var domain = string.Empty;

                var componentCount = 1;
                foreach (var component in components)
                {
                    domain += component + (componentCount < components.Length ? "." : "");
                    componentCount++;
                }

                if (!string.IsNullOrEmpty(domain))
                {
                    output = domain;
                }
            }

            return output;
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
        public List<string> NetBiosDomainNames;
        public string Filter;
    }
}
