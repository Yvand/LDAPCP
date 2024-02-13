using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
using Microsoft.SharePoint.Utilities;
using System;
using System.Security.Claims;
using System.Web.UI;
using Yvand.LdapClaimsProvider.Configuration;
using Yvand.LdapClaimsProvider.Logging;

namespace Yvand.LdapClaimsProvider.Administration
{
    public abstract class LDAPCPSEUserControl : UserControl
    {
        /// <summary>
        /// This member is used in the markup code and cannot be made as a property
        /// </summary>
        public string ClaimsProviderName;

        /// <summary>
        /// This member is used in the markup code and cannot be made as a property
        /// </summary>
        public string ConfigurationName;

        public Guid ConfigurationID { get; set; } = Guid.Empty;

        private LdapProviderConfiguration _Configuration;
        /// <summary>
        /// Gets the persisted object which contains the configuration for AzureDCP
        /// </summary>
        protected LdapProviderConfiguration Configuration
        {
            get
            {
                if (_Configuration == null)
                {
                    var configuration = LdapProviderConfiguration.GetGlobalConfiguration(this.ConfigurationID, true);
                    if (configuration != null)
                    {
                        _Configuration = configuration;
                        Settings = (LdapProviderSettings)configuration.Settings;
                    }
                }
                if (_Configuration == null)
                {
                    SPContext.Current.Web.AllowUnsafeUpdates = true;
                    var configuration = LdapProviderConfiguration.CreateGlobalConfiguration(this.ConfigurationID, this.ConfigurationName, this.ClaimsProviderName);
                    SPContext.Current.Web.AllowUnsafeUpdates = false;
                    if (configuration != null)
                    {
                        _Configuration = configuration;
                        Settings = (LdapProviderSettings)configuration.Settings;
                    }
                }
                return _Configuration;
            }
        }

        /// <summary>
        /// Gets or sets the settings used by EntraCP to run
        /// </summary>
        protected LdapProviderSettings Settings { get; set; }

        protected ConfigStatus Status;

        protected long ConfigurationVersion
        {
            get
            {
                if (ViewState[ViewStatePersistedObjectVersionKey] == null)
                    ViewState.Add(ViewStatePersistedObjectVersionKey, Configuration.Version);
                return (long)ViewState[ViewStatePersistedObjectVersionKey];
            }
            set { ViewState[ViewStatePersistedObjectVersionKey] = value; }
        }

        private SPTrustedLoginProvider _SPTrust;
        protected SPTrustedLoginProvider SPTrust
        {
            get
            {
                if (this._SPTrust == null)
                {
                    this._SPTrust = Utils.GetSPTrustAssociatedWithClaimsProvider(this.ClaimsProviderName);
                }
                return this._SPTrust;
            }
        }

        protected string UserIdentifierEncodedValuePrefix
        {
            get
            {
                SPClaimProviderManager claimMgr = SPClaimProviderManager.Local;
                ClaimTypeConfig idConfig = Settings.ClaimTypes.UserIdentifierConfig;
                if (idConfig == null)
                {
                    return String.Empty;
                }
                return claimMgr.EncodeClaim(new SPClaim(idConfig.ClaimType, String.Empty, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, SPTrust.Name)));
            }
        }

        protected string GroupIdentifierEncodedValuePrefix
        {
            get
            {
                SPClaimProviderManager claimMgr = SPClaimProviderManager.Local;
                ClaimTypeConfig idConfig = Settings.ClaimTypes.GroupIdentifierConfig;
                if (idConfig == null)
                {
                    return String.Empty;
                }
                return claimMgr.EncodeClaim(new SPClaim(idConfig.ClaimType, String.Empty, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, SPTrust.Name)));
            }
        }

        protected string MostImportantError
        {
            get
            {
                if (Status == ConfigStatus.AllGood)
                {
                    return String.Empty;
                }

                if ((Status & ConfigStatus.NoSPTrustAssociation) == ConfigStatus.NoSPTrustAssociation)
                {
                    return String.Format(TextErrorNoSPTrustAssociation, SPEncode.HtmlEncode(ClaimsProviderName));
                }

                if ((Status & ConfigStatus.PersistedObjectNotFound) == ConfigStatus.PersistedObjectNotFound)
                {
                    return TextErrorPersistedObjectNotFound;
                }

                if ((Status & ConfigStatus.ConfigurationInvalid) == ConfigStatus.ConfigurationInvalid)
                {
                    return TextErrorPersistedConfigInvalid;
                }

                if ((Status & ConfigStatus.NoIdentityClaimType) == ConfigStatus.NoIdentityClaimType)
                {
                    return String.Format(TextErrorNoIdentityClaimType, SPTrust.DisplayName, SPTrust.IdentityClaimTypeInformation.MappedClaimType);
                }

                if ((Status & ConfigStatus.PersistedObjectStale) == ConfigStatus.PersistedObjectStale)
                {
                    return TextErrorPersistedObjectStale;
                }

                if ((Status & ConfigStatus.ClaimsProviderNamePropNotSet) == ConfigStatus.ClaimsProviderNamePropNotSet)
                {
                    return TextErrorClaimsProviderNameNotSet;
                }

                if ((Status & ConfigStatus.PersistedObjectNamePropNotSet) == ConfigStatus.PersistedObjectNamePropNotSet)
                {
                    return TextErrorPersistedObjectNameNotSet;
                }

                if ((Status & ConfigStatus.PersistedObjectIDPropNotSet) == ConfigStatus.PersistedObjectIDPropNotSet)
                {
                    return TextErrorPersistedObjectIDNotSet;
                }

                return String.Empty;
            }
        }

        protected static readonly string ViewStatePersistedObjectVersionKey = "PersistedObjectVersion";
        protected static readonly string TextErrorPersistedObjectNotFound = "PersistedObject cannot be found.";
        protected static readonly string TextErrorPersistedObjectStale = "Modifications were not applied because the persisted object was modified after this page was loaded. Please refresh the page and try again.";
        protected static readonly string TextErrorNoSPTrustAssociation = "{0} is currently not associated with any SPTrustedLoginProvider, which is required to create entities.<br/>Visit <a href=\"" + ClaimsProviderConstants.PUBLICSITEURL + "\" target=\"_blank\">EntraCP site</a> for more information.<br/>Refresh this page once '{0}' is associated with a SPTrustedLoginProvider.";
        protected static readonly string TextErrorNoIdentityClaimType = "The SPTrustedLoginProvider '{0}' is set with identity claim type '{1}', but is not set in claim types configuration list.<br/>Please visit claim types configuration page to add it.";
        protected static readonly string TextErrorClaimsProviderNameNotSet = "The attribute 'ClaimsProviderName' must be set in the user control.";
        protected static readonly string TextErrorPersistedObjectNameNotSet = "The attribute 'PersistedObjectName' must be set in the user control.";
        protected static readonly string TextErrorPersistedObjectIDNotSet = "The attribute 'PersistedObjectID' must be set in the user control.";
        protected static readonly string TextErrorPersistedConfigInvalid = "PersistedObject was found but its configuration is not valid. Check the SharePoint logs to see the actual problem.";

        /// <summary>
        /// Ensures configuration is valid to proceed
        /// </summary>
        /// <returns></returns>
        public virtual ConfigStatus ValidatePrerequisite()
        {
            if (!this.IsPostBack)
            {
                // DataBind() must be called to bind attributes that are set as "<%# #>"in .aspx
                // But only during initial page load, otherwise it would reset bindings in other controls like SPGridView
                DataBind();
                ViewState.Add("ClaimsProviderName", ClaimsProviderName);
                ViewState.Add("PersistedObjectName", ConfigurationName);
                ViewState.Add("PersistedObjectID", ConfigurationID);
            }
            else
            {
                ClaimsProviderName = ViewState["ClaimsProviderName"].ToString();
                ConfigurationName = ViewState["PersistedObjectName"].ToString();
                ConfigurationID = new Guid(ViewState["PersistedObjectID"].ToString());
            }

            Status = ConfigStatus.AllGood;
            if (String.IsNullOrEmpty(ClaimsProviderName)) { Status |= ConfigStatus.ClaimsProviderNamePropNotSet; }
            if (String.IsNullOrEmpty(ConfigurationName)) { Status |= ConfigStatus.PersistedObjectNamePropNotSet; }
            if (ConfigurationID == Guid.Empty) { Status |= ConfigStatus.PersistedObjectIDPropNotSet; }
            if (Status != ConfigStatus.AllGood)
            {
                Logger.Log($"[{ClaimsProviderName}] {MostImportantError}", TraceSeverity.Unexpected, EventSeverity.Error, TraceCategory.Configuration);
                return Status;
            }

            if (SPTrust == null)
            {
                Status |= ConfigStatus.NoSPTrustAssociation;
                return Status;
            }

            if (Configuration == null)
            {
                Status |= ConfigStatus.PersistedObjectNotFound;
                return Status;
            }

            if (Status != ConfigStatus.AllGood)
            {
                Logger.Log($"[{ClaimsProviderName}] {MostImportantError}", TraceSeverity.Unexpected, EventSeverity.Error, TraceCategory.Configuration);
                return Status;
            }

            if (Settings == null)
            {
                Status |= ConfigStatus.ConfigurationInvalid;
                return Status;
            }

            // This should never happen if the object is coming from config database
            // But it may happen if object is in a mess in current process, amd SPS does not refresh it from config database because its version did not change
            if (Configuration.ClaimTypes == null || Configuration.ClaimTypes.Count == 0)
            {
                Status |= ConfigStatus.ConfigurationInvalid;
                return Status;
            }
            if (Configuration.ClaimTypes.UserIdentifierConfig == null)
            {
                Status |= ConfigStatus.NoIdentityClaimType;
            }
            if (ConfigurationVersion != Configuration.Version)
            {
                Status |= ConfigStatus.PersistedObjectStale;
            }

            if (Status != ConfigStatus.AllGood)
            {
                Logger.Log($"[{ClaimsProviderName}] {MostImportantError}", TraceSeverity.Unexpected, EventSeverity.Error, TraceCategory.Configuration);
            }
            return Status;
        }

        public virtual void CommitChanges()
        {
            Configuration.ApplySettings(Settings, true);
            ConfigurationVersion = Configuration.Version;
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
        PersistedObjectIDPropNotSet = 0x40,
        ConfigurationInvalid = 0x80,
    };
}
