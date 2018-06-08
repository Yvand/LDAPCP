using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
using Microsoft.SharePoint.WebControls;
using System;
using System.Collections.Generic;
using System.Data;
using System.DirectoryServices;
using System.Linq;
using System.Web.UI.WebControls;
using static ldapcp.ClaimsProviderLogging;

namespace ldapcp.ControlTemplates
{
    public partial class GlobalSettings : LdapcpUserControl
    {
        protected bool ShowValidateSection
        {
            get { return ValidateSection.Visible; }
            set { ValidateSection.Visible = ValidateTopSection.Visible = value; }
        }

        protected bool ShowCurrentLdapConnectionSection
        {
            get { return CurrentLdapConnectionSection.Visible; }
            set { CurrentLdapConnectionSection.Visible = value; }
        }

        protected bool ShowNewLdapConnectionSection
        {
            get { return NewLdapConnectionSection.Visible; }
            set { NewLdapConnectionSection.Visible = value; }
        }

        protected bool ShowAugmentationSection
        {
            get { return AugmentationSection.Visible; }
            set { AugmentationSection.Visible = value; }
        }

        string TextErrorNoGroupClaimType = "There is no claim type associated with an entity type 'FormsRole' or 'SecurityGroup'.";
        string TextErrorLDAPFieldsMissing = "Some mandatory fields are missing.";
        string TextErrorTestLdapConnection = "Unable to connect to LDAP for following reason:<br/>{0}<br/>It may be expected if w3wp process of central admin has intentionally no access to LDAP server.";
        string TextConnectionSuccessful = "Connection successful.";
        string TextSharePointDomain = "Connect to SharePoint domain";
        string TextUpdateAdditionalLdapFilterOk = "LDAP filter was successfully applied to all LDAP attributes of class 'user'.";

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

            // Check prerequisite
            if (ValidatePrerequisite() != ConfigStatus.AllGood)
            {
                this.LabelErrorMessage.Text = base.MostImportantError;
                this.BtnOK.Enabled = this.BtnOKTop.Enabled = false;
                return;
            }

            PopulateLdapConnectionGrid();
            if (!this.IsPostBack)
            {
                PopulateCblAuthenticationTypes();
                InitializeAugmentation();
                InitializeGeneralSettings();
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
            IEnumerable<ClaimTypeConfig> potentialGroupClaimTypes = PersistedObject.ClaimTypes.Where(x => x.EntityType == DirectoryObjectType.Group && !x.UseMainClaimTypeOfDirectoryObject);
            if (potentialGroupClaimTypes == null || potentialGroupClaimTypes.Count() == 0)
            {
                LabelErrorMessage.Text = TextErrorNoGroupClaimType;
                return;
            }

            foreach (var potentialGroup in potentialGroupClaimTypes)
            {
                DdlClaimTypes.Items.Add(potentialGroup.ClaimType);
            }

            ChkEnableAugmentation.Checked = PersistedObject.EnableAugmentation;

            if (!String.IsNullOrEmpty(PersistedObject.MainGroupClaimType) && DdlClaimTypes.Items.FindByValue(PersistedObject.MainGroupClaimType) != null)
                DdlClaimTypes.SelectedValue = PersistedObject.MainGroupClaimType;

            // Initialize grid for LDAP connections
            var spDomainCoco = PersistedObject.LDAPConnectionsProp.FirstOrDefault(x => x.UserServerDirectoryEntry);
            if (spDomainCoco != null) spDomainCoco.Path = TextSharePointDomain;

            GridLdapConnections.DataSource = PersistedObject.LDAPConnectionsProp;
            GridLdapConnections.DataKeyNames = new string[] { "IdProp" };
            GridLdapConnections.DataBind();
        }

        void PopulateLdapConnectionGrid()
        {
            if (PersistedObject.LDAPConnectionsProp != null)
            {
                PropertyCollectionBinder pcb = new PropertyCollectionBinder();
                foreach (LDAPConnection coco in PersistedObject.LDAPConnectionsProp)
                {
                    if (coco.UserServerDirectoryEntry)
                    {
                        ViewState["IsDefaultADConnectionCreated"] = true;

                        pcb.AddRow(coco.Id, TextSharePointDomain, "Process account");
                    }
                    else
                    {
                        pcb.AddRow(coco.Id, coco.Path, coco.Username);
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
                if (String.Equals(valueName, "Encryption", StringComparison.InvariantCultureIgnoreCase) && list.ContainsValue("Encryption")) continue;
                list.Add((int)value, valueName);
            }
            return list;
        }

        private void InitializeGeneralSettings()
        {
            this.ChkIdentityShowAdditionalAttribute.Checked = PersistedObject.DisplayLdapMatchForIdentityClaimTypeProp;
            if (String.IsNullOrEmpty(IdentityClaim.LDAPAttributeToShowAsDisplayText))
            {
                this.RbIdentityDefault.Checked = true;
            }
            else
            {
                this.RbIdentityCustomLDAP.Checked = true;
                this.TxtLdapAttributeToDisplay.Text = IdentityClaim.LDAPAttributeToShowAsDisplayText;
            }

            this.ChkAlwaysResolveUserInput.Checked = PersistedObject.BypassLDAPLookup;
            this.ChkFilterEnabledUsersOnly.Checked = PersistedObject.FilterEnabledUsersOnlyProp;
            this.ChkFilterSecurityGroupsOnly.Checked = PersistedObject.FilterSecurityGroupsOnlyProp;
            this.ChkFilterExactMatchOnly.Checked = PersistedObject.FilterExactMatchOnlyProp;
            this.txtTimeout.Text = PersistedObject.LDAPQueryTimeout.ToString();
            // Deprecated options that are not shown anymore in LDAPCP configuration page
            //this.ChkAddWildcardInFront.Checked = PersistedObject.AddWildcardInFrontOfQueryProp;
            //this.TxtPickerEntityGroupName.Text = PersistedObject.PickerEntityGroupNameProp;
        }

        protected bool UpdateConfiguration(bool commitChanges)
        {
            if (ValidatePrerequisite() != ConfigStatus.AllGood) return false;
            UpdateGeneralSettings();
            UpdateAugmentationSettings();
            if (commitChanges) CommitChanges();
            return true;
        }

        private void UpdateGeneralSettings()
        {
            // Handle identity claim type
            PersistedObject.DisplayLdapMatchForIdentityClaimTypeProp = this.ChkIdentityShowAdditionalAttribute.Checked;
            if (this.RbIdentityCustomLDAP.Checked)
            {
                IdentityClaim.LDAPAttributeToShowAsDisplayText = this.TxtLdapAttributeToDisplay.Text;
            }
            else
            {
                IdentityClaim.LDAPAttributeToShowAsDisplayText = String.Empty;
            }

            PersistedObject.BypassLDAPLookup = this.ChkAlwaysResolveUserInput.Checked;
            PersistedObject.FilterEnabledUsersOnlyProp = this.ChkFilterEnabledUsersOnly.Checked;
            PersistedObject.FilterSecurityGroupsOnlyProp = this.ChkFilterSecurityGroupsOnly.Checked;
            PersistedObject.FilterExactMatchOnlyProp = this.ChkFilterExactMatchOnly.Checked;
            PersistedObject.EnableAugmentation = ChkEnableAugmentation.Checked;
            PersistedObject.MainGroupClaimType = DdlClaimTypes.SelectedValue.Equals("none", StringComparison.InvariantCultureIgnoreCase) ? String.Empty : DdlClaimTypes.SelectedValue;
            // Deprecated options that are not shown anymore in LDAPCP configuration page
            //PersistedObject.AddWildcardInFrontOfQuery = this.ChkAddWildcardInFront.Checked;
            //PersistedObject.PickerEntityGroupName = this.TxtPickerEntityGroupName.Text;

            int timeOut;
            if (!Int32.TryParse(this.txtTimeout.Text, out timeOut) || timeOut < 0)
                timeOut = ClaimsProviderConstants.LDAPCPCONFIG_TIMEOUT; //set to default if unable to parse
            PersistedObject.LDAPQueryTimeout = timeOut;
        }

        private void UpdateAugmentationSettings()
        {
            foreach (GridViewRow item in GridLdapConnections.Rows)
            {
                CheckBox chkAugEn = (CheckBox)item.FindControl("ChkAugmentationEnableOnCoco");
                CheckBox chkIsADDomain = (CheckBox)item.FindControl("ChkGetGroupMembershipAsADDomain");
                TextBox txtId = (TextBox)item.FindControl("IdPropHidden");

                var coco = PersistedObject.LDAPConnectionsProp.First(x => x.IdProp == new Guid(txtId.Text));
                coco.AugmentationEnabledProp = chkAugEn.Checked;
                coco.GetGroupMembershipAsADDomainProp = chkIsADDomain.Checked;
            }
        }

        protected void BtnOK_Click(Object sender, EventArgs e)
        {
            if (ValidatePrerequisite() != ConfigStatus.AllGood) return;
            if (UpdateConfiguration(true)) Response.Redirect("/Security.aspx", false);
            else LabelErrorMessage.Text = MostImportantError;
        }

        protected void BtnResetLDAPCPConfig_Click(Object sender, EventArgs e)
        {
            ResetConfiguration();
        }

        protected virtual void ResetConfiguration()
        {
            LDAPCPConfig.DeleteConfiguration(PersistedObjectName);
            Response.Redirect(Request.RawUrl, false);
        }

        protected void BtnUpdateAdditionalUserLdapFilter_Click(Object sender, EventArgs e)
        {
            UpdateAdditionalUserLdapFilter();
        }

        void UpdateAdditionalUserLdapFilter()
        {
            if (PersistedObject == null) return;
            foreach (var userAttr in this.PersistedObject.ClaimTypes.Where(x => x.EntityType == DirectoryObjectType.User || x.UseMainClaimTypeOfDirectoryObject))
            {
                userAttr.AdditionalLDAPFilter = this.TxtAdditionalUserLdapFilter.Text;
            }
            this.CommitChanges();
            LabelUpdateAdditionalLdapFilterOk.Text = this.TextUpdateAdditionalLdapFilterOk;
        }

        protected void BtnTestLdapConnection_Click(Object sender, EventArgs e)
        {
            this.ValidateLdapConnection();
        }

        protected void BtnAddLdapConnection_Click(object sender, EventArgs e)
        {
            AddLdapConnection();
        }

        /// <summary>
        /// Add new LDAP connection to collection in persisted object
        /// </summary>
        void AddLdapConnection()
        {
            if (ValidatePrerequisite() != ConfigStatus.AllGood) return;

            if (this.RbUseCustomConnection.Checked && (this.TxtLdapConnectionString.Text == String.Empty || this.TxtLdapUsername.Text == String.Empty || this.TxtLdapPassword.Text == String.Empty))
            {
                this.LabelErrorTestLdapConnection.Text = TextErrorLDAPFieldsMissing;
                return;
            }

            if (this.RbUseServerDomain.Checked)
            {
                PersistedObject.LDAPConnectionsProp.Add(new LDAPConnection { UserServerDirectoryEntry = true });
            }
            else
            {
                AuthenticationTypes authNType = GetSelectedAuthenticationTypes(true);
                PersistedObject.LDAPConnectionsProp.Add(
                    new LDAPConnection
                    {
                        UserServerDirectoryEntry = false,
                        Path = this.TxtLdapConnectionString.Text,
                        Username = this.TxtLdapUsername.Text,
                        Password = this.TxtLdapPassword.Text,
                        AuthenticationTypes = authNType,
                    }
                );
            }

            CommitChanges();
            ClaimsProviderLogging.Log($"LDAP server '{this.TxtLdapConnectionString.Text}' was successfully added in configuration '{PersistedObjectName}'", TraceSeverity.Medium, EventSeverity.Information, TraceCategory.Configuration);

            PopulateLdapConnectionGrid();
            InitializeAugmentation();
            ViewState["LDAPpwd"] = String.Empty;
            this.TxtLdapPassword.Attributes.Remove("value");
            this.TxtLdapConnectionString.Text = "LDAP://";
            this.TxtLdapUsername.Text = this.TxtLdapPassword.Text = String.Empty;
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
                ClaimsProviderLogging.LogException(ClaimsProviderName, "while testing LDAP connection", TraceCategory.Configuration, ex);
                this.LabelErrorTestLdapConnection.Text = String.Format(TextErrorTestLdapConnection, ex.Message);
            }
            finally
            {
                if (deSearch != null) deSearch.Dispose();
                if (de != null) de.Dispose();
            }

            // Required to set radio buttons of LDAP connections correctly in UI
            PopulateLdapConnectionGrid();
        }

        protected void grdLDAPConnections_RowDeleting(object sender, GridViewDeleteEventArgs e)
        {
            if (ValidatePrerequisite() != ConfigStatus.AllGood) return;
            if (PersistedObject.LDAPConnectionsProp == null) return;

            GridViewRow rowToDelete = grdLDAPConnections.Rows[e.RowIndex];
            Guid Id = new Guid(rowToDelete.Cells[0].Text);
            LDAPConnection connectionToRemove = PersistedObject.LDAPConnectionsProp.FirstOrDefault(x => x.Id == Id);
            if (connectionToRemove != null)
            {
                PersistedObject.LDAPConnectionsProp.Remove(connectionToRemove);
                CommitChanges();
                ClaimsProviderLogging.Log($"LDAP server '{connectionToRemove.Directory}' was successfully removed from configuration '{PersistedObjectName}'", TraceSeverity.Medium, EventSeverity.Information, TraceCategory.Configuration);
                InitializeAugmentation();
                PopulateLdapConnectionGrid();
            }
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
                if (!item.Selected) continue;
                int selectedType;
                if (!Int32.TryParse(item.Value, out selectedType)) continue;
                authNTypes += selectedType;
                if (ClearSelection) item.Selected = false;
            }
            return authNTypes;
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
