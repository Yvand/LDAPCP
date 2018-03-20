using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
using Microsoft.SharePoint.WebControls;
using System;
using System.Linq;
using System.Web.UI;

namespace ldapcp.ControlTemplates
{
    public abstract class LdapcpUserControl : UserControl
    {
        /// <summary>
        /// This is an attribute that must be set in the markup code, with the name of the claims provider
        /// </summary>
        public string ClaimsProviderName;

        /// <summary>
        /// This is an attribute that must be set in the markup code, with the name of the persisted object that holds the configuration
        /// </summary>
        public string PersistedObjectName;

        private Guid _PersistedObjectID;
        /// <summary>
        /// This is an attribute that must be set in the markup code, with the GUID of the persisted object that holds the configuration
        /// </summary>
        public string PersistedObjectID
        {
            get
            {
                return (this._PersistedObjectID == null || this._PersistedObjectID == Guid.Empty ) ? String.Empty : this._PersistedObjectID.ToString();
            }
            set
            {
                this._PersistedObjectID = new Guid(value);
            }
        }

        private ILDAPCPConfiguration _PersistedObject;
        protected LDAPCPConfig PersistedObject
        {
            get
            {
                SPSecurity.RunWithElevatedPrivileges(delegate ()
                {
                    if (_PersistedObject == null) _PersistedObject = LDAPCPConfig.GetConfiguration(PersistedObjectName);
                    if (_PersistedObject == null)
                    {
                        SPContext.Current.Web.AllowUnsafeUpdates = true;
                        _PersistedObject = LDAPCPConfig.CreatePersistedObject(this.PersistedObjectID, this.PersistedObjectName);
                        SPContext.Current.Web.AllowUnsafeUpdates = false;
                    }
                });
                return _PersistedObject as LDAPCPConfig;
            }
            //set { _PersistedObject = value; }
        }

        protected SPTrustedLoginProvider CurrentTrustedLoginProvider;
        protected ClaimTypeConfig IdentityClaim;
        protected ConfigStatus Status;

        protected long PersistedObjectVersion
        {
            get
            {
                if (ViewState[ViewStatePersistedObjectVersionKey] == null)
                    ViewState.Add(ViewStatePersistedObjectVersionKey, PersistedObject.Version);
                return (long)ViewState[ViewStatePersistedObjectVersionKey];
            }
            set { ViewState[ViewStatePersistedObjectVersionKey] = value; }
        }

        protected string MostImportantError
        {
            get
            {
                if (Status == ConfigStatus.AllGood) return String.Empty;

                if ((Status & ConfigStatus.NoSPTrustAssociation) == ConfigStatus.NoSPTrustAssociation)
                    return String.Format(TextErrorNoSPTrustAssociation, ClaimsProviderName);

                if ((Status & ConfigStatus.PersistedObjectNotFound) == ConfigStatus.PersistedObjectNotFound)
                    return TextErrorPersistedObjectNotFound;

                if ((Status & ConfigStatus.NoIdentityClaimType) == ConfigStatus.NoIdentityClaimType)
                    return String.Format(TextErrorNoIdentityClaimType, CurrentTrustedLoginProvider.DisplayName, CurrentTrustedLoginProvider.IdentityClaimTypeInformation.MappedClaimType);

                if ((Status & ConfigStatus.PersistedObjectStale) == ConfigStatus.PersistedObjectStale)
                    return TextErrorPersistedObjectStale;

                if ((Status & ConfigStatus.ClaimsProviderNamePropNotSet) == ConfigStatus.ClaimsProviderNamePropNotSet)
                    return TextErrorClaimsProviderNameNotSet;

                if ((Status & ConfigStatus.PersistedObjectNamePropNotSet) == ConfigStatus.PersistedObjectNamePropNotSet)
                    return TextErrorPersistedObjectNameNotSet;

                if ((Status & ConfigStatus.PersistedObjectIDPropNotSet) == ConfigStatus.PersistedObjectIDPropNotSet)
                    return TextErrorPersistedObjectIDNotSet;

                return String.Empty;
            }
        }

        protected static string ViewStatePersistedObjectVersionKey = "PersistedObjectVersion";
        protected static string TextErrorPersistedObjectNotFound = "PersistedObject cannot be found.";
        protected static string TextErrorPersistedObjectStale = "Modification is cancelled because persisted object was modified since last load of the page. Please refresh the page and try again.";
        protected static string TextErrorNoSPTrustAssociation = "{0} is currently not associated with any TrustedLoginProvider. It is mandatory because it cannot create permission for a trust if it is not associated to it.<br/>Visit <a href=\"http://ldapcp.com/\" target=\"_blank\">ldapcp.com/</a> to see how to associate it.<br/>Settings on this page will not be available as long as LDAPCP will not associated to a trut.";
        protected static string TextErrorNoIdentityClaimType = "The TrustedLoginProvider {0} is set with identity claim type \"{1}\" but it is not in the claims list of LDAPCP.<br/>Please visit LDAPCP page \"claims mapping\" in Security tab to set it and return to this page afterwards.";
        protected static string TextErrorClaimsProviderNameNotSet = "The attribute 'ClaimsProviderName' is required but is not set on the user control.";
        protected static string TextErrorPersistedObjectNameNotSet = "The attribute 'PersistedObjectName' is required but is not set on the user control.";
        protected static string TextErrorPersistedObjectIDNotSet = "The attribute 'PersistedObjectID' is required but is not set on the user control.";

        abstract protected bool UpdatePersistedObjectProperties(bool commitChanges);

        /// <summary>
        /// Ensures configuration is valid to proceed
        /// </summary>
        /// <returns></returns>
        public virtual ConfigStatus ValidatePrerequisite()
        {
            Status = ConfigStatus.AllGood;
            // DataBind() must be called to bind attributes set in UserControl if they are set as "<%# #>"
            DataBind();
            if (String.IsNullOrEmpty(ClaimsProviderName)) Status |= ConfigStatus.ClaimsProviderNamePropNotSet;
            if (String.IsNullOrEmpty(PersistedObjectName)) Status |= ConfigStatus.PersistedObjectNamePropNotSet;
            if (String.IsNullOrEmpty(PersistedObjectID)) Status |= ConfigStatus.PersistedObjectIDPropNotSet;
            if (Status != ConfigStatus.AllGood)
            {
                LdapcpLogging.Log($"[{ClaimsProviderName}] {MostImportantError}", TraceSeverity.Unexpected, EventSeverity.Error, LdapcpLogging.Categories.Configuration);
                // Should not go further if those requirements are not met
                return Status;
            }

            if (PersistedObject == null)
            {
                Status |= ConfigStatus.PersistedObjectNotFound;
            }
            if (CurrentTrustedLoginProvider == null)
            {
                CurrentTrustedLoginProvider = LDAPCP.GetSPTrustAssociatedWithCP(this.ClaimsProviderName);
                if (CurrentTrustedLoginProvider == null) Status |= ConfigStatus.NoSPTrustAssociation;
            }
            if (IdentityClaim == null && Status == ConfigStatus.AllGood)
            {
                //FINDTOFIRSTORDEFAULT
                IdentityClaim = this.IdentityClaim = PersistedObject.ClaimTypes.FirstOrDefault(x => String.Equals(CurrentTrustedLoginProvider.IdentityClaimTypeInformation.MappedClaimType, x.ClaimType, StringComparison.InvariantCultureIgnoreCase) && !x.CreateAsIdentityClaim);
                if (IdentityClaim == null) Status |= ConfigStatus.NoIdentityClaimType;
            }
            if (PersistedObjectVersion != PersistedObject.Version)
            {
                Status |= ConfigStatus.PersistedObjectStale;
            }

            if (Status != ConfigStatus.AllGood) LdapcpLogging.Log($"[{ClaimsProviderName}] {MostImportantError}", TraceSeverity.Unexpected, EventSeverity.Error, LdapcpLogging.Categories.Configuration);
            return Status;
        }

        public virtual void CommitChanges()
        {
            PersistedObject.Update();
            PersistedObjectVersion = PersistedObject.Version;
            LdapcpLogging.Log(
               $"[{ClaimsProviderName}] Updated PersistedObject {PersistedObjectName} to version {PersistedObject.Version}", 
               TraceSeverity.Medium,
               EventSeverity.Information,
               LdapcpLogging.Categories.Configuration);
        }
    }

    [Flags]
    public enum ConfigStatus
    {
        AllGood = 0x0,
        PersistedObjectNotFound = 0x1,
        NoSPTrustAssociation = 0x2,
        NoIdentityClaimType = 0x4,
        PersistedObjectStale = 0x8,
        ClaimsProviderNamePropNotSet = 0x10,
        PersistedObjectNamePropNotSet = 0x20,
        PersistedObjectIDPropNotSet = 0x40
    };
}
