<%@ Assembly Name="$SharePoint.Project.AssemblyFullName$" %>
<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="GlobalSettings.ascx.cs" Inherits="Yvand.LdapClaimsProvider.Administration.GlobalSettingsUserControl" %>
<%@ Register TagPrefix="Utilities" Namespace="Microsoft.SharePoint.Utilities" Assembly="Microsoft.SharePoint, Version=15.0.0.0, Culture=neutral, PublicKeyToken=71e9bce111e9429c" %>
<%@ Register TagPrefix="asp" Namespace="System.Web.UI" Assembly="System.Web.Extensions, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35" %>
<%@ Register TagPrefix="wssuc" TagName="InputFormSection" Src="~/_controltemplates/InputFormSection.ascx" %>
<%@ Register TagPrefix="wssuc" TagName="InputFormControl" Src="~/_controltemplates/InputFormControl.ascx" %>
<%@ Register TagPrefix="wssuc" TagName="ButtonSection" Src="~/_controltemplates/ButtonSection.ascx" %>
<%@ Register TagPrefix="wssawc" Namespace="Microsoft.SharePoint.WebControls" Assembly="Microsoft.SharePoint, Version=15.0.0.0, Culture=neutral, PublicKeyToken=71e9bce111e9429c" %>

<script type="text/javascript" src="/_layouts/15/ldapcpse/jquery-1.9.1.min.js"></script>
<style>
    /* Maximaze space available for description text */
    .ms-inputformdescription {
        width: 100%;
    }

    /* corev15.css set it to 0.9em, which makes it too small */
    .ms-descriptiontext {
        font-size: 1em;
    }

    /* Set the size of the right part with all input controls */
    .ms-inputformcontrols {
        width: 750px;
    }

    /* Set the display of the title of each section */
    .ms-standardheader {
        color: #0072c6;
        font-weight: bold;
        font-size: 1.15em;
    }

    /* Only used in td elements in grid view that displays LDAP connections */
    .ms-vb2 {
        vertical-align: middle;
    }

    .ldapcp-success {
        color: green;
        font-weight: bold;
    }

    .ldapcp-HideCol {
        display: none;
    }

    .divfieldset label {
        display: inline-block;
        line-height: 1.8;
        width: 200px;
    }

    .divfieldset em {
        font-weight: bold;
        font-style: normal;
        color: #f00;
    }

    fieldset {
        border: 1px lightgray solid;
        margin: 0;
        padding: 0;
    }

        fieldset ul {
            margin: 0;
            padding: 0;
        }

        fieldset li {
            list-style: none;
            padding: 5px;
            margin: 0;
        }
</style>

<script type="text/javascript">
    // IE does not support string.includes, so it is implemented here - https://stackoverflow.com/questions/31221341/ie-does-not-support-array-includes-or-string-includes-methods
    if (!String.prototype.includes) {
        String.prototype.includes = function (search, start) {
            'use strict';
            if (typeof start !== 'number') {
                start = 0;
            }

            if (start + search.length > this.length) {
                return false;
            } else {
                return this.indexOf(search, start) !== -1;
            }
        };
    }

    // Builds unique namespace
    window.Ldapcp = window.Ldapcp || {};
    window.Ldapcp.AdminGlobalSettingsControl = window.Ldapcp.AdminGlobalSettingsControl || {
        dynamicTokens: [],

        Init: function () {
            // Initialize dynamic tokens
            /* // for loop below does not work in IE:
            var dynamicTokens = {};
            dynamicTokens["{domain}"] = "contoso";
            dynamicTokens["{fqdn}"] = "contoso.local";		
            for (const [key, value] of Object.entries(dynamicTokens)) {
              console.log(key, value);
            }*/
            this.dynamicTokens.push({
                key: "{domain}",
                value: "contoso"
            });
            this.dynamicTokens.push({
                key: "{fqdn}",
                value: "contoso.local"
            });

            // Add event handlers to preview the permission's value for both entity types, based on current settings
            // users
            $('#<%= TxtUserIdLdapAttribute.ClientID %>').on('input', function () {
                window.Ldapcp.AdminGlobalSettingsControl.UpdatePermissionValuePreview("<%= TxtUserIdLdapAttribute.ClientID %>", "<%= TxtUserIdLeadingToken.ClientID %>", "lblUserPermissionValuePreview");
            });
            $('#<%= TxtUserIdLeadingToken.ClientID %>').on('input', function () {
                window.Ldapcp.AdminGlobalSettingsControl.UpdatePermissionValuePreview("<%= TxtUserIdLdapAttribute.ClientID %>", "<%= TxtUserIdLeadingToken.ClientID %>", "lblUserPermissionValuePreview");
            });
            // groups
            $('#<%= TxtGroupLdapAttribute.ClientID %>').on('input', function () {
                window.Ldapcp.AdminGlobalSettingsControl.UpdatePermissionValuePreview("<%= TxtGroupLdapAttribute.ClientID %>", "<%= TxtGroupLeadingToken.ClientID %>", "lblGroupPermissionValuePreview");
            });
            $('#<%= TxtGroupLeadingToken.ClientID %>').on('input', function () {
                window.Ldapcp.AdminGlobalSettingsControl.UpdatePermissionValuePreview("<%= TxtGroupLdapAttribute.ClientID %>", "<%= TxtGroupLeadingToken.ClientID %>", "lblGroupPermissionValuePreview");
            });

            this.InitLdapControls();
            this.InitAugmentationControls();
            this.UpdatePermissionValuePreview("<%= TxtUserIdLdapAttribute.ClientID %>", "<%= TxtUserIdLeadingToken.ClientID %>", "lblUserPermissionValuePreview");
            this.UpdatePermissionValuePreview("<%= TxtGroupLdapAttribute.ClientID %>", "<%= TxtGroupLeadingToken.ClientID %>", "lblGroupPermissionValuePreview");
        },

        InitLdapControls: function () {
            // Variables initialized from server side code
            IsDefaultADConnectionCreated = <%= ViewState["IsDefaultADConnectionCreated"].ToString().ToLower() %>;
            ForceCheckCustomLdapConnection = <%= ViewState["ForceCheckCustomLdapConnection"].ToString().ToLower() %>;

            if (IsDefaultADConnectionCreated) {
                // Disable radio button to create default connection and select other one.
                $('#<%= RbUseServerDomain.ClientID %>').prop('disabled', true);
                $('#<%= RbUseCustomConnection.ClientID %>').prop('checked', true);
                // Needed to trigger the click to display the button that tests connection
                $('#<%= RbUseCustomConnection.ClientID %>').trigger('click');
            }
            else {
                // No default connection, give possibility to create one.
                $('#<%= RbUseServerDomain.ClientID %>').prop('enabled', true);
                $('#<%= RbUseServerDomain.ClientID %>').prop('checked', true);
                // Hide asterisk in custom LDAP connection fields
                $('#divNewLdapConnection').find('em').hide();
            }

            if (ForceCheckCustomLdapConnection) {
                $('#<%= RbUseCustomConnection.ClientID %>').prop('checked', true);
                // Needed to trigger the click to display the button that tests connection
                $('#<%= RbUseCustomConnection.ClientID %>').trigger('click');
            }
        },

        // Enable or disable controls for augmentation section
        InitAugmentationControls: function () {
            var enableAugmentationControls = $('#<%= ChkEnableAugmentation.ClientID %>').is(":checked");
            var nodes = document.getElementById("AugmentationControlsGrid").getElementsByTagName('*');
            if (enableAugmentationControls) {
                for (var i = 0; i < nodes.length; i++) {
                    nodes[i].disabled = false;
                }
            }
            else {
                for (var i = 0; i < nodes.length; i++) {
                    nodes[i].disabled = true;
                }
            }
        },

        // New LDAP connection section
        CheckCustomLdapRB: function () {
            var CbUserQuota = (document.getElementById("<%= RbUseCustomConnection.ClientID %>"));
            if (CbUserQuota != null) {
                CbUserQuota.checked = true;
            }
            $('#<%= BtnTestLdapConnection.ClientID %>').show('fast');
            $('#divNewLdapConnection').find('em').show();
        },

        CheckDefaultADConnection: function () {
            $('#divNewLdapConnection').find('em').hide();
            $('#<%= BtnTestLdapConnection.ClientID %>').hide('fast');
        },

        //<%--// Identity permission section
        //CheckRbIdentityCustomLDAP: function () {
            //var control = (document.getElementById("<%= RbUserIdDisplayValueCustom.ClientID %>"));
            //if (control != null) {
                //control.checked = true;
            //}
        //},--%>

        // Preview the permission's value, based on given entity type's settings
        UpdatePermissionValuePreview: function (inputIdentifierAttributeId, inputTokenAttributeId, lblResultId) {
            // Get leading token value TxtGroupLeadingToken
            var leadingTokenInput = $("#" + inputTokenAttributeId).val();

            // Determine the actual leading token value
            var leadingTokenValue = leadingTokenInput;
            var localDynamicTokens = this.dynamicTokens;
            Object.keys(localDynamicTokens).forEach(function (keyIndex) {
                keyValue = localDynamicTokens[keyIndex].key
                if (leadingTokenInput.includes(keyValue)) {
                    leadingTokenValue = leadingTokenInput.replace(keyValue, localDynamicTokens[keyIndex].value);
                }
            });

            // Get the TxtGroupLdapAttribute value
            var entityPermissionValue = $("#" + inputIdentifierAttributeId).val();

            // Set the label control to preview a group's value
            var entityPermissionValuePreview = leadingTokenValue + "<" + entityPermissionValue + "_from_ldap>";
            $("#" + lblResultId).text(entityPermissionValuePreview);
        }
    };

    _spBodyOnLoadFunctionNames.push("window.Ldapcp.AdminGlobalSettingsControl.Init");
</script>

<table width="100%" class="propertysheet" cellspacing="0" cellpadding="0" border="0">
    <tr>
        <td class="ms-descriptionText">
            <asp:Label ID="LabelMessage" runat="server" EnableViewState="False" class="ms-descriptionText" />
        </td>
    </tr>
    <tr>
        <td class="ms-error">
            <asp:Label ID="LabelErrorMessage" runat="server" EnableViewState="False" />
        </td>
    </tr>
    <tr>
        <td class="ms-descriptionText">
            <asp:ValidationSummary ID="ValSummary" HeaderText="<%$SPHtmlEncodedResources:spadmin, ValidationSummaryHeaderText%>"
                DisplayMode="BulletList" ShowSummary="True" runat="server"></asp:ValidationSummary>
        </td>
    </tr>
</table>
<table border="0" cellspacing="0" cellpadding="0" width="100%">
    <wssuc:ButtonSection runat="server">
        <Template_Buttons>
            <asp:Button UseSubmitBehavior="false" runat="server" class="ms-ButtonHeightWidth" OnClick="BtnOK_Click" Text="<%$Resources:wss,multipages_okbutton_text%>" ID="BtnOKTop" AccessKey="<%$Resources:wss,okbutton_accesskey%>" />
        </Template_Buttons>
    </wssuc:ButtonSection>

    <wssuc:InputFormSection ID="CurrentLdapConnectionSection" Title="Registered LDAP connections" runat="server">
        <Template_Description>
            <wssawc:EncodedLiteral runat="server" Text="LDAP connections currently registered in LDAPCP configuration." EncodeMethod='HtmlEncodeAllowSimpleTextFormatting' />
        </Template_Description>
        <Template_InputFormControls>
            <tr>
                <td>
                    <wssawc:SPGridView runat="server" ID="grdLDAPConnections" AutoGenerateColumns="false" OnRowDeleting="grdLDAPConnections_RowDeleting">
                        <Columns>
                            <asp:BoundField DataField="Id" ItemStyle-CssClass="ldapcp-HideCol" HeaderStyle-CssClass="ldapcp-HideCol" />
                            <asp:BoundField HeaderText="LDAP Path" DataField="Path" />
                            <asp:BoundField HeaderText="Username" DataField="Username" />
                            <asp:CommandField HeaderText="Action" ButtonType="Button" DeleteText="Remove" ShowDeleteButton="True" />
                        </Columns>
                    </wssawc:SPGridView>
                </td>
            </tr>
        </Template_InputFormControls>
    </wssuc:InputFormSection>

    <wssuc:InputFormSection ID="NewLdapConnectionSection" Title="Register a new LDAP connection" runat="server">
        <Template_Description>
            <wssawc:EncodedLiteral runat="server" Text="By default, LDAPCP connects to the Active Directory domain of the SharePoint servers using the application pool identity. This connection is labelled 'Connect to SharePoint domain'." EncodeMethod='HtmlEncodeAllowSimpleTextFormatting' />
        </Template_Description>
        <Template_InputFormControls>
            <tr>
                <td>
                    <table>
                        <wssawc:InputFormRadioButton ID="RbUseServerDomain"
                            LabelText="Connect to SharePoint AD domain"
                            Checked="true"
                            GroupName="RbLDAPConnection"
                            CausesValidation="false"
                            runat="server"
                            onclick="window.Ldapcp.AdminGlobalSettingsControl.CheckDefaultADConnection()">
                            <wssawc:EncodedLiteral runat="server" Text="Connect to same AD as SharePoint servers, with application pool credentials." EncodeMethod='HtmlEncode' />
                        </wssawc:InputFormRadioButton>
                        <wssawc:InputFormRadioButton ID="RbUseCustomConnection"
                            LabelText="Connect to a LDAP server"
                            GroupName="RbLDAPConnection"
                            CausesValidation="false"
                            onclick="window.Ldapcp.AdminGlobalSettingsControl.CheckCustomLdapRB()"
                            runat="server">
                            <div id="divNewLdapConnection" class="divfieldset">
                                <fieldset>
                                    <legend>Settings for new LDAP connection</legend>
                                    <ol>
                                        <li>
                                            <label for="<%= TxtLdapConnectionString.ClientID %>">LDAP <a href="https://learn.microsoft.com/en-us/dotnet/api/system.directoryservices.directoryentry.path?view=netframework-4.8.1" target="_blank">path</a> <em>*</em></label>
                                            <wssawc:InputFormTextBox onclick="window.Ldapcp.AdminGlobalSettingsControl.CheckCustomLdapRB()" title="LDAP connection string" class="ms-input" ID="TxtLdapConnectionString" Columns="50" runat="server" MaxLength="255" Text="LDAP://" />
                                        </li>
                                        <li>
                                            <label for="<%= TxtLdapUsername.ClientID %>">Username <em>*</em></label>
                                            <wssawc:InputFormTextBox onclick="window.Ldapcp.AdminGlobalSettingsControl.CheckCustomLdapRB()" title="Username" class="ms-input" ID="TxtLdapUsername" Columns="50" runat="server" MaxLength="255" />
                                        </li>
                                        <li>
                                            <label for="<%= TxtLdapPassword.ClientID %>">Password <em>*</em></label>
                                            <wssawc:InputFormTextBox onclick="window.Ldapcp.AdminGlobalSettingsControl.CheckCustomLdapRB()" title="Password" class="ms-input" ID="TxtLdapPassword" Columns="50" runat="server" MaxLength="255" TextMode="Password" />
                                        </li>
                                        <li>
                                            <div class="text-nowrap">Select the <a href="https://learn.microsoft.com/en-us/dotnet/api/system.directoryservices.authenticationtypes?view=netframework-4.8.1" target="_blank">authentication type</a> to use (optional):</div>
                                            <wssawc:InputFormCheckBoxList
                                                ID="CblAuthenticationTypes"
                                                runat="server"
                                                onclick="window.Ldapcp.AdminGlobalSettingsControl.CheckCustomLdapRB()"
                                                RepeatDirection="Horizontal" RepeatColumns="3" />
                                        </li>
                                    </ol>
                                </fieldset>
                            </div>
                        </wssawc:InputFormRadioButton>
                    </table>
                    <div class="divbuttons">
                        <asp:Button runat="server" ID="BtnTestLdapConnection" Text="Test LDAP Connection" OnClick="BtnTestLdapConnection_Click" class="ms-ButtonHeightWidth" Style="display: none;" />
                        <asp:Button runat="server" ID="BtnAddLdapConnection" Text="Add LDAP Connection" OnClick="BtnAddLdapConnection_Click" class="ms-ButtonHeightWidth" />
                    </div>
                    <p>
                        <asp:Label ID="LabelErrorTestLdapConnection" runat="server" EnableViewState="False" class="ms-error" />
                        <asp:Label ID="LabelTestLdapConnectionOK" runat="server" EnableViewState="False" />
                    </p>
                </td>
            </tr>
        </Template_InputFormControls>
    </wssuc:InputFormSection>

    <wssuc:InputFormSection runat="server" Title="Configuration for the user identifier claim type">
        <Template_Description>
            <sharepoint:encodedliteral runat="server" text="Specify the settings to search, create and display the permissions for users." encodemethod='HtmlEncodeAllowSimpleTextFormatting' />
            <br />
            <br />
            <sharepoint:encodedliteral runat="server" text="Preview of a user permission's encoded value returned by LDAPCP, based on current settings:" encodemethod='HtmlEncodeAllowSimpleTextFormatting' />
            <br />
            <b><span><%= UserIdentifierEncodedValuePrefix %><span id="lblUserPermissionValuePreview"></span></span></b>
            <br />
            <br />
            <sharepoint:encodedliteral runat="server" text="- &quot;Leading token&quot;: Specify a static or dynnamic token to add to the permission's value. Possible dynamic tokens are &quot;{domain}&quot; and &quot;{fqdn}&quot;" encodemethod='HtmlEncodeAllowSimpleTextFormatting' />
            <br />
            <br />
            <sharepoint:encodedliteral runat="server" text="- &quot;Additional LDAP filter&quot;: Specify a custom LDAP filter to restrict the users that may be returned. Be mindful that an invalid filter may break the LDAP requests." encodemethod='HtmlEncodeAllowSimpleTextFormatting' />
        </Template_Description>
        <Template_InputFormControls>
            <tr>
                <td colspan="2">
                    <div class="divfieldset">
                        <fieldset>
                            <legend>Settings that uniquely identify a user</legend>
                            <ol>
                                <li>
                                    <label>Claim type</label>
                                    <label>
                                        <wssawc:EncodedLiteral runat="server" ID="lblUserIdClaimType" EncodeMethod='HtmlEncodeAllowSimpleTextFormatting' /></label>
                                </li>
                                <li>
                                    <label for="<%= TxtUserIdLdapClass.ClientID %>">LDAP object class <em>*</em></label>
                                    <wssawc:InputFormTextBox title="LDAP object class" class="ms-input" ID="TxtUserIdLdapClass" Columns="50" runat="server" MaxLength="255" />
                                </li>
                                <li>
                                    <label for="<%= TxtUserIdLdapAttribute.ClientID %>">LDAP object attribute <em>*</em></label>
                                    <wssawc:InputFormTextBox title="LDAP object attribute" class="ms-input" ID="TxtUserIdLdapAttribute" Columns="50" runat="server" MaxLength="255" />
                                </li>
                            </ol>
                        </fieldset>
                        <fieldset>
                            <legend>Additional settings</legend>
                            <ol>
                                <%--<li>
                                    <label class="text-nowrap" style="display: inline;">LDAP object attribute used as display text in the people picker</label>
                                    <table style="white-space: nowrap;">
                                        <wssawc:InputFormRadioButton ID="RbUserIdDisplayValueDefault"
                                            LabelText="Same as the LDAP object attribute"
                                            Checked="true"
                                            GroupName="RbUserIdDisplayValue"
                                            CausesValidation="false"
                                            runat="server">
                                        </wssawc:InputFormRadioButton>
                                        <wssawc:InputFormRadioButton ID="RbUserIdDisplayValueCustom"
                                            LabelText="Show a different LDAP attribute:"
                                            GroupName="RbUserIdDisplayValue"
                                            CausesValidation="false"
                                            runat="server">
                                            <asp:TextBox runat="server" ID="TxtUserIdDisplayValueCustom" title="LDAP object attribute as display text" class="ms-input" Columns="50" MaxLength="255" onclick="window.Ldapcp.AdminGlobalSettingsControl.CheckRbIdentityCustomLDAP()" />
                                        </wssawc:InputFormRadioButton>
                                    </table>
                                </li>--%>
                                <li>
                                    <label for="<%= TxtUserIdDisplayTextAttribute.ClientID %>" title="Attribute displayed in the results list in the people picker (leave blank to use the user identifier attribute)">LDAP attribute as display text &#9432;</label>
                                    <wssawc:InputFormTextBox title="" class="ms-input" ID="TxtUserIdDisplayTextAttribute" Columns="50" runat="server" MaxLength="255" />
                                </li>
                                <li>
                                    <label for="<%= TxtUserIdAdditionalLdapAttributes.ClientID %>" title="LDAP attributes added to the LDAP query when searching users in the people picker (comma-separated values)">Additional search attributes &#9432;</label>
                                    <wssawc:InputFormTextBox title="" class="ms-input" ID="TxtUserIdAdditionalLdapAttributes" Columns="50" runat="server" MaxLength="255" />
                                </li>
                                <li>
                                    <label for="<%= TxtUserIdLeadingToken.ClientID %>" title="Static or dynnamic token to add to the permission's value">Leading token in claim value &#9432;</label>
                                    <wssawc:InputFormTextBox title="" class="ms-input" ID="TxtUserIdLeadingToken" Columns="50" runat="server" MaxLength="255" />
                                </li>
                                <li>
                                    <label for="<%= TxtUserIdAdditionalLdapFilter.ClientID %>" title="Additional LDAP filter applied to all the user attributes">Additional LDAP filter &#9432;</label>
                                    <wssawc:InputFormTextBox title="" class="ms-input" ID="TxtUserIdAdditionalLdapFilter" Columns="50" runat="server" MaxLength="255" />
                                </li>
                            </ol>
                        </fieldset>

                    </div>
                </td>
            </tr>
        </Template_InputFormControls>
    </wssuc:InputFormSection>

    <wssuc:InputFormSection ID="AugmentationSection" runat="server" Title="Configuration for the group claim type">
        <Template_Description>
            <sharepoint:encodedliteral runat="server" text="Specify the settings to search, create and display the permissions for groups." encodemethod='HtmlEncodeAllowSimpleTextFormatting' />
            <br />
            <br />
            <sharepoint:encodedliteral runat="server" text="Preview of a group permission's encoded value returned by LDAPCP, based on current settings:" encodemethod='HtmlEncodeAllowSimpleTextFormatting' />
            <br />
            <b><span><%= GroupIdentifierEncodedValuePrefix %><span id="lblGroupPermissionValuePreview"></span></span></b>
            <br />
            <br />
            <sharepoint:encodedliteral runat="server" text="- &quot;Leading token&quot;: Specify a static or dynnamic token to add to the permission's value. Possible dynamic tokens are &quot;{domain}&quot; and &quot;{fqdn}&quot;" encodemethod='HtmlEncodeAllowSimpleTextFormatting' />
            <br />
            <br />
            <sharepoint:encodedliteral runat="server" text="- &quot;Additional LDAP filter&quot;: Specify a custom LDAP filter to restrict the groups that may be returned. Be mindful that an invalid filter may break the LDAP requests." encodemethod='HtmlEncodeAllowSimpleTextFormatting' />
        </Template_Description>
        <Template_InputFormControls>
            <p class="ms-error">
                <asp:Label ID="Label1" runat="server" EnableViewState="False" />
            </p>
            <div id="divGroupClaimTypeConfiguration">
                <tr>
                    <td>
                        <div class="divfieldset">
                            <fieldset>
                                <legend>Settings that uniquely identify a group</legend>
                                <ol>
                                    <li>
                                        <label title="This liste is based on the claim types registered in your SharePoint trust">
                                            <wssawc:EncodedLiteral runat="server" Text="Claim type &#9432;" EncodeMethod='HtmlEncodeAllowSimpleTextFormatting' /><em>*</em></label>
                                        <asp:DropDownList ID="DdlGroupClaimType" runat="server">
                                            <asp:ListItem Selected="True" Value="None"></asp:ListItem>
                                        </asp:DropDownList>
                                    </li>
                                    <li>
                                        <label for="<%= TxtGroupLdapClass.ClientID %>">LDAP object class <em>*</em></label>
                                        <wssawc:InputFormTextBox title="" class="ms-input" ID="TxtGroupLdapClass" Columns="50" runat="server" MaxLength="255" />
                                    </li>
                                    <li>
                                        <label for="<%= TxtGroupLdapAttribute.ClientID %>">LDAP object attribute <em>*</em></label>
                                        <wssawc:InputFormTextBox title="" class="ms-input" ID="TxtGroupLdapAttribute" Columns="50" runat="server" MaxLength="255" />
                                    </li>
                                </ol>
                            </fieldset>
                            <fieldset>
                                <legend>Additional settings</legend>
                                <ol>
                                    <li>
                                        <label for="<%= TxtGroupDisplayTextAttribute.ClientID %>" title="Attribute displayed in the results list in the people picker (leave blank to use the group identifier attribute)">LDAP attribute as display text &#9432;</label>
                                        <wssawc:InputFormTextBox title="" class="ms-input" ID="TxtGroupDisplayTextAttribute" Columns="50" runat="server" MaxLength="255" />
                                    </li>
                                    <li>
                                        <label for="<%= TxtGroupAdditionalLdapAttributes.ClientID %>" title="LDAP attributes added to the LDAP query when searching users in the people picker (comma-separated values)">Additional search attributes &#9432;</label>
                                        <wssawc:InputFormTextBox title="" class="ms-input" ID="TxtGroupAdditionalLdapAttributes" Columns="50" runat="server" MaxLength="255" />
                                    </li>
                                    <li>
                                        <label for="<%= TxtGroupLeadingToken.ClientID %>" title="Static or dynnamic token to add to the permission's value">Leading token in claim value &#9432;</label>
                                        <wssawc:InputFormTextBox title="" class="ms-input" ID="TxtGroupLeadingToken" Columns="50" runat="server" MaxLength="255" />
                                    </li>
                                    <li>
                                        <label for="<%= TxtGroupAdditionalLdapFilter.ClientID %>" title="Additional LDAP filter applied to all the group attributes">Additional LDAP filter &#9432;</label>
                                        <wssawc:InputFormTextBox title="" class="ms-input" ID="TxtGroupAdditionalLdapFilter" Columns="50" runat="server" MaxLength="255" />
                                    </li>
                                </ol>
                            </fieldset>
                        </div>
                    </td>
                </tr>
            </div>
        </Template_InputFormControls>
    </wssuc:InputFormSection>

    <wssuc:InputFormSection runat="server" Title="Augmentation">
        <Template_Description>
            <sharepoint:encodedliteral runat="server" text="If enabled, LDAPCP gets the group membership of the trusted users when they sign-in, or whenever SharePoint asks for it." encodemethod='HtmlEncodeAllowSimpleTextFormatting' />
            <br />
            <sharepoint:encodedliteral runat="server" text="If not enabled, some features and permissions granted to trusted groups may not work." encodemethod='HtmlEncodeAllowSimpleTextFormatting' />
        </Template_Description>
        <Template_InputFormControls>
            <tr>
                <td>
                    <asp:CheckBox Checked="false" runat="server" Name="ChkEnableAugmentation" ID="ChkEnableAugmentation" OnClick="window.Ldapcp.AdminGlobalSettingsControl.InitAugmentationControls();" Text="Enable augmentation" />
                    <div id="AugmentationControlsGrid">
                        <wssawc:SPGridView ID="GridLdapConnections" runat="server" AutoGenerateColumns="False" ShowHeader="false">
                            <Columns>
                                <asp:TemplateField>
                                    <ItemTemplate>
                                        <fieldset>
                                            <asp:TextBox ID="IdPropHidden" runat="server" Text='<%# Bind("Identifier") %>' Visible="false" />
                                            <legend><span>LDAP Server "<asp:Label ID="TextPath" runat="server" Text='<%# Bind("LdapPath") %>' />":</span></legend>
                                            <asp:CheckBox ID="ChkAugmentationEnableOnCoco" runat="server" Checked='<%# Bind("EnableAugmentation") %>' Text="Use for augmentation" />
                                            <asp:CheckBox ID="ChkGetGroupMembershipAsADDomain" runat="server" Checked='<%# Bind("GetGroupMembershipUsingDotNetHelpers") %>' Text="Get groups using the <a href='https://docs.microsoft.com/en-us/dotnet/api/system.directoryservices.accountmanagement.userprincipal.getauthorizationgroups' target='_blank'>.NET helper</a> (for Active Directory only)" />
                                        </fieldset>
                                    </ItemTemplate>
                                </asp:TemplateField>
                            </Columns>
                        </wssawc:SPGridView>
                    </div>
                </td>
            </tr>
        </Template_InputFormControls>
    </wssuc:InputFormSection>

    <wssuc:InputFormSection runat="server" Title="Active Directory specific settings" Description="Enable or disable LDAP filters specific to Active Directory.">
        <Template_InputFormControls>
            <asp:CheckBox Checked="false" runat="server" Name="ChkFilterEnabledUsersOnly" ID="ChkFilterEnabledUsersOnly" Text="Exclude disabled users" />
            <br />
            <br />
            <asp:CheckBox Checked="false" runat="server" Name="ChkFilterSecurityGroupsOnly" ID="ChkFilterSecurityGroupsOnly" Text="Exclude distribution lists" />
        </Template_InputFormControls>
    </wssuc:InputFormSection>

    <wssuc:InputFormSection runat="server" Title="LDAP requests timeout" Description="Specify the timeout for the requests to the LDAP servers, in seconds.">
        <Template_InputFormControls>
            <wssawc:InputFormTextBox title="Set the timeout value in seconds." class="ms-input" ID="txtTimeout" Columns="5" runat="server" MaxLength="3" />
            <wssawc:EncodedLiteral runat="server" Text="&nbsp;second(s)" EncodeMethod='HtmlEncodeAllowSimpleTextFormatting' />
        </Template_InputFormControls>
    </wssuc:InputFormSection>

    <wssuc:InputFormSection runat="server" Title="Bypass the LDAP server(s)">
        <Template_Description>
            <sharepoint:encodedliteral runat="server" text="Bypass the LDAP server(s) registered and, depending on the context:" encodemethod='HtmlEncodeAllowSimpleTextFormatting' />
            <br />
            <sharepoint:encodedliteral runat="server" text="- Search: Uses the input as the claim's value, and return 1 entity per claim type." encodemethod='HtmlEncodeAllowSimpleTextFormatting' />
            <br />
            <sharepoint:encodedliteral runat="server" text="- Validation: Validates the incoming entity as-is." encodemethod='HtmlEncodeAllowSimpleTextFormatting' />
            <br />
            <sharepoint:encodedliteral runat="server" text="This setting does not affect the augmentation." encodemethod='HtmlEncodeAllowSimpleTextFormatting' />
            <br />
            <br />
            <sharepoint:encodedliteral runat="server" text="It can be used as a mitigation if one or more SharePoint server(s) lost the connection with a LDAP server(s), until it is restored." encodemethod='HtmlEncodeAllowSimpleTextFormatting' />
        </Template_Description>
        <Template_InputFormControls>
            <asp:CheckBox Checked="false" runat="server" Name="ChkAlwaysResolveUserInput" ID="ChkAlwaysResolveUserInput" Text="Bypass requests to LDAP server(s)" />
        </Template_InputFormControls>
    </wssuc:InputFormSection>

    <wssuc:InputFormSection runat="server" Title="Require exact match when typing in the people picker">
        <Template_Description>
            <sharepoint:encodedliteral runat="server" text="Enable this to return results in the people picker, only if the user input matches exactly the value of the LDAP object attribute (case-insensitive)." encodemethod='HtmlEncodeAllowSimpleTextFormatting' />
        </Template_Description>
        <Template_InputFormControls>
            <asp:CheckBox Checked="false" runat="server" Name="ChkFilterExactMatchOnly" ID="ChkFilterExactMatchOnly" Text="Require exact match when typing in the people picker" />
        </Template_InputFormControls>
    </wssuc:InputFormSection>

    <wssuc:InputFormSection runat="server" Title="Reset LDAPCP configuration" Description="Restore configuration to its default values. All changes, including in claim types mappings, will be lost.">
        <Template_InputFormControls>
            <asp:Button runat="server" ID="BtnResetConfig" Text="Reset LDAPCP configuration" OnClick="BtnResetConfig_Click" class="ms-ButtonHeightWidth" OnClientClick="return confirm('Do you really want to reset LDAPCP configuration?');" />
        </Template_InputFormControls>
    </wssuc:InputFormSection>

    <wssuc:ButtonSection runat="server">
        <Template_Buttons>
            <asp:Button UseSubmitBehavior="false" runat="server" class="ms-ButtonHeightWidth" OnClick="BtnOK_Click" Text="<%$Resources:wss,multipages_okbutton_text%>" ID="BtnOK" AccessKey="<%$Resources:wss,okbutton_accesskey%>" />
        </Template_Buttons>
    </wssuc:ButtonSection>
</table>
