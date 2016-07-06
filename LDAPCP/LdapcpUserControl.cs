using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
using Microsoft.SharePoint.WebControls;
using System;
using System.Web.UI;

namespace ldapcp.ControlTemplates
{
    public abstract class LdapcpUserControl : UserControl
    {
        private ILDAPCPConfiguration _PersistedObject;
        public virtual LDAPCPConfig PersistedObject
        {
            get
            {
                SPSecurity.RunWithElevatedPrivileges(delegate ()
                {
                    if (_PersistedObject == null) _PersistedObject = LDAPCPConfig.GetFromConfigDB();
                    if (_PersistedObject == null)
                    {
                        SPContext.Current.Web.AllowUnsafeUpdates = true;
                        _PersistedObject = LDAPCPConfig.CreatePersistedObject();
                        SPContext.Current.Web.AllowUnsafeUpdates = false;
                    }
                });
                return _PersistedObject as LDAPCPConfig;
            }
            //set { _PersistedObject = value; }
        }

        protected SPTrustedLoginProvider CurrentTrustedLoginProvider;
        protected AttributeHelper IdentityClaim;
        public ConfigStatus Status;

        public long PersistedObjectVersion
        {
            get
            {
                if (ViewState[ViewStatePersistedObjectVersionKey] == null)
                    ViewState.Add(ViewStatePersistedObjectVersionKey, PersistedObject.Version);
                return (long)ViewState[ViewStatePersistedObjectVersionKey];
            }
            set { ViewState[ViewStatePersistedObjectVersionKey] = value; }
        }

        public string MostImportantError
        {
            get
            {
                if (Status == ConfigStatus.AllGood) return String.Empty;

                if ((Status & ConfigStatus.NoSPTrustAssociation) == ConfigStatus.NoSPTrustAssociation)
                    return TextErrorNoSPTrustAssociation;

                if ((Status & ConfigStatus.PersistedObjectNotFound) == ConfigStatus.PersistedObjectNotFound)
                    return TextErrorPersistedObjectNotFound;

                if ((Status & ConfigStatus.NoIdentityClaimType) == ConfigStatus.NoIdentityClaimType)
                    return String.Format(TextErrorNoIdentityClaimType, CurrentTrustedLoginProvider.DisplayName, CurrentTrustedLoginProvider.IdentityClaimTypeInformation.MappedClaimType);

                if ((Status & ConfigStatus.PersistedObjectStale) == ConfigStatus.PersistedObjectStale)
                    return TextErrorPersistedObjectStale;

                return String.Empty;
            }
        }

        public static string ViewStatePersistedObjectVersionKey = "PersistedObjectVersion";
        public static string TextErrorPersistedObjectNotFound = "PersistedObject cannot be found.";
        public static string TextErrorPersistedObjectStale = "Modification is cancelled because persisted object was modified since last load of the page. Please refresh the page and try again.";
        public static string TextErrorNoSPTrustAssociation = "LDAPCP is currently not associated with any TrustedLoginProvider. It is mandatory because it cannot create permission for a trust if it is not associated to it.<br/>Visit <a href=\"http://ldapcp.codeplex.com/\" target=\"_blank\">http://ldapcp.codeplex.com/</a> to see how to associate it.<br/>Settings on this page will not be available as long as LDAPCP will not associated to a trut.";
        public static string TextErrorNoIdentityClaimType = "The TrustedLoginProvider {0} is set with identity claim type \"{1}\" but it is not in the claims list of LDAPCP.<br/>Please visit LDAPCP page \"claims mapping\" in Security tab to set it and return to this page afterwards.";

        abstract public bool UpdatePersistedObjectProperties(bool commitChanges);

        /// <summary>
        /// Ensures configuration is valid to proceed
        /// </summary>
        /// <returns></returns>
        public virtual ConfigStatus ValidatePrerequisite()
        {
            Status = ConfigStatus.AllGood;
            if (PersistedObject == null)
            {
                Status |= ConfigStatus.PersistedObjectNotFound;
            }
            if (CurrentTrustedLoginProvider == null)
            {
                CurrentTrustedLoginProvider = LDAPCP.GetSPTrustAssociatedWithCP(LDAPCP._ProviderInternalName);
                if (CurrentTrustedLoginProvider == null) Status |= ConfigStatus.NoSPTrustAssociation;
            }
            if (IdentityClaim == null && Status == ConfigStatus.AllGood)
            {
                IdentityClaim = this.IdentityClaim = PersistedObject.AttributesListProp.Find(x => String.Equals(CurrentTrustedLoginProvider.IdentityClaimTypeInformation.MappedClaimType, x.ClaimType, StringComparison.InvariantCultureIgnoreCase) && !x.CreateAsIdentityClaim);
                if (IdentityClaim == null) Status |= ConfigStatus.NoIdentityClaimType;
            }
            if (PersistedObjectVersion != PersistedObject.Version)
            {
                Status |= ConfigStatus.PersistedObjectStale;
            }

            if (Status != ConfigStatus.AllGood) LdapcpLogging.Log(String.Format(MostImportantError), TraceSeverity.High, EventSeverity.Information, LdapcpLogging.Categories.Configuration);
            return Status;
        }

        public virtual void CommitChanges()
        {
            PersistedObject.Update();
            PersistedObjectVersion = PersistedObject.Version;
            LdapcpLogging.Log(
               String.Format("Updated PersistedObject {0} to version {1}", PersistedObject.DisplayName, PersistedObject.Version),
               TraceSeverity.Medium,
               EventSeverity.Information,
               LdapcpLogging.Categories.Configuration);
        }
    }

    [Flags]
    public enum ConfigStatus { AllGood = 0x0, PersistedObjectNotFound = 0x1, NoSPTrustAssociation = 0x2, NoIdentityClaimType = 0x4, PersistedObjectStale = 0x8 };
}
