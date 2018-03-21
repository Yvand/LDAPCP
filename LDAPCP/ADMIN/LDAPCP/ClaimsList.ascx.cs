using Microsoft.SharePoint.Administration.Claims;
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
    public partial class ClaimsList : LdapcpUserControl
    {
        protected string CurrentTrustedLoginProviderName = String.Empty;
        List<KeyValuePair<int, ClaimTypeConfig>> ClaimsMapping;
        protected bool ShowNewItemForm = false;
        protected bool HideAllContent = false;

        string TextErrorFieldsMissing = "Some mandatory fields are missing.";
        string TextErrorDuplicateClaimType = "This claim type already exists in the list, you cannot create duplicates.";
        string TextErrorUpdateItemDuplicate = "You tried to update item {0} with a {1} that already exists ({2}). Duplicates are not allowed.";
        string TextErrorUpdateIdentityClaimTypeChanged = "You cannot change claim type of identity claim.";
        string TextErrorIdentityClaimTypeNotUser = "Identity claim must be set to SPClaimEntityTypes.User.";
        string TextErrorNewMetadataAlreadyUsed = "Metadata {0} is already used for the claim entity type {1}. Duplicates are not allowed.";
        string TextErrorDuplicateLdapAttrAndClass = "The LDAP attribute/class specified are already used.";

        string HtmlCellClaimType = "<span name=\"span_claimtype_{1}\" id=\"span_claimtype_{1}\">{0}</span><input name=\"input_claimtype_{1}\" id=\"input_claimtype_{1}\" style=\"display: none; width: 90%;\" value=\"{0}\"></input>";
        string HtmlCellLAttrName = "<span name=\"span_attrname_{1}\" id=\"span_attrname_{1}\">{0}</span><input name=\"input_attrname_{1}\" id=\"input_attrname_{1}\" style=\"display:none;\" value=\"{0}\"></input>";
        string HtmlCellLAttrClass = "<span name=\"span_attrclass_{1}\" id=\"span_attrclass_{1}\">{0}</span><input name=\"input_attrclass_{1}\" id=\"input_attrclass_{1}\" style=\"display:none;\" value=\"{0}\"></input>";
        string HtmlCellLDAPAttrToDisplay = "<span name=\"span_LDAPAttrToDisplay_{1}\" id=\"span_LDAPAttrToDisplay_{1}\">{0}</span><input name=\"input_LDAPAttrToDisplay_{1}\" id=\"input_LDAPAttrToDisplay_{1}\" style=\"display:none;\" value=\"{0}\"></input>";
        string HtmlCellMetadata = "<span name=\"span_Metadata_{1}\" id=\"span_Metadata_{1}\">{0}</span><select name=\"list_Metadata_{1}\" id=\"list_Metadata_{1}\" style=\"display:none;\">{2}</select>";
        string HtmlCellLAddLDAPFilter = "<span name=\"span_AddLDAPFilter_{1}\" id=\"span_AddLDAPFilter_{1}\">{0}</span><input name=\"input_AddLDAPFilter_{1}\" id=\"input_AddLDAPFilter_{1}\" style=\"display:none;\" value=\"{0}\"></input>";
        string HtmlCellKeywordToValidateInputWithoutLookup = "<span name=\"span_KeywordToValidateInputWithoutLookup_{1}\" id=\"span_KeywordToValidateInputWithoutLookup_{1}\">{0}</span><input name=\"input_KeywordToValidateInputWithoutLookup_{1}\" id=\"input_KeywordToValidateInputWithoutLookup_{1}\" style=\"display:none;\" value=\"{0}\"></input>";
        string HtmlCellPrefixToAddToValueReturned = "<span name=\"span_PrefixToAddToValueReturned_{1}\" id=\"span_PrefixToAddToValueReturned_{1}\">{0}</span><input name=\"input_PrefixToAddToValueReturned_{1}\" id=\"input_PrefixToAddToValueReturned_{1}\" style=\"display:none;\" value=\"{0}\"></input>";
        string HtmlCellClaimEntityType = "<span name=\"span_ClaimEntityType_{1}\" id=\"span_ClaimEntityType_{1}\">{0}</span><select name=\"list_ClaimEntityType_{1}\" id=\"list_ClaimEntityType_{1}\" style=\"display:none;\">{2}</select>";
        string HtmlCellShowClaimNameInDisplayText = "<input type=checkbox id=\"chk_ShowClaimNameInDisplayText_{1}\" name=\"chk_ShowClaimNameInDisplayText_{1}\" {0} disabled>";

        string HtmlEditLink = "<a name=\"editLink_{0}\" id=\"editLink_{0}\" href=\"javascript:Ldapcp.ClaimsTablePage.EditItem('{0}')\">Edit</a>";
        //string HtmlEditLink = "<a name=\"editLink_{0}\" id=\"editLink_{0}\" href=\"javascript:Ldapcp.ClaimsTablePage.EditItem('{0}')\"><div class='s4-clust ms-promotedActionButton-icon' style='width: 16px; height: 16px; overflow: hidden; display: inline-block; position: relative;'><img style='left: -236px; top: -84px; position: absolute;' alt='Edit' src='/_layouts/15/images/spcommon.png?rev=23'/></div></a>";
        string HtmlCancelEditLink = "<a name=\"cancelLink_{0}\" id=\"cancelLink_{0}\" href=\"javascript:Ldapcp.ClaimsTablePage.CancelEditItem('{0}')\" style=\"display:none;\">Cancel</a>";

        protected void Page_Load(object sender, EventArgs e)
        {
            if (ValidatePrerequisite() != ConfigStatus.AllGood && Status != ConfigStatus.NoIdentityClaimType)
            {
                this.LabelErrorMessage.Text = base.MostImportantError;
                this.HideAllContent = true;
                this.BtnCreateNewItem.Visible = false;
                return;
            }
            if (!this.IsPostBack) Initialize();
            BuildAttributesListTable(this.IsPostBack);
        }

        private void Initialize()
        {
            CurrentTrustedLoginProviderName = CurrentTrustedLoginProvider.Name;
            New_DdlPermissionMetadata.Items.Add(String.Empty);
            Type EntityDataKeysInfo = typeof(PeopleEditorEntityDataKeys);
            object[] fields = EntityDataKeysInfo.GetFields();
            foreach (object field in fields)
            {
                New_DdlPermissionMetadata.Items.Add(((FieldInfo)field).Name);
            }

            MemberInfo[] members = typeof(SPClaimEntityTypes).GetProperties(BindingFlags.Static | BindingFlags.Public);
            foreach (MemberInfo member in members.Where(x => x.Name == SPClaimEntityTypes.User || x.Name == SPClaimEntityTypes.FormsRole))
            {
                New_DdlClaimEntityType.Items.Add(member.Name);
            }
            New_DdlClaimEntityType.Items.FindByValue(SPClaimEntityTypes.User).Selected = true;
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
            TableRow tr = new TableRow();
            TableHeaderCell th;
            th = GetTableHeaderCell("Action");
            tr.Cells.Add(th);
            th = GetTableHeaderCell("Claim type");
            tr.Cells.Add(th);
            th = GetTableHeaderCell("LDAP Attribute");
            tr.Cells.Add(th);
            th = GetTableHeaderCell("LDAP class");
            tr.Cells.Add(th);
            th = GetTableHeaderCell("Attribute to display");
            tr.Cells.Add(th);
            th = GetTableHeaderCell("<a href='http://msdn.microsoft.com/en-us/library/office/microsoft.sharepoint.webcontrols.peopleeditorentitydatakeys_members(v=office.15).aspx' target='_blank'>Metadata</a>");
            tr.Cells.Add(th);
            th = GetTableHeaderCell("<a href='http://msdn.microsoft.com/en-us/library/office/microsoft.sharepoint.administration.claims.spclaimentitytypes_members(v=office.15).aspx' target='_blank'>Claim entity type</a>");
            tr.Cells.Add(th);
            th = GetTableHeaderCell("Additional LDAP filter");
            tr.Cells.Add(th);
            th = GetTableHeaderCell("Prefix to bypass lookup");
            tr.Cells.Add(th);
            th = GetTableHeaderCell("Prefix to add to value returned");
            tr.Cells.Add(th);
            th = GetTableHeaderCell("Show claim name in display text");
            tr.Cells.Add(th);
            this.TblClaimsMapping.Rows.Add(tr);

            foreach (var attr in this.ClaimsMapping)
            {
                tr = new TableRow();
                bool allowEditItem = String.IsNullOrEmpty(attr.Value.ClaimType) ? false : true;
                bool isIdentityClaimType = String.Equals(attr.Value.ClaimType, CurrentTrustedLoginProvider.IdentityClaimTypeInformation.MappedClaimType, StringComparison.InvariantCultureIgnoreCase) && !attr.Value.CreateAsIdentityClaim ? true : false;

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
                        else
                        {
                            tr.CssClass = "ldapcp-rowClaimTypeOk";
                        }
                    }
                    else
                    {
                        c = GetTableCell(attr.Value.CreateAsIdentityClaim ? "linked to identity claim" : "Used as metadata for the permission created");
                    }
                    tr.Cells.Add(c);

                    html = String.Format(HtmlCellLAttrName, attr.Value.LDAPAttribute, attr.Key);
                    tr.Cells.Add(GetTableCell(html));

                    html = String.Format(HtmlCellLAttrClass, attr.Value.LDAPClass, attr.Key);
                    tr.Cells.Add(GetTableCell(html));

                    html = String.Format(HtmlCellLDAPAttrToDisplay, attr.Value.LDAPAttributeToShowAsDisplayText, attr.Key);
                    tr.Cells.Add(GetTableCell(html));

                    MemberInfo[] members;
                    members = typeof(PeopleEditorEntityDataKeys).GetFields(BindingFlags.Static | BindingFlags.Public);
                    html = BuildDDLFromTypeMembers(HtmlCellMetadata, attr, "EntityDataKey", members, true);
                    tr.Cells.Add(GetTableCell(html));

                    members = typeof(SPClaimEntityTypes).GetProperties(BindingFlags.Static | BindingFlags.Public);
                    html = BuildDDLFromTypeMembers(HtmlCellClaimEntityType, attr, "ClaimEntityType", members, false);
                    tr.Cells.Add(GetTableCell(html));

                    html = String.Format(HtmlCellLAddLDAPFilter, attr.Value.AdditionalLDAPFilter, attr.Key);
                    tr.Cells.Add(GetTableCell(html));

                    html = String.Format(HtmlCellKeywordToValidateInputWithoutLookup, attr.Value.PrefixToBypassLookup, attr.Key);
                    tr.Cells.Add(GetTableCell(html));

                    html = String.Format(HtmlCellPrefixToAddToValueReturned, attr.Value.ClaimValuePrefix, attr.Key);
                    tr.Cells.Add(GetTableCell(html));

                    if (isIdentityClaimType || !allowEditItem) html = String.Empty;
                    else
                    {
                        string strChecked = attr.Value.ShowClaimNameInDisplayText ? "checked" : String.Empty;
                        html = String.Format(HtmlCellShowClaimNameInDisplayText, strChecked, attr.Key);
                    }
                    tr.Cells.Add(GetTableCell(html));
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

        protected override bool UpdatePersistedObjectProperties(bool commitChanges)
        {
            throw new NotImplementedException();
        }

        void LnkDeleteItem_Command(object sender, CommandEventArgs e)
        {
            if (ValidatePrerequisite() != ConfigStatus.AllGood && Status != ConfigStatus.NoIdentityClaimType) return;

            string itemId = e.CommandArgument.ToString();
            ClaimTypeConfig attr = ClaimsMapping.Find(x => x.Key == Convert.ToInt32(itemId)).Value;
            PersistedObject.ClaimTypes.Remove(attr);
            CommitChanges();
            this.BuildAttributesListTable(false);
        }

        void LnkUpdateItem_Command(object sender, CommandEventArgs e)
        {
            if (ValidatePrerequisite() != ConfigStatus.AllGood && Status != ConfigStatus.NoIdentityClaimType) return;

            string itemId = e.CommandArgument.ToString();
            NameValueCollection formData = Request.Form;
            int attrObjectId = Convert.ToInt32(itemId);
            ClaimTypeConfig attr = ClaimsMapping.Find(x => x.Key == attrObjectId).Value;

            // Check if changes are OK
            string newClaimType = formData["input_claimtype_" + itemId].Trim();
            string newLDAPAttribute = formData["input_attrname_" + itemId].Trim();
            string newLDAPClass = formData["input_attrclass_" + itemId].Trim();
            string newClaimEntityType = formData["list_ClaimEntityType_" + itemId];
            string newEntityDataKey = formData["list_Metadata_" + itemId];
            string newShowClaimNameInDisplayText = formData["chk_ShowClaimNameInDisplayText_" + itemId];

            // Check if new LDAP attribute/class/claimtype are empty
            if (String.IsNullOrEmpty(newClaimType) || String.IsNullOrEmpty(newLDAPAttribute) || String.IsNullOrEmpty(newLDAPClass))
            {
                this.LabelErrorMessage.Text = TextErrorFieldsMissing;
                BuildAttributesListTable(false);
                return;
            }

            // Check if claim type is not already used
            List<KeyValuePair<int, ClaimTypeConfig>> otherObjects = ClaimsMapping.FindAll(x => x.Key != attrObjectId);
            KeyValuePair<int, ClaimTypeConfig> matchFound;
            matchFound = otherObjects.FirstOrDefault(x => String.Equals(x.Value.ClaimType, newClaimType, StringComparison.InvariantCultureIgnoreCase));

            // Check if new claim type is not already used
            if (!matchFound.Equals(default(KeyValuePair<int, ClaimTypeConfig>)))
            {
                this.LabelErrorMessage.Text = String.Format(TextErrorUpdateItemDuplicate, attr.ClaimType, "claim type", newClaimType);
                BuildAttributesListTable(false);
                return;
            }

            // Check if new entity data key is not already used on the new claim entity type (we don't care about this check if it's empty)
            if (newEntityDataKey != String.Empty)
            {
                matchFound = otherObjects.FirstOrDefault(x =>
                    String.Equals(x.Value.EntityDataKey, newEntityDataKey, StringComparison.InvariantCultureIgnoreCase) &&
                    String.Equals(x.Value.ClaimEntityType, newClaimEntityType, StringComparison.InvariantCultureIgnoreCase));
                if (!matchFound.Equals(default(KeyValuePair<int, ClaimTypeConfig>)))
                {
                    this.LabelErrorMessage.Text = String.Format(TextErrorUpdateItemDuplicate, attr.ClaimType, "permission metadata", newEntityDataKey);
                    BuildAttributesListTable(false);
                    return;
                }
            }

            // Specific checks if current claim type is identity claim type
            if (String.Equals(attr.ClaimType, CurrentTrustedLoginProvider.IdentityClaimTypeInformation.MappedClaimType, StringComparison.InvariantCultureIgnoreCase))
            {
                // We don't allow to change claim type
                if (!String.Equals(attr.ClaimType, newClaimType, StringComparison.InvariantCultureIgnoreCase))
                {
                    this.LabelErrorMessage.Text = TextErrorUpdateIdentityClaimTypeChanged;
                    BuildAttributesListTable(false);
                    return;
                }

                // ClaimEntityType must be "SPClaimEntityTypes.User"
                if (!String.Equals(SPClaimEntityTypes.User, newClaimEntityType, StringComparison.InvariantCultureIgnoreCase))
                {
                    this.LabelErrorMessage.Text = TextErrorIdentityClaimTypeNotUser;
                    BuildAttributesListTable(false);
                    return;
                }
            }

            // Check if new LDAP attribute/class are not already used or empty
            matchFound = otherObjects.FirstOrDefault(x =>
                String.Equals(x.Value.LDAPAttribute, newLDAPAttribute, StringComparison.InvariantCultureIgnoreCase) &&
                String.Equals(x.Value.LDAPClass, newLDAPClass, StringComparison.InvariantCultureIgnoreCase));
            if (!matchFound.Equals(default(KeyValuePair<int, ClaimTypeConfig>)))
            {
                this.LabelErrorMessage.Text = TextErrorDuplicateLdapAttrAndClass;
                BuildAttributesListTable(false);
                return;
            }

            attr.ClaimType = newClaimType;
            attr.LDAPAttribute = newLDAPAttribute;
            attr.LDAPClass = newLDAPClass;
            attr.LDAPAttributeToShowAsDisplayText = formData["input_LDAPAttrToDisplay_" + itemId];
            attr.EntityDataKey = newEntityDataKey;
            attr.ClaimEntityType = newClaimEntityType;
            attr.AdditionalLDAPFilter = formData["input_AddLDAPFilter_" + itemId];
            attr.PrefixToBypassLookup = formData["input_KeywordToValidateInputWithoutLookup_" + itemId];
            attr.ClaimValuePrefix = formData["input_PrefixToAddToValueReturned_" + itemId].ToLower();
            attr.ShowClaimNameInDisplayText = String.IsNullOrEmpty(newShowClaimNameInDisplayText) ? false : true;

            CommitChanges();
            this.BuildAttributesListTable(false);
        }

        protected void BtnReset_Click(object sender, EventArgs e)
        {
            PersistedObject.ResetClaimTypesList();
            PersistedObject.Update();
            Response.Redirect(Request.Url.ToString());
        }

        protected void BtnCreateNewItem_Click(object sender, EventArgs e)
        {
            string newClaimType = TxtNewClaimType.Text.Trim();
            string newLdapAttribute = TxtNewAttrName.Text.Trim();
            string newLdapClass = TxtNewObjectClass.Text.Trim();
            string newClaimEntityType = New_DdlClaimEntityType.SelectedValue;
            string newPermissionMetadata = New_DdlPermissionMetadata.SelectedValue;
            bool newCreateAsIdentityClaim = false;
            if (String.IsNullOrEmpty(newLdapAttribute) || String.IsNullOrEmpty(newLdapClass))
            {
                this.LabelErrorMessage.Text = TextErrorFieldsMissing;
                ShowNewItemForm = true;
                BuildAttributesListTable(false);
                return;
            }

            if (RdbNewItemClassicClaimType.Checked)
            {
                if (String.IsNullOrEmpty(newClaimType))
                {
                    this.LabelErrorMessage.Text = TextErrorFieldsMissing;
                    ShowNewItemForm = true;
                    BuildAttributesListTable(false);
                    return;
                }

                if (PersistedObject.ClaimTypes.Where(x => String.Equals(newClaimType, x.ClaimType, StringComparison.InvariantCultureIgnoreCase)) != null)
                {
                    this.LabelErrorMessage.Text = TextErrorDuplicateClaimType;
                    ShowNewItemForm = true;
                    BuildAttributesListTable(false);
                    return;
                }

                // Check if new claim type matches identity claim, and if so ensure that ClaimEntityType is User
                if (String.Equals(newClaimType, CurrentTrustedLoginProvider.IdentityClaimTypeInformation.MappedClaimType, StringComparison.InvariantCultureIgnoreCase) &&
                    !String.Equals(newClaimEntityType, SPClaimEntityTypes.User, StringComparison.InvariantCultureIgnoreCase))
                {
                    this.LabelErrorMessage.Text = TextErrorIdentityClaimTypeNotUser;
                    ShowNewItemForm = true;
                    BuildAttributesListTable(false);
                    return;
                }
            }
            else if (RdbNewItemPermissionMetadata.Checked)
            {
                if (String.IsNullOrEmpty(newPermissionMetadata))
                {
                    this.LabelErrorMessage.Text = TextErrorFieldsMissing;
                    ShowNewItemForm = true;
                    BuildAttributesListTable(false);
                    return;
                }
            }
            else
            {
                newCreateAsIdentityClaim = true;
                //CP newClaimEntityType = SPClaimEntityTypes.User;
                newClaimEntityType = New_DdlClaimEntityType.SelectedValue;
            }

            // Check if metadata is not already used for the specified claim entity type
            if (!String.IsNullOrEmpty(newPermissionMetadata) &&
                !ClaimsMapping.FirstOrDefault(x =>
                    String.Equals(x.Value.EntityDataKey, newPermissionMetadata, StringComparison.InvariantCultureIgnoreCase) &&
                    // Change condition to fix bug http://ldapcp.codeplex.com/discussions/653087
                    // We don't care about the claim entity type, it must be unique based on the LDAP class
                    //String.Equals(x.Value.ClaimEntityType, newClaimEntityType, StringComparison.InvariantCultureIgnoreCase)).
                    String.Equals(x.Value.LDAPClass, newLdapClass, StringComparison.InvariantCultureIgnoreCase)).
                Equals(default(KeyValuePair<int, ClaimTypeConfig>)))
            {
                this.LabelErrorMessage.Text = String.Format(TextErrorNewMetadataAlreadyUsed, newPermissionMetadata, newClaimEntityType);
                ShowNewItemForm = true;
                BuildAttributesListTable(false);
                return;
            }

            // Check if same LDAP attribute/class is not already used
            //FINDTOWHERE
            if (PersistedObject.ClaimTypes.Where(x =>
                String.Equals(newLdapAttribute, x.LDAPAttribute, StringComparison.InvariantCultureIgnoreCase) &&
                String.Equals(newLdapClass, x.LDAPClass, StringComparison.InvariantCultureIgnoreCase)) != null)
            {
                this.LabelErrorMessage.Text = TextErrorDuplicateLdapAttrAndClass;
                ShowNewItemForm = true;
                BuildAttributesListTable(false);
                return;
            }

            ClaimTypeConfig attr = new ClaimTypeConfig();
            attr.CreateAsIdentityClaim = newCreateAsIdentityClaim;
            attr.ClaimType = newClaimType;
            attr.LDAPAttribute = newLdapAttribute;
            attr.LDAPClass = newLdapClass;
            attr.ClaimEntityType = newClaimEntityType;
            attr.EntityDataKey = newPermissionMetadata;

            PersistedObject.ClaimTypes.Add(attr);
            CommitChanges();
            BuildAttributesListTable(false);
        }
    }
}
