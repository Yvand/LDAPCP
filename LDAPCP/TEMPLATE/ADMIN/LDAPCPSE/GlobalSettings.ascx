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

    .Ldapcpse-success {
        color: green;
        font-weight: bold;
    }

    .Ldapcpse-HideCol {
        display: none;
    }

    #divNewLdapConnection label {
        display: inline-block;
        line-height: 1.8;
        width: 250px;
    }

    #divUserIdentifiers label {
        display: inline-block;
        line-height: 1.8;
        width: 250px;
    }

    fieldset {
        border: 1;
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

    #divNewLdapConnection em {
        font-weight: bold;
        font-style: normal;
        color: #f00;
    }
</style>

<script type="text/javascript">
    // Builds unique namespace
    window.Ldapcp = window.Ldapcp || {};
    window.Ldapcp.AdminGlobalSettingsControl = window.Ldapcp.AdminGlobalSettingsControl || {};

    _spBodyOnLoadFunctionNames.push("window.Ldapcp.AdminGlobalSettingsControl.Init");
    window.Ldapcp.AdminGlobalSettingsControl.Init = function () {
        window.Ldapcp.AdminGlobalSettingsControl.InitLdapControls();
        window.Ldapcp.AdminGlobalSettingsControl.InitAugmentationControls();
    }

    window.Ldapcp.AdminGlobalSettingsControl.InitLdapControls = function () {
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
    }

    // Enable or disable controls for augmentation section
    window.Ldapcp.AdminGlobalSettingsControl.InitAugmentationControls = function () {
        var enableAugmentationControls = $('#<%= ChkEnableAugmentation.ClientID %>').is(":checked");
        if (enableAugmentationControls) {
            $("#AugmentationControls").children().prop('disabled', false);
            $("#AugmentationControlsGrid").children().removeAttr('disabled');
            $("#AugmentationControlsGrid").children().prop('disabled', null);
        }
        else {
            $("#AugmentationControls").children().prop('disabled', true);
            $("#AugmentationControlsGrid").children().attr('disabled', '');
            $("#AugmentationControlsGrid").children().prop('disabled');
        }
    }

    // New LDAP connection section
    window.Ldapcp.AdminGlobalSettingsControl.CheckCustomLdapRB = function () {
        var CbUserQuota = (document.getElementById("<%= RbUseCustomConnection.ClientID %>"));
        if (CbUserQuota != null) {
            CbUserQuota.checked = true;
        }
        $('#<%= BtnTestLdapConnection.ClientID %>').show('fast');
        $('#divNewLdapConnection').find('em').show();
    }

    window.Ldapcp.AdminGlobalSettingsControl.CheckDefaultADConnection = function () {
        $('#divNewLdapConnection').find('em').hide();
        $('#<%= BtnTestLdapConnection.ClientID %>').hide('fast');
    }

    // Identity permission section
    window.Ldapcp.AdminGlobalSettingsControl.CheckRbIdentityCustomLDAP = function () {
        var control = (document.getElementById("<%= RbIdentityCustomLDAP.ClientID %>"));
        if (control != null) {
            control.checked = true;
        }
    }
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
    <wssuc:buttonsection runat="server">
        <template_buttons>
            <asp:Button UseSubmitBehavior="false" runat="server" class="ms-ButtonHeightWidth" OnClick="BtnOK_Click" Text="<%$Resources:wss,multipages_okbutton_text%>" ID="BtnOKTop" AccessKey="<%$Resources:wss,okbutton_accesskey%>" />
        </template_buttons>
    </wssuc:buttonsection>

    <wssuc:InputFormSection ID="CurrentLdapConnectionSection" Title="Registered LDAP connections" runat="server">
        <template_description>
            <wssawc:EncodedLiteral runat="server" Text="LDAP connections currently registered in LDAPCP configuration." EncodeMethod='HtmlEncodeAllowSimpleTextFormatting' />
        </template_description>
        <template_inputformcontrols>
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
        </template_inputformcontrols>
    </wssuc:InputFormSection>

    <wssuc:InputFormSection ID="NewLdapConnectionSection" Title="Register a new LDAP connection" runat="server">
        <template_description>
            <wssawc:EncodedLiteral runat="server" Text="By default, LDAPCP connects to the Active Directory domain of the SharePoint servers using the application pool identity. This connection is labelled 'Connect to SharePoint domain'." EncodeMethod='HtmlEncodeAllowSimpleTextFormatting' />
        </template_description>
        <template_inputformcontrols>
            <tr>
                <td>
                    <table>
                        <wssawc:InputFormRadioButton ID="RbUseServerDomain"
                            LabelText="Connect to SharePoint AD domain"
                            Checked="true"
                            GroupName="RbLDAPConnection"
                            CausesValidation="false"
                            runat="server"
                            onclick="window.Ldapcp.LdapcpSettingsPage.CheckDefaultADConnection()">
                            <wssawc:EncodedLiteral runat="server" Text="Connect to same AD as SharePoint servers, with application pool credentials." EncodeMethod='HtmlEncode' />
                        </wssawc:InputFormRadioButton>
                        <wssawc:InputFormRadioButton ID="RbUseCustomConnection"
                            LabelText="Connect to a LDAP server"
                            GroupName="RbLDAPConnection"
                            CausesValidation="false"
                            onclick="window.Ldapcp.AdminGlobalSettingsControl.CheckCustomLdapRB()"
                            runat="server">
                            <div id="divNewLdapConnection">
                                <fieldset>
                                    <ol>
                                        <li>
                                            <label for="<%= TxtLdapConnectionString.ClientID %>">LDAP <a href="http://msdn.microsoft.com/en-us/library/system.directoryservices.directoryentry.path(v=vs.110).aspx" target="_blank">path</a>: <em>*</em></label>
                                            <wssawc:InputFormTextBox onclick="window.Ldapcp.AdminGlobalSettingsControl.CheckCustomLdapRB()" title="LDAP connection string" class="ms-input" ID="TxtLdapConnectionString" Columns="50" runat="server" MaxLength="255" Text="LDAP://" />
                                        </li>
                                        <li>
                                            <label for="<%= TxtLdapUsername.ClientID %>">Username: <em>*</em></label>
                                            <wssawc:InputFormTextBox onclick="window.Ldapcp.AdminGlobalSettingsControl.CheckCustomLdapRB()" title="Username" class="ms-input" ID="TxtLdapUsername" Columns="50" runat="server" MaxLength="255" />
                                        </li>
                                        <li>
                                            <label for="<%= TxtLdapPassword.ClientID %>">Password: <em>*</em></label>
                                            <wssawc:InputFormTextBox onclick="window.Ldapcp.AdminGlobalSettingsControl.CheckCustomLdapRB()" title="Password" class="ms-input" ID="TxtLdapPassword" Columns="50" runat="server" MaxLength="255" TextMode="Password" />
                                        </li>
                                    </ol>
                                </fieldset>
                            </div>
                            <tr>
                                <td colspan='2' style="padding-left: 30px;">
                                    <label>Select the <a href="http://msdn.microsoft.com/en-us/library/system.directoryservices.authenticationtypes(v=vs.110).aspx" target="_blank">authentication type</a> to use (optional):</label>
                                    <wssawc:InputFormCheckBoxList
                                        ID="CblAuthenticationTypes"
                                        runat="server"
                                        onclick="window.Ldapcp.AdminGlobalSettingsControl.CheckCustomLdapRB()"
                                        RepeatDirection="Horizontal" RepeatColumns="5"
                                        CellPadding="3" CellSpacing="0" />
                                </td>
                            </tr>
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
        </template_inputformcontrols>
    </wssuc:InputFormSection>

    <wssuc:inputformsection runat="server" title="Display of user identifier results" description="Configure how entities created with the identity claim type appear in the people picker.<br/>It does not affect the actual value of the entity, which is always set with the user identifier property.">
        <template_inputformcontrols>
            <wssawc:InputFormRadioButton ID="RbIdentityDefault"
                LabelText="Show the user identifier value"
                Checked="true"
                GroupName="RbIdentityDisplay"
                CausesValidation="false"
                runat="server">
            </wssawc:InputFormRadioButton>
            <wssawc:InputFormRadioButton ID="RbIdentityCustomGraphProperty"
                LabelText="Show the value of another property, e.g the display name:"
                GroupName="RbIdentityDisplay"
                CausesValidation="false"
                runat="server">
                <wssuc:InputFormControl LabelText="InputFormControlLabelText">
                    <template_control>
                        <asp:DropDownList runat="server" ID="DDLGraphPropertyToDisplay" onclick="window.Ldapcpse.EntracpSettingsPage.CheckRbIdentityCustomGraphProperty()" class="ms-input" />
                    </template_control>
                </wssuc:InputFormControl>
            </wssawc:InputFormRadioButton>
        </template_inputformcontrols>
    </wssuc:inputformsection>

    <wssuc:inputformsection ID="AugmentationSection" runat="server" title="Configuration for the groups" description="Enable augmentation to let LDAPCP get all the groups that the user is a member of.<br/><br/>If not enabled, permissions granted to federated groups may not work correctly.">
        <template_inputformcontrols>
            <p class="ms-error">
                <asp:Label ID="Label1" runat="server" EnableViewState="False" />
            </p>
            <div id="AugmentationControls">
                <tr>
                    <td>
                        <div id="divGroupClaimTypeConfiguration">
                            <fieldset>
                                <legend>Configuration for the group claim type</legend>
                                <ol>
                                    <li>
                                        <wssawc:EncodedLiteral runat="server" Text="Select the claim type to use for the groups:" EncodeMethod='HtmlEncodeAllowSimpleTextFormatting' />
                                        <br />
                                        <asp:DropDownList ID="DdlGroupClaimType" runat="server">
                                            <asp:ListItem Selected="True" Value="None"></asp:ListItem>
                                        </asp:DropDownList>
                                    </li>
                                    <li>
                                        <label for="<%= TxtGroupLdapClass.ClientID %>">LDAP object class: <em>*</em></label>
                                        <wssawc:InputFormTextBox title="LDAP object class" class="ms-input" ID="TxtGroupLdapClass" Columns="50" runat="server" MaxLength="255" />
                                    </li>
                                    <li>
                                        <label for="<%= TxtGroupLdapAttribute.ClientID %>">LDAP object attribute: <em>*</em></label>
                                        <wssawc:InputFormTextBox title="LDAP object attribute" class="ms-input" ID="TxtGroupLdapAttribute" Columns="50" runat="server" MaxLength="255" />
                                    </li>
                                    <li>
                                        <label for="<%= TxtGroupAdditionalLdapAttributes.ClientID %>">Additional LDAP attributes to search a group (values separated by a ','):</label>
                                        <wssawc:InputFormTextBox title="Additional LDAP attributes" class="ms-input" ID="TxtGroupAdditionalLdapAttributes" Columns="50" runat="server" MaxLength="255" />
                                    </li>
                                    <li>
                                        <label for="<%= TxtGroupLeadingToken.ClientID %>">Leading token added to the value returned by LDAP: <em>*</em></label>
                                        <wssawc:InputFormTextBox title="LDAP object attribute" class="ms-input" ID="TxtGroupLeadingToken" Columns="50" runat="server" MaxLength="255" />
                                    </li>
                                </ol>
                            </fieldset>
                        </div>
                    </td>
                </tr>
                <tr>
                    <td>
                        <asp:CheckBox Checked="false" runat="server" Name="ChkEnableAugmentation" ID="ChkEnableAugmentation" OnClick="window.Ldapcp.AdminGlobalSettingsControl.InitAugmentationControls();" Text="Enable augmentation" />
                        <wssawc:EncodedLiteral runat="server" Text="<p>Augmentation can be activated/deactivated per connection.<br />If connecting to Active Directory, you may check option &quot;Use <a href='https://docs.microsoft.com/en-us/dotnet/api/system.directoryservices.accountmanagement.userprincipal.getauthorizationgroups' target='_blank'>.NET helper</a>&quot;.<br />Otherwise, LDAPCP gets groups by reading the LDAP attribute memberOf/uniquememberof of the user.</p>" EncodeMethod='NoEncode' />
                        <div id="AugmentationControlsGrid">
                            <wssawc:SPGridView ID="GridLdapConnections" runat="server" AutoGenerateColumns="False" ShowHeader="false">
                                <Columns>
                                    <asp:TemplateField>
                                        <ItemTemplate>
                                            <fieldset>
                                                <asp:TextBox ID="IdPropHidden" runat="server" Text='<%# Bind("Identifier") %>' Visible="false" />
                                                <legend><span>LDAP Server "<asp:Label ID="TextPath" runat="server" Text='<%# Bind("LDAPPath") %>' />":</span></legend>
                                                <asp:CheckBox ID="ChkAugmentationEnableOnCoco" runat="server" Checked='<%# Bind("EnableAugmentation") %>' Text="Query this server" />
                                                <asp:CheckBox ID="ChkGetGroupMembershipAsADDomain" runat="server" Checked='<%# Bind("GetGroupMembershipUsingDotNetHelpers") %>' Text="Use <a href='https://docs.microsoft.com/en-us/dotnet/api/system.directoryservices.accountmanagement.userprincipal.getauthorizationgroups' target='_blank'>.NET helper</a> (for Active Directory only)" />
                                            </fieldset>
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                </Columns>
                            </wssawc:SPGridView>
                        </div>
                    </td>
                </tr>
            </div>
        </template_inputformcontrols>
    </wssuc:inputformsection>

    <wssuc:InputFormSection runat="server" Title="LDAP query timeout" Description="Set the timeout value for LDAP queries.">
        <template_inputformcontrols>
            <wssawc:InputFormTextBox title="Set the timeout in seconds." class="ms-input" ID="txtTimeout" Columns="5" runat="server" MaxLength="3" />
            <wssawc:EncodedLiteral runat="server" Text="&nbsp;second(s)" EncodeMethod='HtmlEncodeAllowSimpleTextFormatting' />
        </template_inputformcontrols>
    </wssuc:InputFormSection>

    <wssuc:inputformsection runat="server" title="User identifier properties" description="Set the LDAP class and attribute that identify users in AD / LDAP.">
        <template_inputformcontrols>
            <div id="divUserIdentifiers">
                <label>LDAP class:</label>
                <asp:TextBox runat="server" ID="TxtUserIdentifierLDAPClass" class="ms-input">
                </asp:TextBox>
                <br />
                <label>LDAP attribute:</label>
                <asp:TextBox runat="server" ID="TxtUserIdentifierLDAPAttribute" class="ms-input">
                </asp:TextBox>
            </div>
        </template_inputformcontrols>
    </wssuc:inputformsection>

    <wssuc:InputFormSection runat="server" Title="Display of user identifier results" description="Configure how entities created with the identity claim type appear in the people picker.<br/>It does not affect the actual value of the entity, which is always set with the user identifier attribute.">
        <template_inputformcontrols>
            <wssawc:InputFormRadioButton ID="InputFormRadioButton1"
                LabelText="Show the user identifier value"
                Checked="true"
                GroupName="RbIdentityDisplay"
                CausesValidation="false"
                runat="server">
            </wssawc:InputFormRadioButton>
            <wssawc:InputFormRadioButton ID="RbIdentityCustomLDAP"
                LabelText="Show the value of another LDAP attribute, e.g. displayName:"
                GroupName="RbIdentityDisplay"
                CausesValidation="false"
                runat="server">
                <wssuc:InputFormControl LabelText="InputFormControlLabelText">
                    <template_control>
                        <wssawc:InputFormTextBox onclick="window.Ldapcp.AdminGlobalSettingsControl.CheckRbIdentityCustomLDAP()" title="LDAP attribute to display" class="ms-input" ID="TxtLdapAttributeToDisplay" Columns="50" Runat="server" MaxLength="255" />
                    </template_control>
                </wssuc:InputFormControl>
            </wssawc:InputFormRadioButton>
            <tr>
                <td colspan="2">
                    <br />
                    <asp:CheckBox runat="server" Name="ChkIdentityShowAdditionalAttribute" ID="ChkIdentityShowAdditionalAttribute" Text="If input matches an attribute linked to identity claim (typically the displayName), show its value in parenthesis." />
                </td>
            </tr>
        </template_inputformcontrols>
    </wssuc:InputFormSection>

    <wssuc:InputFormSection runat="server" Title="Bypass LDAP lookup" Description="Skip LDAP lookup and consider any input as valid.<br/><br/>This can be useful to keep people picker working even if connectivity with LDAP server is lost.">
        <template_inputformcontrols>
            <asp:CheckBox Checked="false" runat="server" Name="ChkAlwaysResolveUserInput" ID="ChkAlwaysResolveUserInput" Text="Bypass LDAP lookup" />
        </template_inputformcontrols>
    </wssuc:InputFormSection>

    <wssuc:InputFormSection runat="server" Title="Active Directory specific settings" Description="Customize LDAP lookup with settings specific to Active Directory.">
        <template_inputformcontrols>
            <asp:CheckBox Checked="false" runat="server" Name="ChkFilterEnabledUsersOnly" ID="ChkFilterEnabledUsersOnly" Text="Exclude disabled users" />
            <br />
            <br />
            <asp:CheckBox Checked="false" runat="server" Name="ChkFilterSecurityGroupsOnly" ID="ChkFilterSecurityGroupsOnly" Text="Exclude distribution lists" />
        </template_inputformcontrols>
    </wssuc:InputFormSection>
    <%--<wssuc:InputFormSection runat="server" Title="Use wildcard in beginning of query" Description="In most cases, LDAP lookup is significantly faster when query does not start with a wildcard (for example searching 'joe*' instead of '*joe*').">
    <template_inputformcontrols>
        <asp:Checkbox Checked="false" Runat="server" Name="ChkAddWildcardInFront" ID="ChkAddWildcardInFront" Text="Use wildcard in beginning of query" />
	</template_inputformcontrols>
</wssuc:InputFormSection>
<wssuc:InputFormSection runat="server" Title="People picker display text" Description="This text is displayed in the header of the results list in the people picker control">
    <template_inputformcontrols>
		<wssawc:InputFormTextBox title="Text to display" class="ms-input" ID="TxtPickerEntityGroupName" Columns="50" Runat="server" MaxLength=255 />
	</template_inputformcontrols>
</wssuc:InputFormSection> --%>
    <wssuc:InputFormSection runat="server" Title="Require exact match" Description="Enable this to return only results that match exactly the user input (case-insensitive). ">
        <template_inputformcontrols>
            <asp:CheckBox Checked="false" runat="server" Name="ChkFilterExactMatchOnly" ID="ChkFilterExactMatchOnly" Text="Require exact match" />
        </template_inputformcontrols>
    </wssuc:InputFormSection>

    <wssuc:InputFormSection runat="server" Title="Additional LDAP filter for user attributes" Description="Specify a custom LDAP filter that will be applied to all user attributes.<br/>By default this filter is set to exclude computer accounts: (!(objectClass=computer))<br/><br/>As an example, this filter excludes computer accounts and includes only users that are member of a specific security group:<br/> (!(objectClass=computer)) (memberof=CN=group1,CN=Users,DC=YvanHost,DC=local)<br/><br/>Important notes:<br/>If the filter is incorrect, every user resolution may break.<br/>This filter only applies to entries with &quot;SPClaimEntityTypes&quot; set to &quot;User&quot;.">
        <template_inputformcontrols>
            <label for="<%= TxtAdditionalUserLdapFilter.ClientID %>">Additional LDAP filter for entries with <a href='http://msdn.microsoft.com/en-us/library/office/microsoft.sharepoint.administration.claims.spclaimentitytypes_members(v=office.15).aspx' target='_blank'>SPClaimEntityTypes</a> set to &quot;User&quot; (leave blank to remove):</label><br />
            <wssawc:InputFormTextBox title="Additional LDAP filter" class="ms-input" ID="TxtAdditionalUserLdapFilter" Columns="50" runat="server" />
            <p>
                <asp:Button runat="server" ID="BtnUpdateAdditionalUserLdapFilter" Text="Apply to all user attributes" OnClick="BtnUpdateAdditionalUserLdapFilter_Click" CssClass="ms-ButtonHeightWidth" OnClientClick="return confirm('This will apply LDAP filter to all user attributes, do you want to continue?');" />
            </p>
            <p class="ldapcp-success">
                <asp:Label ID="LabelUpdateAdditionalLdapFilterOk" runat="server" EnableViewState="False" />
            </p>
        </template_inputformcontrols>
    </wssuc:InputFormSection>

    <wssuc:InputFormSection runat="server" Title="Reset LDAPCP configuration" description="Restore configuration to its default values. All changes, including in claim types mappings, will be lost.">
        <template_inputformcontrols>
            <asp:Button runat="server" ID="BtnResetConfig" Text="Reset LDAPCP configuration" OnClick="BtnResetConfig_Click" class="ms-ButtonHeightWidth" OnClientClick="return confirm('Do you really want to reset LDAPCP configuration?');" />
        </template_inputformcontrols>
    </wssuc:InputFormSection>

    <wssuc:buttonsection runat="server">
        <template_buttons>
            <asp:Button UseSubmitBehavior="false" runat="server" class="ms-ButtonHeightWidth" OnClick="BtnOK_Click" Text="<%$Resources:wss,multipages_okbutton_text%>" ID="BtnOK" AccessKey="<%$Resources:wss,okbutton_accesskey%>" />
        </template_buttons>
    </wssuc:buttonsection>
</table>
