using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
using Microsoft.SharePoint.WebControls;
using System;
using System.Collections.Generic;
using System.Data;
using System.DirectoryServices;
using System.Linq;
using System.Web.UI.WebControls;

namespace ldapcp.ControlTemplates
{
    public partial class GlobalSettings : LdapcpUserControl
    {
        public bool ShowValidateSection
        {
            get { return ValidateSection.Visible; }
            set { ValidateSection.Visible = ValidateTopSection.Visible = value; }
        }

        public bool ShowCurrentLdapConnectionSection
        {
            get { return CurrentLdapConnectionSection.Visible; }
            set { CurrentLdapConnectionSection.Visible = value; }
        }

        public bool ShowNewLdapConnectionSection
        {
            get { return NewLdapConnectionSection.Visible; }
            set { NewLdapConnectionSection.Visible = value; }
        }

        public bool ShowAugmentationSection
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
            ViewState["IsDefaultADConnectionCreated"] = false;
            ViewState["ForceCheckCustomLdapConnection"] = false;

            if (ValidatePrerequisite() != ConfigStatus.AllGood)
            {
                this.LabelErrorMessage.Text = base.MostImportantError;
                this.BtnOK.Enabled = this.BtnOKTop.Enabled = false;
                return;
            }

            if (!this.IsPostBack) Initialize();

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

        public void Initialize()
        {
            PopulateLdapConnectionGrid();
            PopulateCblAuthenticationTypes();
            InitializeAugmentation();
            InitializeGeneralSettings();
        }

        private void InitializeAugmentation()
        {
            IEnumerable<AttributeHelper> potentialGroupClaimTypes = PersistedObject.AttributesListProp.Where(x => x.ClaimEntityType == SPClaimEntityTypes.FormsRole || x.ClaimEntityType == SPClaimEntityTypes.SecurityGroup);
            if (potentialGroupClaimTypes == null || potentialGroupClaimTypes.Count() == 0)
            {
                LabelErrorMessage.Text = TextErrorNoGroupClaimType;
                return;
            }

            foreach (var potentialGroup in potentialGroupClaimTypes)
            {
                DdlClaimTypes.Items.Add(potentialGroup.ClaimType);
            }

            ChkEnableAugmentation.Checked = PersistedObject.AugmentationEnabledProp;

            if (!String.IsNullOrEmpty(PersistedObject.AugmentationClaimTypeProp) && DdlClaimTypes.Items.FindByValue(PersistedObject.AugmentationClaimTypeProp) != null)
                DdlClaimTypes.SelectedValue = PersistedObject.AugmentationClaimTypeProp;

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
            Dictionary<int, string> authenticationTypesDS = EnumToList(typeof(AuthenticationTypes));
            foreach (KeyValuePair<int, string> authNType in authenticationTypesDS)
            {
                ListItem authNTypeItem = new ListItem();
                authNTypeItem.Text = authNType.Value;
                authNTypeItem.Value = authNType.Key.ToString();
                CblAuthenticationTypes.Items.Add(authNTypeItem);
            }
        }

        private void InitializeGeneralSettings()
        {
            this.ChkIdentityShowAdditionalAttribute.Checked = PersistedObject.DisplayLdapMatchForIdentityClaimTypeProp;
            if (String.IsNullOrEmpty(IdentityClaim.LDAPAttributeToDisplayProp))
            {
                this.RbIdentityDefault.Checked = true;
            }
            else
            {
                this.RbIdentityCustomLDAP.Checked = true;
                this.TxtLdapAttributeToDisplay.Text = IdentityClaim.LDAPAttributeToDisplayProp;
            }

            this.ChkAlwaysResolveUserInput.Checked = PersistedObject.AlwaysResolveUserInputProp;
            this.ChkFilterEnabledUsersOnly.Checked = PersistedObject.FilterEnabledUsersOnlyProp;
            this.ChkFilterSecurityGroupsOnly.Checked = PersistedObject.FilterSecurityGroupsOnlyProp;
            this.ChkFilterExactMatchOnly.Checked = PersistedObject.FilterExactMatchOnlyProp;
            this.txtTimeout.Text = PersistedObject.TimeoutProp.ToString();

            // Deprecated options that are not shown anymore in LDAPCP configuration page
            //this.ChkAddWildcardInFront.Checked = PersistedObject.AddWildcardInFrontOfQueryProp;
            //this.TxtPickerEntityGroupName.Text = PersistedObject.PickerEntityGroupNameProp;
        }

        public override bool UpdatePersistedObjectProperties(bool commitChanges)
        {
            if (ValidatePrerequisite() != ConfigStatus.AllGood) return false;
            UpdateLdapSettings();
            UpdateAugmentationSettings();
            UpdateGeneralSettings();
            if (commitChanges) CommitChanges();
            return true;
        }

        private void UpdateGeneralSettings()
        {
            // Handle identity claim type
            PersistedObject.DisplayLdapMatchForIdentityClaimTypeProp = this.ChkIdentityShowAdditionalAttribute.Checked;
            if (this.RbIdentityCustomLDAP.Checked)
            {
                IdentityClaim.LDAPAttributeToDisplayProp = this.TxtLdapAttributeToDisplay.Text;
            }
            else
            {
                IdentityClaim.LDAPAttributeToDisplayProp = String.Empty;
            }

            PersistedObject.AlwaysResolveUserInputProp = this.ChkAlwaysResolveUserInput.Checked;
            PersistedObject.FilterEnabledUsersOnlyProp = this.ChkFilterEnabledUsersOnly.Checked;
            PersistedObject.FilterSecurityGroupsOnlyProp = this.ChkFilterSecurityGroupsOnly.Checked;
            PersistedObject.FilterExactMatchOnlyProp = this.ChkFilterExactMatchOnly.Checked;
            // Deprecated options that are not shown anymore in LDAPCP configuration page
            //PersistedObject.AddWildcardInFrontOfQuery = this.ChkAddWildcardInFront.Checked;
            //PersistedObject.PickerEntityGroupName = this.TxtPickerEntityGroupName.Text;

            int timeOut;
            if (!Int32.TryParse(this.txtTimeout.Text, out timeOut) || timeOut < 0)
                timeOut = Constants.LDAPCPCONFIG_TIMEOUT; //set to default if unable to parse
            PersistedObject.TimeoutProp = timeOut;
        }

        private void UpdateLdapSettings()
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

        private void UpdateAugmentationSettings()
        {
            PersistedObject.AugmentationEnabledProp = ChkEnableAugmentation.Checked;
            PersistedObject.AugmentationClaimTypeProp = DdlClaimTypes.SelectedValue.Equals("none", StringComparison.InvariantCultureIgnoreCase) ? String.Empty : DdlClaimTypes.SelectedValue;
        }

        protected void BtnOK_Click(Object sender, EventArgs e)
        {
            if (ValidatePrerequisite() != ConfigStatus.AllGood) return;
            if (UpdatePersistedObjectProperties(true)) Response.Redirect("/Security.aspx", false);
            else LabelErrorMessage.Text = MostImportantError;
        }

        protected void BtnResetLDAPCPConfig_Click(Object sender, EventArgs e)
        {
            ResetConfiguration();
        }

        protected virtual void ResetConfiguration()
        {
            LDAPCPConfig.DeleteLDAPCPConfig();
            Response.Redirect(Request.RawUrl, false);
        }

        protected void BtnUpdateAdditionalUserLdapFilter_Click(Object sender, EventArgs e)
        {
            UpdateAdditionalUserLdapFilter();
        }

        void UpdateAdditionalUserLdapFilter()
        {
            if (PersistedObject == null) return;
            foreach (var userAttr in this.PersistedObject.AttributesListProp.FindAll(x => x.ClaimEntityType == SPClaimEntityTypes.User || x.CreateAsIdentityClaim))
            {
                userAttr.AdditionalLDAPFilterProp = this.TxtAdditionalUserLdapFilter.Text;
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

            // Update object in database
            CommitChanges();
            LdapcpLogging.Log(
                   String.Format("Added a new LDAP connection in PersistedObject {0}", Constants.LDAPCPCONFIG_NAME),
                   TraceSeverity.Medium,
                   EventSeverity.Information,
                   LdapcpLogging.Categories.Configuration);

            PopulateLdapConnectionGrid();
            InitializeAugmentation();
            ViewState["LDAPpwd"] = String.Empty;
            TxtLdapPassword.Attributes.Remove("value");
            this.TxtLdapUsername.Text = this.TxtLdapPassword.Text = String.Empty;
            this.TxtLdapConnectionString.Text = "LDAP://";
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
                LdapcpLogging.LogException(LDAPCP._ProviderInternalName, "while testing LDAP connection", LdapcpLogging.Categories.Configuration, ex);
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
            PersistedObject.LDAPConnectionsProp.Remove(PersistedObject.LDAPConnectionsProp.Find(x => x.Id == Id));

            // Update object in database
            CommitChanges();
            LdapcpLogging.Log(
                    String.Format("Removed a LDAP connection in PersistedObject {0}", Constants.LDAPCPCONFIG_NAME),
                    TraceSeverity.Medium,
                    EventSeverity.Information,
                    LdapcpLogging.Categories.Configuration);

            InitializeAugmentation();
            PopulateLdapConnectionGrid();
        }

        public static Dictionary<int, string> EnumToList(Type t)
        {
            Dictionary<int, string> list = new Dictionary<int, string>();
            foreach (var v in Enum.GetValues(t))
            {
                string name = Enum.GetName(t, (int)v);
                // Encryption and SecureSocketsLayer have same value and it will violate uniqueness of key if attempt to add both to Dictionary
                if (String.Equals(name, "Encryption", StringComparison.InvariantCultureIgnoreCase) && list.ContainsValue("Encryption")) continue;
                list.Add((int)v, name);
            }
            return list;
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
