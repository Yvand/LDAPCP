﻿using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.WebControls;
using System;
using System.Collections.Generic;
using System.Data;
using System.DirectoryServices;
using System.Linq;
using System.Web.UI.WebControls;
using Yvand.LdapClaimsProvider.Configuration;
using static Microsoft.SharePoint.MobileMessage.SPMobileMessageServiceProvider;
using AuthenticationTypes = System.DirectoryServices.AuthenticationTypes;
using LdapConnection = Yvand.LdapClaimsProvider.Configuration.LdapConnection;

namespace Yvand.LdapClaimsProvider.Administration
{
    public partial class GlobalSettingsUserControl : LDAPCPSEUserControl
    {
        readonly string TextConnectionSuccessful = "Connection successful.";
        readonly string TextSummaryPersistedObjectInformation = "Found configuration '{0}' v{1} (Persisted Object ID: '{2}')";
        readonly string TextSharePointDomain = "Connect to SharePoint domain";
        readonly string TextErrorLDAPFieldsMissing = "Some mandatory fields are missing.";
        readonly string TextErrorTestLdapConnection = "Unable to connect to LDAP for following reason:<br/>{0}<br/>It may be expected if w3wp process of central admin has intentionally no access to LDAP server.";
        readonly string TextUpdateAdditionalLdapFilterOk = "LDAP filter was successfully applied to all LDAP attributes of class 'user'.";

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
            PopulateConnectionsGrid();
            if (!this.IsPostBack)
            {
                PopulateCblAuthenticationTypes();
                PopulateFields();
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

        void PopulateConnectionsGrid()
        {
            if (Settings.LdapConnections != null)
            {
                PropertyCollectionBinder pcb = new PropertyCollectionBinder();
                foreach (LdapConnection ldapConnection in Settings.LdapConnections)
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
            this.ChkIdentityShowAdditionalAttribute.Checked = true; // Settings.DisplayLdapMatchForIdentityClaimTypeProp;
            if (String.IsNullOrEmpty(IdentityCTConfig.LDAPAttributeToShowAsDisplayText))
            {
                this.RbIdentityDefault.Checked = true;
            }
            else
            {
                this.RbIdentityCustomLDAP.Checked = true;
                this.TxtLdapAttributeToDisplay.Text = IdentityCTConfig.LDAPAttributeToShowAsDisplayText;
            }
            this.TxtUserIdentifierLDAPClass.Text = IdentityCTConfig.LDAPClass;
            this.TxtUserIdentifierLDAPAttribute.Text = IdentityCTConfig.LDAPAttribute;

            this.ChkAlwaysResolveUserInput.Checked = Settings.AlwaysResolveUserInput;
            this.ChkFilterEnabledUsersOnly.Checked = Settings.FilterEnabledUsersOnly;
            this.ChkFilterSecurityGroupsOnly.Checked = Settings.FilterSecurityGroupsOnly;
            this.ChkFilterExactMatchOnly.Checked = Settings.FilterExactMatchOnly;
            this.txtTimeout.Text = Settings.Timeout.ToString();
            // Deprecated options that are not shown anymore in LDAPCP configuration page
            //this.ChkAddWildcardInFront.Checked = Settings.AddWildcardInFrontOfQueryProp;
            //this.TxtPickerEntityGroupName.Text = Settings.PickerEntityGroupNameProp;

            // Init controls for user identifier configuration
            this.TxtUserIdLdapClass.Text = IdentityCTConfig.LDAPClass;
            this.TxtUserIdLdapAttribute.Text = IdentityCTConfig.LDAPAttribute;
            this.TxtUserIdAdditionalLdapAttributes.Text = String.Join(",", Utils.GetAdditionalLdapAttributes(Settings.ClaimTypes, DirectoryObjectType.User));
            this.TxtUserIdLeadingToken.Text = IdentityCTConfig.ClaimValuePrefix;

            // Init controls for group configuration
            ClaimTypeConfig groupCtc = Utils.GetMainGroupClaimTypeConfig(Settings.ClaimTypes);
            var groupClaimTypeCandidates = Utils.GetNonWellKnownUserClaimTypes(base.ClaimsProviderName);
            foreach (string groupClaimTypeCandidate in groupClaimTypeCandidates)
            {
                ListItem groupClaimTypeCandidateItem = new ListItem(groupClaimTypeCandidate);
                DdlGroupClaimType.Items.Add(groupClaimTypeCandidateItem);
            }

            DdlGroupClaimType.SelectedValue = groupCtc?.ClaimType;
            if (groupCtc != null)
            {
                TxtGroupLdapClass.Text = groupCtc.LDAPClass;
                TxtGroupLdapAttribute .Text = groupCtc.LDAPAttribute;
                TxtGroupAdditionalLdapAttributes.Text = String.Join(",", Utils.GetAdditionalLdapAttributes(Settings.ClaimTypes, DirectoryObjectType.Group));
                TxtGroupLeadingToken.Text = groupCtc.ClaimValuePrefix;
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
            LdapConnection tenantToRemove = Settings.LdapConnections.FirstOrDefault(x => x.Identifier == Id);
            if (tenantToRemove != null)
            {
                Settings.LdapConnections.Remove(tenantToRemove);
                CommitChanges();
                Logger.Log($"Microsoft Entra ID tenant '{tenantToRemove.LdapPath}' was successfully removed from configuration '{ConfigurationName}'", TraceSeverity.Medium, EventSeverity.Information, TraceCategory.Configuration);
                LabelMessage.Text = String.Format(TextSummaryPersistedObjectInformation, Configuration.Name, Configuration.Version, Configuration.Id);
                PopulateConnectionsGrid();
            }
        }

        protected bool UpdateConfiguration(bool commitChanges)
        {
            if (ValidatePrerequisite() != ConfigStatus.AllGood) { return false; }


            // Handle identity claim type
            //PersistedObject.DisplayLdapMatchForIdentityClaimTypeProp = this.ChkIdentityShowAdditionalAttribute.Checked;
            if (this.RbIdentityCustomLDAP.Checked)
            {
                IdentityCTConfig.LDAPAttributeToShowAsDisplayText = this.TxtLdapAttributeToDisplay.Text;
            }
            else
            {
                IdentityCTConfig.LDAPAttributeToShowAsDisplayText = String.Empty;
            }

            if (!String.IsNullOrWhiteSpace(TxtUserIdentifierLDAPClass.Text) && !String.IsNullOrWhiteSpace(TxtUserIdentifierLDAPAttribute.Text))
            {
                Settings.ClaimTypes.UpdateUserIdentifier(TxtUserIdentifierLDAPClass.Text, TxtUserIdentifierLDAPAttribute.Text);
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

            //UpdateAugmentationSettings()
            //foreach (GridViewRow item in GridLdapConnections.Rows)
            //{
            //    CheckBox chkAugEn = (CheckBox)item.FindControl("ChkAugmentationEnableOnCoco");
            //    CheckBox chkIsADDomain = (CheckBox)item.FindControl("ChkGetGroupMembershipAsADDomain");
            //    TextBox txtId = (TextBox)item.FindControl("IdPropHidden");

            //    var coco = PersistedObject.LDAPConnectionsProp.First(x => x.Identifier == new Guid(txtId.Text));
            //    coco.EnableAugmentation = chkAugEn.Checked;
            //    coco.GetGroupMembershipUsingDotNetHelpers = chkIsADDomain.Checked;
            //}

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
        /// Add new Microsoft Entra ID tenant in persisted object
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
                Settings.LdapConnections.Add(
                    new LdapConnection
                    {
                        UseDefaultADConnection = true,
                        EnableAugmentation = true,
                    }
                );
            }
            else
            {
                AuthenticationTypes authNType = GetSelectedAuthenticationTypes(true);
                Settings.LdapConnections.Add(
                    new LdapConnection
                    {
                        UseDefaultADConnection = false,
                        LdapPath = this.TxtLdapConnectionString.Text,
                        Username = this.TxtLdapUsername.Text,
                        Password = this.TxtLdapPassword.Text,
                        AuthenticationType = authNType,
                        EnableAugmentation = true,
                    }
                );
            }

            CommitChanges();
            Logger.Log($"LDAP server '{this.TxtLdapConnectionString.Text}' was successfully added to configuration '{ConfigurationName}'", TraceSeverity.Medium, EventSeverity.Information, TraceCategory.Configuration);
            PopulateConnectionsGrid();
            //InitializeAugmentation();
            ViewState["LDAPpwd"] = String.Empty;
            this.TxtLdapPassword.Attributes.Remove("value");
            this.TxtLdapConnectionString.Text = "LDAP://";
            this.TxtLdapUsername.Text = String.Empty;
            this.TxtLdapPassword.Text = String.Empty;
        }

        protected void BtnUpdateAdditionalUserLdapFilter_Click(Object sender, EventArgs e)
        {
            UpdateAdditionalUserLdapFilter();
        }

        void UpdateAdditionalUserLdapFilter()
        {
            if (ValidatePrerequisite() != ConfigStatus.AllGood) { return; }
            foreach (var userAttr in this.Settings.ClaimTypes.Where(x => x.EntityType == DirectoryObjectType.User))
            {
                userAttr.AdditionalLDAPFilter = this.TxtAdditionalUserLdapFilter.Text;
            }
            this.CommitChanges();
            LabelUpdateAdditionalLdapFilterOk.Text = this.TextUpdateAdditionalLdapFilterOk;
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