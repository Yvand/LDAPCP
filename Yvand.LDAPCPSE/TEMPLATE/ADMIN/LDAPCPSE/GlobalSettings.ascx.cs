using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.WebControls;
using System;
using System.Collections.Generic;
using System.Data;
using System.DirectoryServices;
using System.Linq;
using System.Web.UI.WebControls;
using Yvand.LdapClaimsProvider.Configuration;
using Yvand.LdapClaimsProvider.Logging;
using AuthenticationTypes = System.DirectoryServices.AuthenticationTypes;
using DirectoryConnection = Yvand.LdapClaimsProvider.Configuration.DirectoryConnection;

namespace Yvand.LdapClaimsProvider.Administration
{
    public partial class GlobalSettingsUserControl : LDAPCPSEUserControl
    {
        public new string UserIdentifierEncodedValuePrefix = String.Empty; // This must be a member to be accessible from marup code, it cannot be a property
        public new string GroupIdentifierEncodedValuePrefix = String.Empty; // This must be a member to be accessible from marup code, it cannot be a property

        readonly string NoValueSelected = "None";
        readonly string TextConnectionSuccessful = "Connection successful.";
        readonly string TextSummaryPersistedObjectInformation = "Found configuration '{0}' v{1} (Persisted Object ID: '{2}')";
        readonly string TextSharePointDomain = "Connect to SharePoint domain";
        readonly string TextErrorLDAPFieldsMissing = "Some mandatory fields are missing.";
        readonly string TextErrorTestLdapConnection = "Unable to connect to LDAP for following reason:<br/>{0}<br/>It may be expected if w3wp process of central admin has intentionally no access to LDAP server.";

        protected void Page_Load(object sender, EventArgs e)
        {
            Initialize();
        }

        /// <summary>
        /// Initialize controls as needed if prerequisites are ok, otherwise deactivate controls and show error message
        /// </summary>
        protected void Initialize()
        {
            // Set default values of IsDefaultADConnectionCreated and ForceCheckCustomLdapConnection
            // They are overridden later if needed
            ViewState["IsDefaultADConnectionCreated"] = false;
            ViewState["ForceCheckCustomLdapConnection"] = false;

            if (ValidatePrerequisite() != ConfigStatus.AllGood)
            {
                this.LabelErrorMessage.Text = base.MostImportantError;
                this.BtnOK.Enabled = false;
                this.BtnOKTop.Enabled = false;
                return;
            }

            LabelMessage.Text = String.Format(TextSummaryPersistedObjectInformation, Configuration.Name, Configuration.Version, Configuration.Id);
            UserIdentifierEncodedValuePrefix = base.UserIdentifierEncodedValuePrefix;
            GroupIdentifierEncodedValuePrefix = base.GroupIdentifierEncodedValuePrefix;
            if (!this.IsPostBack)
            {
                PopulateConnectionsGrid();
                PopulateCblAuthenticationTypes();
                PopulateFields();
                InitializeAugmentation();
            }

            // Handle password storage in ViewState
            if (ViewState["LDAPpwd"] != null)
            {
                ViewState["LDAPpwd"] = TxtLdapPassword.Text;
                TxtLdapPassword.Attributes.Add("value", ViewState["LDAPpwd"].ToString());
            }
            else
            {
                ViewState["LDAPpwd"] = TxtLdapPassword.Text;
            }
        }

        private void InitializeAugmentation()
        {
            ChkEnableAugmentation.Checked = Settings.EnableAugmentation;

            // Initialize grid for LDAP connections
            var spDomainCoco = Settings.LdapConnections.FirstOrDefault(x => x.UseDefaultADConnection);
            if (spDomainCoco != null)
            {
                spDomainCoco.LdapPath = TextSharePointDomain;
            }

            GridLdapConnections.DataSource = Settings.LdapConnections;
            GridLdapConnections.DataKeyNames = new string[] { "Identifier" };
            GridLdapConnections.DataBind();
        }

        void PopulateConnectionsGrid()
        {
            if (Settings.LdapConnections != null)
            {
                PropertyCollectionBinder pcb = new PropertyCollectionBinder();
                foreach (DirectoryConnection ldapConnection in Settings.LdapConnections)
                {
                    if (ldapConnection.UseDefaultADConnection)
                    {
                        ViewState["IsDefaultADConnectionCreated"] = true;

                        pcb.AddRow(ldapConnection.Identifier, TextSharePointDomain, "Process account");
                    }
                    else
                    {
                        pcb.AddRow(ldapConnection.Identifier, ldapConnection.LdapPath, ldapConnection.Username);
                    }
                }
                pcb.BindGrid(grdLDAPConnections);
            }
        }

        void PopulateCblAuthenticationTypes()
        {
            Dictionary<int, string> authenticationTypesDS = ParseEnumTypeAuthenticationTypes();
            foreach (KeyValuePair<int, string> authNType in authenticationTypesDS)
            {
                ListItem authNTypeItem = new ListItem();
                authNTypeItem.Text = authNType.Value;
                authNTypeItem.Value = authNType.Key.ToString();
                CblAuthenticationTypes.Items.Add(authNTypeItem);
            }
        }

        protected static Dictionary<int, string> ParseEnumTypeAuthenticationTypes()
        {
            Type enumType = typeof(AuthenticationTypes);
            Dictionary<int, string> list = new Dictionary<int, string>();
            foreach (var value in Enum.GetValues(enumType))
            {
                string valueName = Enum.GetName(enumType, (int)value);
                // Encryption and SecureSocketsLayer have same value and adding both to Dictionary would violate uniqueness of the key
                if (String.Equals(valueName, "Encryption", StringComparison.InvariantCultureIgnoreCase) && list.ContainsValue("Encryption"))
                {
                    continue;
                }
                list.Add((int)value, valueName);
            }
            return list;
        }

        private void PopulateFields()
        {
            // User identifier settings
            this.lblUserIdClaimType.Text = Settings.ClaimTypes.UserIdentifierConfig.ClaimType;
            this.TxtUserIdLdapClass.Text = Settings.ClaimTypes.UserIdentifierConfig.DirectoryObjectClass;
            this.TxtUserIdLdapAttribute.Text = Settings.ClaimTypes.UserIdentifierConfig.DirectoryObjectAttribute;
            this.TxtUserIdDisplayTextAttribute.Text = Settings.ClaimTypes.UserIdentifierConfig.DirectoryObjectAttributeForDisplayText;
            this.TxtUserIdAdditionalLdapAttributes.Text = String.Join(",", Settings.ClaimTypes.GetSearchAttributesForEntity(DirectoryObjectType.User));
            this.TxtUserIdLeadingToken.Text = Settings.ClaimTypes.UserIdentifierConfig.ClaimValueLeadingToken;
            this.TxtUserIdAdditionalLdapFilter.Text = Settings.ClaimTypes.UserIdentifierConfig.DirectoryObjectAdditionalFilter;

            // Group identifier settings
            this.DdlGroupClaimType.Items.Add(NoValueSelected);
            this.DdlGroupClaimType.Items[0].Selected = true;
            var possibleGroupClaimTypes = Utils.GetNonWellKnownUserClaimTypesFromTrust(base.ClaimsProviderName);
            foreach (string possibleGroupClaimType in possibleGroupClaimTypes)
            {
                ListItem possibleGroupClaimTypeItem = new ListItem(possibleGroupClaimType);
                this.DdlGroupClaimType.Items.Add(possibleGroupClaimTypeItem);
            }

            ClaimTypeConfig groupCtc = Settings.ClaimTypes.GetIdentifierConfiguration(DirectoryObjectType.Group);
            if (groupCtc != null)
            {
                this.DdlGroupClaimType.SelectedValue = groupCtc.ClaimType;
                this.TxtGroupLdapClass.Text = groupCtc.DirectoryObjectClass;
                this.TxtGroupLdapAttribute.Text = groupCtc.DirectoryObjectAttribute;
                this.TxtGroupDisplayTextAttribute.Text = groupCtc.DirectoryObjectAttributeForDisplayText;
                this.TxtGroupAdditionalLdapAttributes.Text = String.Join(",", Settings.ClaimTypes.GetSearchAttributesForEntity(DirectoryObjectType.Group));
                this.TxtGroupLeadingToken.Text = groupCtc.ClaimValueLeadingToken;
                this.TxtGroupAdditionalLdapFilter.Text = groupCtc.DirectoryObjectAdditionalFilter;
            }

            // Other settings
            this.ChkAlwaysResolveUserInput.Checked = Settings.AlwaysResolveUserInput;
            this.ChkFilterEnabledUsersOnly.Checked = Settings.FilterEnabledUsersOnly;
            this.ChkFilterSecurityGroupsOnly.Checked = Settings.FilterSecurityGroupsOnly;
            this.ChkFilterExactMatchOnly.Checked = Settings.FilterExactMatchOnly;
            this.txtTimeout.Text = Settings.Timeout.ToString();
        }

        /// <summary>
        /// Parse checkbox list CblAuthenticationTypes to find authentication modes selected
        /// </summary>
        /// <returns></returns>
        AuthenticationTypes GetSelectedAuthenticationTypes(bool ClearSelection)
        {
            AuthenticationTypes authNTypes = 0;
            foreach (ListItem item in this.CblAuthenticationTypes.Items)
            {
                if (!item.Selected) { continue; }
                int selectedType;
                if (!Int32.TryParse(item.Value, out selectedType)) { continue; }
                authNTypes += selectedType;
                if (ClearSelection)
                {
                    item.Selected = false;
                }
            }
            return authNTypes;
        }

        protected void grdLDAPConnections_RowDeleting(object sender, GridViewDeleteEventArgs e)
        {
            if (ValidatePrerequisite() != ConfigStatus.AllGood) { return; }
            if (Settings.LdapConnections == null) { return; }

            GridViewRow rowToDelete = grdLDAPConnections.Rows[e.RowIndex];
            Guid Id = new Guid(rowToDelete.Cells[0].Text);
            DirectoryConnection tenantToRemove = Settings.LdapConnections.FirstOrDefault(x => x.Identifier == Id);
            if (tenantToRemove != null)
            {
                Settings.LdapConnections.Remove(tenantToRemove);
                CommitChanges();
                Logger.Log($"Directory '{tenantToRemove.LdapPath}' was successfully removed from configuration '{ConfigurationName}'", TraceSeverity.Medium, TraceCategory.Configuration);
                LabelMessage.Text = String.Format(TextSummaryPersistedObjectInformation, Configuration.Name, Configuration.Version, Configuration.Id);
                PopulateConnectionsGrid();
                InitializeAugmentation();
            }
        }

        protected bool UpdateConfiguration(bool commitChanges)
        {
            if (ValidatePrerequisite() != ConfigStatus.AllGood) { return false; }

            // User identifier settings            
            Settings.ClaimTypes.UserIdentifierConfig.DirectoryObjectClass = this.TxtUserIdLdapClass.Text;
            Settings.ClaimTypes.UserIdentifierConfig.DirectoryObjectAttribute = this.TxtUserIdLdapAttribute.Text;
            Settings.ClaimTypes.UserIdentifierConfig.DirectoryObjectAttributeForDisplayText = this.TxtUserIdDisplayTextAttribute.Text;
            Settings.ClaimTypes.UserIdentifierConfig.ClaimValueLeadingToken = this.TxtUserIdLeadingToken.Text;
            Settings.ClaimTypes.SetSearchAttributesForEntity(this.TxtUserIdAdditionalLdapAttributes.Text, Settings.ClaimTypes.UserIdentifierConfig.DirectoryObjectClass, DirectoryObjectType.User);
            Settings.ClaimTypes.SetAdditionalLdapFilterForEntity(this.TxtUserIdAdditionalLdapFilter.Text, DirectoryObjectType.User);

            // Group identifier settings
            if (!String.Equals(this.DdlGroupClaimType.SelectedValue, NoValueSelected, StringComparison.OrdinalIgnoreCase))
            {
                ClaimTypeConfig groupIdConfig = Settings.ClaimTypes.GroupIdentifierConfig;
                bool newGroupConfigObject = false;
                if (groupIdConfig == null)
                {
                    groupIdConfig = new ClaimTypeConfig { DirectoryObjectType = DirectoryObjectType.Group };
                    newGroupConfigObject = true;
                }
                groupIdConfig.ClaimType = this.DdlGroupClaimType.SelectedValue;
                groupIdConfig.DirectoryObjectClass = this.TxtGroupLdapClass.Text;
                groupIdConfig.DirectoryObjectAttribute = this.TxtGroupLdapAttribute.Text;
                groupIdConfig.DirectoryObjectAttributeForDisplayText = this.TxtGroupDisplayTextAttribute.Text;
                groupIdConfig.ClaimValueLeadingToken = this.TxtGroupLeadingToken.Text;
                Settings.ClaimTypes.SetSearchAttributesForEntity(this.TxtGroupAdditionalLdapAttributes.Text, groupIdConfig.DirectoryObjectClass, DirectoryObjectType.Group);
                Settings.ClaimTypes.SetAdditionalLdapFilterForEntity(this.TxtGroupAdditionalLdapFilter.Text, DirectoryObjectType.Group);
                if (newGroupConfigObject)
                {
                    Settings.ClaimTypes.Add(groupIdConfig);
                }
            }
            else
            {
                ClaimTypeConfig groupIdConfig = Settings.ClaimTypes.GroupIdentifierConfig;
                if (groupIdConfig != null)
                {
                    Settings.ClaimTypes.Remove(groupIdConfig);
                }
            }

            // Augmentation settings
            foreach (GridViewRow item in GridLdapConnections.Rows)
            {
                CheckBox chkAugEn = (CheckBox)item.FindControl("ChkAugmentationEnableOnCoco");
                CheckBox chkIsADDomain = (CheckBox)item.FindControl("ChkGetGroupMembershipAsADDomain");
                TextBox txtId = (TextBox)item.FindControl("IdPropHidden");

                DirectoryConnection ldapConnection = Settings.LdapConnections.First(x => x.Identifier == new Guid(txtId.Text));
                ldapConnection.EnableAugmentation = chkAugEn.Checked;
                ldapConnection.GetGroupMembershipUsingDotNetHelpers = chkIsADDomain.Checked;
            }

            Settings.AlwaysResolveUserInput = this.ChkAlwaysResolveUserInput.Checked;
            Settings.FilterEnabledUsersOnly = this.ChkFilterEnabledUsersOnly.Checked;
            Settings.FilterSecurityGroupsOnly = this.ChkFilterSecurityGroupsOnly.Checked;
            Settings.FilterExactMatchOnly = this.ChkFilterExactMatchOnly.Checked;
            Settings.EnableAugmentation = this.ChkEnableAugmentation.Checked;

            int timeOut;
            if (!Int32.TryParse(this.txtTimeout.Text, out timeOut) || timeOut < 0)
            {
                timeOut = ClaimsProviderConstants.DEFAULT_TIMEOUT; //set to default if unable to parse
            }
            Settings.Timeout = timeOut;

            foreach (var userAttr in this.Settings.ClaimTypes.Where(x => x.DirectoryObjectType == DirectoryObjectType.User))
            {
                userAttr.DirectoryObjectAdditionalFilter = this.TxtUserIdAdditionalLdapFilter.Text;
            }

            if (commitChanges) { CommitChanges(); }
            return true;
        }

        protected void BtnTestLdapConnection_Click(Object sender, EventArgs e)
        {
            this.ValidateLdapConnection();
        }

        protected void ValidateLdapConnection()
        {
            ViewState["ForceCheckCustomLdapConnection"] = true;
            if (this.TxtLdapConnectionString.Text == String.Empty || this.TxtLdapPassword.Text == String.Empty || this.TxtLdapUsername.Text == String.Empty)
            {
                this.LabelErrorTestLdapConnection.Text = TextErrorLDAPFieldsMissing;
                return;
            }

            DirectoryEntry de = null;
            DirectorySearcher deSearch = new DirectorySearcher();
            try
            {
                AuthenticationTypes authNTypes = GetSelectedAuthenticationTypes(false);
                de = new DirectoryEntry(this.TxtLdapConnectionString.Text, this.TxtLdapUsername.Text, this.TxtLdapPassword.Text, authNTypes);
                deSearch.SearchRoot = de;
                deSearch.FindOne();
                this.LabelTestLdapConnectionOK.Text = TextConnectionSuccessful;
            }
            catch (Exception ex)
            {
                Logger.LogException(ClaimsProviderName, "while testing LDAP connection", TraceCategory.Configuration, ex);
                this.LabelErrorTestLdapConnection.Text = String.Format(TextErrorTestLdapConnection, ex.Message);
            }
            finally
            {
                if (deSearch != null) { deSearch.Dispose(); }
                if (de != null) { de.Dispose(); }
            }

            // Required to set radio buttons of LDAP connections correctly in UI
            PopulateConnectionsGrid();
        }

        protected void BtnOK_Click(Object sender, EventArgs e)
        {
            if (ValidatePrerequisite() != ConfigStatus.AllGood) { return; }
            if (UpdateConfiguration(true))
            {
                Response.Redirect("/Security.aspx", false);
            }
            else
            {
                LabelErrorMessage.Text = base.MostImportantError;
            }
        }

        protected void BtnResetConfig_Click(Object sender, EventArgs e)
        {
            LdapProviderConfiguration.DeleteGlobalConfiguration(ConfigurationID);
            Response.Redirect(Request.RawUrl, false);
        }

        protected void BtnAddLdapConnection_Click(object sender, EventArgs e)
        {
            AddTenantConnection();
        }

        /// <summary>
        /// Add new Directory in persisted object
        /// </summary>
        void AddTenantConnection()
        {
            if (ValidatePrerequisite() != ConfigStatus.AllGood) { return; }

            if (this.RbUseCustomConnection.Checked && (this.TxtLdapConnectionString.Text == String.Empty || this.TxtLdapUsername.Text == String.Empty || this.TxtLdapPassword.Text == String.Empty))
            {
                this.LabelErrorTestLdapConnection.Text = TextErrorLDAPFieldsMissing;
                return;
            }

            if (this.RbUseServerDomain.Checked)
            {
                Settings.LdapConnections.Add(new DirectoryConnection(true));
            }
            else
            {
                AuthenticationTypes authNType = GetSelectedAuthenticationTypes(true);
                Settings.LdapConnections.Add(
                    new DirectoryConnection(false)
                    {
                        LdapPath = this.TxtLdapConnectionString.Text,
                        Username = this.TxtLdapUsername.Text,
                        Password = this.TxtLdapPassword.Text,
                        AuthenticationType = authNType,
                        EnableAugmentation = true,
                    }
                );
            }

            CommitChanges();
            Logger.Log($"LDAP server '{this.TxtLdapConnectionString.Text}' was successfully added to configuration '{ConfigurationName}'", TraceSeverity.Medium, TraceCategory.Configuration);
            PopulateConnectionsGrid();
            InitializeAugmentation();
            ViewState["LDAPpwd"] = String.Empty;
            this.TxtLdapPassword.Attributes.Remove("value");
            this.TxtLdapConnectionString.Text = "LDAP://";
            this.TxtLdapUsername.Text = String.Empty;
            this.TxtLdapPassword.Text = String.Empty;
        }

        protected void grdLDAPConnections_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            // Ask user for confirmation when cliking on button Delete - https://stackoverflow.com/questions/9026884/asp-net-gridview-delete-row-only-on-confirmation
            if (e.Row.RowType == DataControlRowType.DataRow)
            {
                Button deleteButton = (Button)e.Row.Cells[3].Controls[0];
                if (deleteButton != null && String.Equals(deleteButton.Text, "Delete", StringComparison.OrdinalIgnoreCase))
                {
                    deleteButton.OnClientClick = "if(!confirm('Are you sure you want to delete this directory?')) return;";
                }
            }
        }
    }

    public class PropertyCollectionBinder
    {
        protected DataTable PropertyCollection = new DataTable();
        public PropertyCollectionBinder()
        {
            PropertyCollection.Columns.Add("Id", typeof(Guid));
            PropertyCollection.Columns.Add("Path", typeof(string));
            PropertyCollection.Columns.Add("Username", typeof(string));
        }

        public void AddRow(Guid Id, string Path, string Username)
        {
            DataRow newRow = PropertyCollection.Rows.Add();
            newRow["Id"] = Id;
            newRow["Path"] = Path;
            newRow["Username"] = Username;
        }

        public void BindGrid(SPGridView grid)
        {
            grid.DataSource = PropertyCollection.DefaultView;
            grid.DataBind();
        }
    }
}
