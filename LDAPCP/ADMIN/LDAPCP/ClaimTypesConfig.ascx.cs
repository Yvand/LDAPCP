﻿using Microsoft.SharePoint.Administration.Claims;
using Microsoft.SharePoint.WebControls;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace ldapcp.ControlTemplates
{
    public partial class ClaimTypesConfigUserControl : LdapcpUserControl
    {
        public string TrustName = String.Empty; // This must be a field to be accessible from marup code, it cannot be a property
        List<KeyValuePair<int, ClaimTypeConfig>> ClaimsMapping;
        protected bool ShowNewItemForm = false;
        protected bool HideAllContent = false;

        string TextErrorFieldsMissing = "Some mandatory fields are missing.";
        string TextErrorUpdateEmptyClaimType = "Claim type must be set.";

        string HtmlCellClaimType = "<span name=\"span_claimtype_{1}\" id=\"span_claimtype_{1}\">{0}</span><input name=\"input_claimtype_{1}\" id=\"input_claimtype_{1}\" style=\"display: none; width: 90%;\" value=\"{0}\"></input>";
        string HtmlCellLAttrName = "<span name=\"span_attrname_{1}\" id=\"span_attrname_{1}\">{0}</span><input name=\"input_attrname_{1}\" id=\"input_attrname_{1}\" style=\"display:none;\" value=\"{0}\"></input>";
        string HtmlCellLAttrClass = "<span name=\"span_attrclass_{1}\" id=\"span_attrclass_{1}\">{0}</span><input name=\"input_attrclass_{1}\" id=\"input_attrclass_{1}\" style=\"display:none;\" value=\"{0}\"></input>";
        string HtmlCellLDAPAttrToDisplay = "<span name=\"span_LDAPAttrToDisplay_{1}\" id=\"span_LDAPAttrToDisplay_{1}\">{0}</span><input name=\"input_LDAPAttrToDisplay_{1}\" id=\"input_LDAPAttrToDisplay_{1}\" style=\"display:none;\" value=\"{0}\"></input>";
        string HtmlCellMetadata = "<span name=\"span_Metadata_{1}\" id=\"span_Metadata_{1}\">{0}</span><select name=\"list_Metadata_{1}\" id=\"list_Metadata_{1}\" style=\"display:none;\">{2}</select>";
        string HtmlCellLAddLDAPFilter = "<span name=\"span_AddLDAPFilter_{1}\" id=\"span_AddLDAPFilter_{1}\">{0}</span><input name=\"input_AddLDAPFilter_{1}\" id=\"input_AddLDAPFilter_{1}\" style=\"display:none;\" value=\"{0}\"></input>";
        string HtmlCellKeywordToValidateInputWithoutLookup = "<span name=\"span_KeywordToValidateInputWithoutLookup_{1}\" id=\"span_KeywordToValidateInputWithoutLookup_{1}\">{0}</span><input name=\"input_KeywordToValidateInputWithoutLookup_{1}\" id=\"input_KeywordToValidateInputWithoutLookup_{1}\" style=\"display:none;\" value=\"{0}\"></input>";
        string HtmlCellPrefixToAddToValueReturned = "<span name=\"span_PrefixToAddToValueReturned_{1}\" id=\"span_PrefixToAddToValueReturned_{1}\">{0}</span><input name=\"input_PrefixToAddToValueReturned_{1}\" id=\"input_PrefixToAddToValueReturned_{1}\" style=\"display:none;\" value=\"{0}\"></input>";
        string HtmlCellDirectoryObjectType = "<span name=\"span_ClaimEntityType_{1}\" id=\"span_ClaimEntityType_{1}\">{0}</span><select name=\"list_ClaimEntityType_{1}\" id=\"list_ClaimEntityType_{1}\" style=\"display:none;\">{2}</select>";
        string HtmlCellShowClaimNameInDisplayText = "<input type=checkbox id=\"chk_ShowClaimNameInDisplayText_{1}\" name=\"chk_ShowClaimNameInDisplayText_{1}\" {0} disabled>";

        string HtmlEditLink = "<a name=\"editLink_{0}\" id=\"editLink_{0}\" href=\"javascript:Ldapcp.ClaimsTablePage.EditItem('{0}')\">Edit</a>";
        string HtmlCancelEditLink = "<a name=\"cancelLink_{0}\" id=\"cancelLink_{0}\" href=\"javascript:Ldapcp.ClaimsTablePage.CancelEditItem('{0}')\" style=\"display:none;\">Cancel</a>";

        protected void Page_Load(object sender, EventArgs e)
        {
            Initialize();
        }

        private void Initialize()
        {
            ConfigStatus status = ValidatePrerequisite();
            if (status != ConfigStatus.AllGood && status != ConfigStatus.NoIdentityClaimType)
            {
                this.LabelErrorMessage.Text = base.MostImportantError;
                this.HideAllContent = true;
                this.BtnCreateNewItem.Visible = false;
                return;
            }

            TrustName = CurrentTrustedLoginProvider.Name;
            if (!this.IsPostBack)
            {
                // NEW ITEM FORM
                // Populate picker entity metadata DDL
                DdlNewEntityMetadata.Items.Add(String.Empty);
                Type EntityDataKeysInfo = typeof(PeopleEditorEntityDataKeys);
                foreach (object field in EntityDataKeysInfo.GetFields())
                {
                    DdlNewEntityMetadata.Items.Add(((FieldInfo)field).Name);
                }

                // Populate EntityType DDL
                foreach (var value in Enum.GetValues(typeof(DirectoryObjectType)))
                {
                    DdlNewDirectoryObjectType.Items.Add(value.ToString());
                }
            }
            BuildAttributesListTable(this.IsPostBack);
        }

        private void BuildAttributesListTable(bool pendingUpdate)
        {
            // Copy claims list in a key value pair so that each item has a unique ID that can be used later for update/delete operations
            ClaimsMapping = new List<KeyValuePair<int, ClaimTypeConfig>>();
            int i = 0;
            foreach (ClaimTypeConfig attr in this.PersistedObject.ClaimTypes)
            {
                ClaimsMapping.Add(new KeyValuePair<int, ClaimTypeConfig>(i++, attr));
            }

            bool identityClaimPresent = false;

            TblClaimsMapping.Rows.Clear();

            // FIRST ROW HEADERS
            TableRow tr = new TableRow();
            TableHeaderCell th;
            th = GetTableHeaderCell("Actions");
            th.RowSpan = 2;
            tr.Cells.Add(th);
            th = GetTableHeaderCell("Claim type");
            th.RowSpan = 2;
            tr.Cells.Add(th);
            th = GetTableHeaderCell("Directory object details");
            th.ColumnSpan = 4;
            tr.Cells.Add(th);
            th = GetTableHeaderCell("Optional settings");
            th.ColumnSpan = 5;
            tr.Cells.Add(th);
            this.TblClaimsMapping.Rows.Add(tr);

            // SECONDE ROW HEADERS
            tr = new TableRow();
            th = new TableHeaderCell();
            th = GetTableHeaderCell("Entity type");
            tr.Cells.Add(th);
            th = GetTableHeaderCell("LDAP class");
            tr.Cells.Add(th);
            th = GetTableHeaderCell("LDAP attribute to query");
            tr.Cells.Add(th);
            th = GetTableHeaderCell("LDAP attribute to display");
            tr.Cells.Add(th);
            th = GetTableHeaderCell("<a href='http://msdn.microsoft.com/en-us/library/office/microsoft.sharepoint.webcontrols.peopleeditorentitydatakeys_members(v=office.15).aspx' target='_blank'>PickerEntity Metadata</a>");
            tr.Cells.Add(th);
            th = GetTableHeaderCell("Additional LDAP filter");
            tr.Cells.Add(th);
            th = GetTableHeaderCell("Prefix to bypass lookup");
            tr.Cells.Add(th);
            th = GetTableHeaderCell("Claim value prefix");
            tr.Cells.Add(th);
            //th = GetTableHeaderCell("Show claim name in display text");
            //tr.Cells.Add(th);
            this.TblClaimsMapping.Rows.Add(tr);

            foreach (var attr in this.ClaimsMapping)
            {
                tr = new TableRow();
                bool allowEditItem = String.IsNullOrEmpty(attr.Value.ClaimType) ? false : true;
                bool isIdentityClaimType = String.Equals(attr.Value.ClaimType, CurrentTrustedLoginProvider.IdentityClaimTypeInformation.MappedClaimType, StringComparison.InvariantCultureIgnoreCase) && !attr.Value.UseMainClaimTypeOfDirectoryObject ? true : false;

                // ACTIONS
                // LinkButton must always be created otherwise event receiver will not fire on postback 
                TableCell tc = new TableCell();
                if (allowEditItem) tc.Controls.Add(new LiteralControl(String.Format(HtmlEditLink, attr.Key) + "&nbsp;&nbsp;"));

                // Don't allow to delete identity claim
                if (!isIdentityClaimType)
                {
                    LinkButton LnkDeleteItem = new LinkButton();
                    LnkDeleteItem.ID = "DeleteItemLink_" + attr.Key;
                    LnkDeleteItem.Command += LnkDeleteItem_Command;
                    LnkDeleteItem.CommandArgument = attr.Key.ToString();
                    //LnkDeleteItem.Text = "<div class='ms-cui-img-16by16 ms-cui-img-cont-float'><img style='left: -271px; top: -271px;' alt='' src='/_layouts/15/1033/images/formatmap16x16.png?rev=23' unselectable='on'></div>";
                    LnkDeleteItem.Text = "Delete";
                    LnkDeleteItem.OnClientClick = "javascript:return confirm('This will delete this item. Do you want to continue?');";
                    if (pendingUpdate) LnkDeleteItem.Visible = false;
                    tc.Controls.Add(LnkDeleteItem);
                }

                LinkButton LnkUpdateItem = new LinkButton();
                LnkUpdateItem.ID = "UpdateItemLink_" + attr.Key;
                LnkUpdateItem.Command += LnkUpdateItem_Command;
                LnkUpdateItem.CommandArgument = attr.Key.ToString();
                LnkUpdateItem.Text = "Save";
                LnkUpdateItem.Style.Add("display", "none");
                if (pendingUpdate) LnkUpdateItem.Visible = false;

                tc.Controls.Add(LnkUpdateItem);
                tc.Controls.Add(new LiteralControl("&nbsp;&nbsp;" + String.Format(HtmlCancelEditLink, attr.Key)));
                tr.Cells.Add(tc);

                // This is just to avoid building the table if we know that there is a pending update, which means it will be rebuilt again after update is complete
                if (!pendingUpdate)
                {
                    // CLAIM TYPE
                    string html;
                    TableCell c = null;
                    // Check if claim type is set, and if it exists in the current trust
                    if (!String.IsNullOrEmpty(attr.Value.ClaimType))
                    {
                        html = String.Format(HtmlCellClaimType, attr.Value.ClaimType, attr.Key);
                        c = GetTableCell(html);
                        allowEditItem = true;
                        if (isIdentityClaimType)
                        {
                            tr.CssClass = "ldapcp-rowidentityclaim";
                            identityClaimPresent = true;
                        }
                        else if (CurrentTrustedLoginProvider.ClaimTypeInformation.FirstOrDefault(x => String.Equals(x.MappedClaimType, attr.Value.ClaimType, StringComparison.InvariantCultureIgnoreCase)) == null)
                        {
                            tr.CssClass = "ldapcp-rowClaimTypeNotUsedInTrust";
                        }
                        else if (attr.Value.EntityType == DirectoryObjectType.Group && String.Equals(this.PersistedObject.MainGroupClaimType, attr.Value.ClaimType, StringComparison.InvariantCultureIgnoreCase))
                        {
                            tr.CssClass = "ldapcp-rowMainGroupClaimType";
                        }
                    }
                    else
                    {
                        if (!attr.Value.UseMainClaimTypeOfDirectoryObject)
                        {
                            c = GetTableCell("Map LDAP attribute with a PickerEntity metadata");
                        }
                        else
                        {
                            c = GetTableCell($"Use main claim type of object {attr.Value.EntityType}");
                            if (attr.Value.EntityType == DirectoryObjectType.User)
                            {
                                tr.CssClass = "ldapcp-rowUserProperty";
                            }
                            else
                            {
                                tr.CssClass = "ldapcp-rowGroupProperty";
                            }
                        }
                    }
                    tr.Cells.Add(c);

                    // DIRECTORY OBJECT SETTINGS
                    html = BuildDirectoryObjectTypeDDL(attr);
                    tr.Cells.Add(GetTableCell(html));

                    html = String.Format(HtmlCellLAttrClass, attr.Value.LDAPClass, attr.Key);
                    tr.Cells.Add(GetTableCell(html));

                    html = String.Format(HtmlCellLAttrName, attr.Value.LDAPAttribute, attr.Key);
                    tr.Cells.Add(GetTableCell(html));

                    html = String.Format(HtmlCellLDAPAttrToDisplay, attr.Value.LDAPAttributeToShowAsDisplayText, attr.Key);
                    tr.Cells.Add(GetTableCell(html));

                    // OPTIONAL SETTINGS
                    MemberInfo[] members;
                    members = typeof(PeopleEditorEntityDataKeys).GetFields(BindingFlags.Static | BindingFlags.Public);
                    html = BuildDDLFromTypeMembers(HtmlCellMetadata, attr, "EntityDataKey", members, true);
                    tr.Cells.Add(GetTableCell(html));

                    html = String.Format(HtmlCellLAddLDAPFilter, attr.Value.AdditionalLDAPFilter, attr.Key);
                    tr.Cells.Add(GetTableCell(html));

                    html = String.Format(HtmlCellKeywordToValidateInputWithoutLookup, attr.Value.PrefixToBypassLookup, attr.Key);
                    tr.Cells.Add(GetTableCell(html));

                    html = String.Format(HtmlCellPrefixToAddToValueReturned, attr.Value.ClaimValuePrefix, attr.Key);
                    tr.Cells.Add(GetTableCell(html));

                    //if (isIdentityClaimType || !allowEditItem) html = String.Empty;
                    //else
                    //{
                    //    string strChecked = attr.Value.ShowClaimNameInDisplayText ? "checked" : String.Empty;
                    //    html = String.Format(HtmlCellShowClaimNameInDisplayText, strChecked, attr.Key);
                    //}
                    //tr.Cells.Add(GetTableCell(html));
                }
                TblClaimsMapping.Rows.Add(tr);
            }

            if (!identityClaimPresent && !pendingUpdate)
            {
                LabelErrorMessage.Text = String.Format(TextErrorNoIdentityClaimType, CurrentTrustedLoginProvider.DisplayName, CurrentTrustedLoginProvider.IdentityClaimTypeInformation.MappedClaimType);
            }
        }

        private string BuildDDLFromTypeMembers(string htmlCell, KeyValuePair<int, ClaimTypeConfig> attr, string propertyToCheck, MemberInfo[] members, bool addEmptyChoice)
        {
            string option = "<option value=\"{0}\" {1}>{2}</option>";
            string selected = String.Empty;
            bool metadataFound = false;
            StringBuilder options = new StringBuilder();

            // GetValue returns null if object doesn't have a value on this property, using "as string" avoids to throw a NullReference in this case.
            string attrValue = typeof(ClaimTypeConfig).GetProperty(propertyToCheck).GetValue(attr.Value) as string;

            // Build DDL based on members retrieved from the supplied type
            foreach (MemberInfo member in members)
            {
                if (String.Equals(attrValue, member.Name, StringComparison.InvariantCultureIgnoreCase))
                {
                    selected = "selected";
                    metadataFound = true;
                }
                else selected = String.Empty;
                options.Append(String.Format(option, member.Name, selected, member.Name));
            }

            if (addEmptyChoice)
            {
                selected = metadataFound ? String.Empty : "selected";
                options = options.Insert(0, String.Format(option, String.Empty, selected, String.Empty));
            }
            return String.Format(htmlCell, attrValue, attr.Key, options.ToString());
        }

        private string BuildDirectoryObjectTypeDDL(KeyValuePair<int, ClaimTypeConfig> azureObject)
        {
            string option = "<option value=\"{0}\" {1}>{2}</option>";
            StringBuilder directoryObjectTypeOptions = new StringBuilder();

            string selectedText = azureObject.Value.EntityType == DirectoryObjectType.User ? "selected" : String.Empty;
            directoryObjectTypeOptions.Append(String.Format(option, DirectoryObjectType.User.ToString(), selectedText, DirectoryObjectType.User.ToString()));
            selectedText = azureObject.Value.EntityType == DirectoryObjectType.Group ? "selected" : String.Empty;
            directoryObjectTypeOptions.Append(String.Format(option, DirectoryObjectType.Group.ToString(), selectedText, DirectoryObjectType.Group.ToString()));

            return String.Format(HtmlCellDirectoryObjectType, azureObject.Value.EntityType, azureObject.Key, directoryObjectTypeOptions.ToString());
        }

        private TableHeaderCell GetTableHeaderCell(string Value)
        {
            TableHeaderCell tc = new TableHeaderCell();
            tc.Text = Value;
            return tc;
        }
        private TableCell GetTableCell(string Value)
        {
            TableCell tc = new TableCell();
            tc.Text = Value;
            return tc;
        }        

        void LnkDeleteItem_Command(object sender, CommandEventArgs e)
        {
            if (ValidatePrerequisite() != ConfigStatus.AllGood && Status != ConfigStatus.NoIdentityClaimType) return;

            string itemId = e.CommandArgument.ToString();
            ClaimTypeConfig ctConfig = ClaimsMapping.Find(x => x.Key == Convert.ToInt32(itemId)).Value;
            PersistedObject.ClaimTypes.Remove(ctConfig);
            CommitChanges();
            this.BuildAttributesListTable(false);
        }

        void LnkUpdateItem_Command(object sender, CommandEventArgs e)
        {
            if (ValidatePrerequisite() != ConfigStatus.AllGood && Status != ConfigStatus.NoIdentityClaimType) return;

            string itemId = e.CommandArgument.ToString();
            ClaimTypeConfig existingCTConfig = ClaimsMapping.Find(x => x.Key == Convert.ToInt32(itemId)).Value;

            // Get new values
            NameValueCollection formData = Request.Form;
            string newClaimType = formData["input_claimtype_" + itemId].Trim();
            string newDirectoryObjectType = formData["list_ClaimEntityType_" + itemId];
            Enum.TryParse(newDirectoryObjectType, out DirectoryObjectType directoryObjectTypeSelected);

            if (String.IsNullOrEmpty(newClaimType))
            {
                this.LabelErrorMessage.Text = TextErrorUpdateEmptyClaimType;
                BuildAttributesListTable(false);
                return;
            }

            ClaimTypeConfig newCTConfig = existingCTConfig.CopyCurrentObject();
            newCTConfig.ClaimType = newClaimType;
            newCTConfig.EntityType = directoryObjectTypeSelected;
            newCTConfig.LDAPClass = formData["input_attrclass_" + itemId].Trim();
            newCTConfig.LDAPAttribute = formData["input_attrname_" + itemId].Trim();
            newCTConfig.LDAPAttributeToShowAsDisplayText = formData["input_LDAPAttrToDisplay_" + itemId];
            newCTConfig.EntityDataKey = formData["list_Metadata_" + itemId];
            newCTConfig.AdditionalLDAPFilter = formData["input_AddLDAPFilter_" + itemId];
            newCTConfig.PrefixToBypassLookup = formData["input_KeywordToValidateInputWithoutLookup_" + itemId];
            newCTConfig.ClaimValuePrefix = formData["input_PrefixToAddToValueReturned_" + itemId].ToLower();
            //string newShowClaimNameInDisplayText = formData["chk_ShowClaimNameInDisplayText_" + itemId];
            //newCTConfig.ShowClaimNameInDisplayText = String.IsNullOrEmpty(newShowClaimNameInDisplayText) ? false : true;

            try
            {
                // ClaimTypeConfigCollection.Update() may thrown an exception if new ClaimTypeConfig is not valid for any reason
                PersistedObject.ClaimTypes.Update(existingCTConfig.ClaimType, newCTConfig);
            }
            catch (Exception ex)
            {
                this.LabelErrorMessage.Text = ex.Message;
                BuildAttributesListTable(false);
                return;
            }

            CommitChanges();
            this.BuildAttributesListTable(false);
        }

        protected void BtnReset_Click(object sender, EventArgs e)
        {
            PersistedObject.ResetClaimTypesList();
            PersistedObject.Update();
            Response.Redirect(Request.Url.ToString());
        }

        /// <summary>
        /// Add a new claim type configuration
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void BtnCreateNewItem_Click(object sender, EventArgs e)
        {
            string newClaimType = TxtNewClaimType.Text.Trim();
            string newLdapAttribute = TxtNewAttrName.Text.Trim();
            string newLdapClass = TxtNewObjectClass.Text.Trim();
            DirectoryObjectType newDirectoryObjectType;
            Enum.TryParse<DirectoryObjectType>(DdlNewDirectoryObjectType.SelectedValue, out newDirectoryObjectType);
            string newEntityMetadata = DdlNewEntityMetadata.SelectedValue;
            bool useMainClaimTypeOfDirectoryObject = false;

            if (RdbNewItemClassicClaimType.Checked)
            {
                if (String.IsNullOrEmpty(newClaimType))
                {
                    this.LabelErrorMessage.Text = TextErrorFieldsMissing;
                    ShowNewItemForm = true;
                    BuildAttributesListTable(false);
                    return;
                }
            }
            else if (RdbNewItemPermissionMetadata.Checked)
            {
                if (String.IsNullOrEmpty(newEntityMetadata))
                {
                    this.LabelErrorMessage.Text = TextErrorFieldsMissing;
                    ShowNewItemForm = true;
                    BuildAttributesListTable(false);
                    return;
                }
                newClaimType = String.Empty;
            }
            else
            {
                useMainClaimTypeOfDirectoryObject = true;
                newClaimType = String.Empty;
            }

            ClaimTypeConfig newCTConfig = new ClaimTypeConfig();
            newCTConfig.ClaimType = newClaimType;
            newCTConfig.EntityType = newDirectoryObjectType;
            newCTConfig.LDAPClass = newLdapClass;
            newCTConfig.LDAPAttribute = newLdapAttribute;
            newCTConfig.UseMainClaimTypeOfDirectoryObject = useMainClaimTypeOfDirectoryObject;
            newCTConfig.EntityDataKey = newEntityMetadata;

            try
            {
                // ClaimTypeConfigCollection.Add() may thrown an exception if new ClaimTypeConfig is not valid for any reason
                PersistedObject.ClaimTypes.Add(newCTConfig);
            }
            catch (Exception ex)
            {
                this.LabelErrorMessage.Text = ex.Message;
                ShowNewItemForm = true;
                BuildAttributesListTable(false);
                return;
            }

            // Update configuration and rebuild table with new configuration
            CommitChanges();
            BuildAttributesListTable(false);
        }
    }
}
