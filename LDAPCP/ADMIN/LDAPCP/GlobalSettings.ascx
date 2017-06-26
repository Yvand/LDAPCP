<%@ Assembly Name="$SharePoint.Project.AssemblyFullName$" %>
<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="GlobalSettings.ascx.cs" Inherits="ldapcp.ControlTemplates.GlobalSettings" %>
<%@ Register TagPrefix="asp" Namespace="System.Web.UI" Assembly="System.Web.Extensions, Version=3.5.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35" %>
<%@ Register TagPrefix="wssuc" TagName="InputFormSection" Src="~/_controltemplates/InputFormSection.ascx" %>
<%@ Register TagPrefix="wssuc" TagName="InputFormControl" Src="~/_controltemplates/InputFormControl.ascx" %>
<%@ Register TagPrefix="wssuc" TagName="ButtonSection" Src="~/_controltemplates/ButtonSection.ascx" %>
<%@ Register TagPrefix="wssawc" Namespace="Microsoft.SharePoint.WebControls" Assembly="Microsoft.SharePoint, Version=15.0.0.0, Culture=neutral, PublicKeyToken=71e9bce111e9429c" %>

<script type="text/javascript" src="/_layouts/15/ldapcp/jquery-1.9.1.min.js"></script>
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
        width: 650px;
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

    #divNewLdapConnection label {
        display: inline-block;
        line-height: 1.8;
        width: 100px;
    }

    #divNewLdapConnection fieldset {
        border: 0;
        margin: 0;
        padding: 0;
    }

        #divNewLdapConnection fieldset ol {
            margin: 0;
            padding: 0;
        }

        #divNewLdapConnection fieldset li {
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

<p class="ms-error">
    <asp:Label ID="LabelErrorMessage" runat="server" EnableViewState="False" /></p>

<wssuc:ButtonSection ID="ValidateTopSection" runat="server">
    <template_buttons>
        <asp:Button UseSubmitBehavior="false" runat="server" class="ms-ButtonHeightWidth" OnClick="BtnOK_Click" Text="<%$Resources:wss,multipages_okbutton_text%>" id="BtnOKTop" accesskey="<%$Resources:wss,okbutton_accesskey%>"/>
    </template_buttons>
</wssuc:ButtonSection>

<wssuc:InputFormSection ID="CurrentLdapConnectionSection" Title="Current LDAP connections" runat="server" Visible="<%# ShowCurrentLdapConnectionSection %>">
    <template_description>
		<wssawc:EncodedLiteral runat="server" text="Current LDAP connections." EncodeMethod='HtmlEncodeAllowSimpleTextFormatting'/>
	</template_description>
    <template_inputformcontrols>
		<tr><td>
		<wssawc:SPGridView runat="server" ID="grdLDAPConnections" AutoGenerateColumns="false" OnRowDeleting="grdLDAPConnections_RowDeleting">
			<Columns>
				<asp:BoundField DataField="Id" ItemStyle-CssClass="ldapcp-HideCol" HeaderStyle-CssClass="ldapcp-HideCol"/>
				<asp:BoundField HeaderText="LDAP Path" DataField="Path"/>
				<asp:BoundField HeaderText="Username" DataField="Username"/>
				<asp:CommandField HeaderText="Action" ButtonType="Button" DeleteText="Remove" ShowDeleteButton="True" />
			</Columns>
		</wssawc:SPGridView>
		</td></tr>
	</template_inputformcontrols>
</wssuc:InputFormSection>

<wssuc:InputFormSection ID="NewLdapConnectionSection" Title="New LDAP connection" runat="server" Visible="<%# ShowNewLdapConnectionSection %>">
    <template_description>
		<wssawc:EncodedLiteral runat="server" text="Create a new LDAP connection. A connection to same AD as SharePoint servers is created by default." EncodeMethod='HtmlEncodeAllowSimpleTextFormatting'/>
	</template_description>
    <template_inputformcontrols>
		<tr><td>
		<table>
		<wssawc:InputFormRadioButton id="RbUseServerDomain"
			LabelText="Connect to SharePoint AD domain"
			Checked="true"
			GroupName="RbLDAPConnection"
			CausesValidation="false"
			runat="server"
			onclick="window.Ldapcp.LdapcpSettingsPage.CheckDefaultADConnection()"					>
			<wssawc:EncodedLiteral runat="server" text="Connect to same AD as SharePoint servers, with application pool credentials." EncodeMethod='HtmlEncode'/>
		</wssawc:InputFormRadioButton>
		<wssawc:InputFormRadioButton id="RbUseCustomConnection"
			LabelText="Manually specify LDAP connection"
			GroupName="RbLDAPConnection"
			CausesValidation="false"
			onclick="window.Ldapcp.AdminGlobalSettingsControl.CheckCustomLdapRB()"
			runat="server" >
			<div id="divNewLdapConnection">
			<fieldset>
			<ol>
				<li>
					<label for="<%= TxtLdapConnectionString.ClientID %>">LDAP <a href="http://msdn.microsoft.com/en-us/library/system.directoryservices.directoryentry.path(v=vs.110).aspx" target="_blank">path</a>: <em>*</em></label>
					<wssawc:InputFormTextBox onclick="window.Ldapcp.AdminGlobalSettingsControl.CheckCustomLdapRB()" title="LDAP connection string" class="ms-input" ID="TxtLdapConnectionString" Columns="50" Runat="server" MaxLength=255 Text="LDAP://" />
				</li>
				<li>
					<label for="<%= TxtLdapUsername.ClientID %>">Username: <em>*</em></label>
					<wssawc:InputFormTextBox onclick="window.Ldapcp.AdminGlobalSettingsControl.CheckCustomLdapRB()" title="Username" class="ms-input" ID="TxtLdapUsername" Columns="50" Runat="server" MaxLength=255 />
				</li>
				<li>
					<label for="<%= TxtLdapPassword.ClientID %>">Password: <em>*</em></label>
					<wssawc:InputFormTextBox onclick="window.Ldapcp.AdminGlobalSettingsControl.CheckCustomLdapRB()" title="Password" class="ms-input" ID="TxtLdapPassword" Columns="50" Runat="server" MaxLength=255 TextMode="Password" />
				</li>
			</ol>
			</fieldset>
			</div>
			<tr><td colspan='2' style="padding-left: 30px;">
				<label>Select the <a href="http://msdn.microsoft.com/en-us/library/system.directoryservices.authenticationtypes(v=vs.110).aspx" target="_blank">authentication type</a> to use (optional):</label>
				<wssawc:InputFormCheckBoxList
					id="CblAuthenticationTypes" 
					runat="server"
					onclick="window.Ldapcp.AdminGlobalSettingsControl.CheckCustomLdapRB()"
					RepeatDirection="Horizontal" RepeatColumns="5"
					CellPadding="3" CellSpacing="0" />
			</td></tr>
		</wssawc:InputFormRadioButton>
		</table>
		<div class="divbuttons">
			<asp:Button runat="server" ID="BtnTestLdapConnection" Text="Test LDAP Connection" onclick="BtnTestLdapConnection_Click" class="ms-ButtonHeightWidth" style="display: none;" />
			<asp:Button runat="server" ID="BtnAddLdapConnection" Text="Add LDAP Connection" OnClick="BtnAddLdapConnection_Click" class="ms-ButtonHeightWidth" />
		</div>
		<p>
			<asp:Label ID="LabelErrorTestLdapConnection" Runat="server" EnableViewState="False" class="ms-error" />
			<asp:Label ID="LabelTestLdapConnectionOK" Runat="server" EnableViewState="False" />
		</p>
	</td></tr>
	</template_inputformcontrols>
</wssuc:InputFormSection>

<wssuc:inputformsection ID="AugmentationSection" runat="server" Visible="<%# ShowAugmentationSection %>" title="Augmentation" description="Enable augmentation to let LDAPCP get group membership of federated users.<br/><br/><b>Important:</b> During augmentation LDAPCP only knows the identity claim of the user, so it is strongly recommended to use an identity claim that ensures the uniqueness of the value across all domains, such as the email or the UPN.">
    <template_inputformcontrols>
        <p class="ms-error"><asp:Label ID="Label1" runat="server" EnableViewState="False" /></p>
        <asp:Checkbox Checked="false" Runat="server" Name="ChkEnableAugmentation" ID="ChkEnableAugmentation" OnClick="window.Ldapcp.AdminGlobalSettingsControl.InitAugmentationControls();" Text="Enable augmentation" />
        <div id="AugmentationControls" style="padding: 15px;">
            <wssawc:EncodedLiteral runat="server" text="Select the claim type to use for the groups:" EncodeMethod='HtmlEncodeAllowSimpleTextFormatting'/>
            <br />
            <asp:DropDownList ID="DdlClaimTypes" runat="server">
                <asp:ListItem Selected="True" Value="None"></asp:ListItem>
            </asp:DropDownList>
			<tr><td>
			<wssawc:EncodedLiteral runat="server" text="<p>For Active Directory servers, the preferred way to get groups is using <a href='https://msdn.microsoft.com/en-us/library/system.directoryservices.accountmanagement.userprincipal.getauthorizationgroups.aspx' target='_blank'>UserPrincipal.GetAuthorizationGroups()</a>.<br />Otherwise LDAPCP reads LDAP attribute memberOf/uniquememberof of the user.</p>" EncodeMethod='NoEncode'/>
			<div id="AugmentationControlsGrid">
            <wssawc:SPGridView ID="GridLdapConnections" runat="server" AutoGenerateColumns="False" ShowHeader="false">
                <Columns>
                    <asp:TemplateField>
                        <ItemTemplate>
							<fieldset>
                            <asp:TextBox ID="IdPropHidden" runat="server" Text='<%# Bind("IdProp") %>' Visible="false" />
							<legend><span>LDAP Server "<asp:Label ID="TextPath" runat="server" Text='<%# Bind("PathProp") %>' />":</span></legend>
                            <asp:CheckBox ID="ChkAugmentationEnableOnCoco" runat="server" Checked='<%# Bind("AugmentationEnabledProp") %>' Text="Query this server" />
                            <asp:CheckBox ID="ChkGetGroupMembershipAsADDomain" runat="server" Checked='<%# Bind("GetGroupMembershipAsADDomainProp") %>' Text="This is an Active Directory server, get groups using <a href='https://msdn.microsoft.com/en-us/library/system.directoryservices.accountmanagement.userprincipal.getauthorizationgroups.aspx' target='_blank'>UserPrincipal.GetAuthorizationGroups</a>" />
							</fieldset>
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
            </wssawc:SPGridView>
			</div>
			</td></tr>
        </div>
    </template_inputformcontrols>
</wssuc:inputformsection>

<wssuc:InputFormSection runat="server" Title="LDAP query timeout" Description="Set the timeout value for LDAP queries.">
    <template_inputformcontrols>
        <wssawc:InputFormTextBox title="Set the timeout in seconds." class="ms-input" ID="txtTimeout" Columns="5" Runat="server" MaxLength="3" />
        <wssawc:EncodedLiteral runat="server" text="&nbsp;second(s)" EncodeMethod='HtmlEncodeAllowSimpleTextFormatting'/>
    </template_inputformcontrols>
</wssuc:InputFormSection>

<wssuc:InputFormSection runat="server" Title="Display of permissions created with identity claim" Description="Customize the display text of permissions created with identity claim. Identity claim is defined in the TrustedLoginProvider.<br/> It does not impact the actual value of the permission that will always be the LDAP attribute associated with the identity claim.">
    <template_inputformcontrols>
		<wssawc:InputFormRadioButton id="RbIdentityDefault"
			LabelText="Display LDAP attribute mapped to identity claim"
			Checked="true"
			GroupName="RbIdentityDisplay"
			CausesValidation="false"
			runat="server" >
        </wssawc:InputFormRadioButton>
		<wssawc:InputFormRadioButton id="RbIdentityCustomLDAP"
			LabelText="Display another LDAP attribute"
			GroupName="RbIdentityDisplay"
			CausesValidation="false"
			runat="server" >
        <wssuc:InputFormControl LabelText="InputFormControlLabelText">
			<Template_control>
				<wssawc:EncodedLiteral runat="server" text="This is useful if LDAP attribute used doesn't mean anything to users (for example a corporate ID).<br/>LDAP attribute to display:<br/>" EncodeMethod='HtmlEncodeAllowSimpleTextFormatting'/>
				<wssawc:InputFormTextBox onclick="window.Ldapcp.AdminGlobalSettingsControl.CheckRbIdentityCustomLDAP()" title="LDAP attribute to display" class="ms-input" ID="TxtLdapAttributeToDisplay" Columns="50" Runat="server" MaxLength=255 />
			</Template_control>
		</wssuc:InputFormControl>
		</wssawc:InputFormRadioButton>
		<tr><td colspan="2"><br/>
		<asp:Checkbox Runat="server" Name="ChkIdentityShowAdditionalAttribute" ID="ChkIdentityShowAdditionalAttribute" Text="If input matches an attribute linked to identity claim (typically the displayName), show its value in parenthesis." />
		</td></tr>
	</template_inputformcontrols>
</wssuc:InputFormSection>
<wssuc:InputFormSection runat="server" Title="Bypass LDAP lookup" Description="Completely bypass LDAP lookup and consider any input as valid.<br/><br/>This can be useful to keep people picker working even if connectivity with LDAP server is lost.">
    <template_inputformcontrols>
        <asp:Checkbox Checked="false" Runat="server" Name="ChkAlwaysResolveUserInput" ID="ChkAlwaysResolveUserInput" Text="Bypass LDAP lookup" />
	</template_inputformcontrols>
</wssuc:InputFormSection>
<wssuc:InputFormSection runat="server" Title="Active Directory specific settings" Description="Customize LDAP lookup with settings specific to Active Directory.">
    <template_inputformcontrols>
        <asp:Checkbox Checked="false" Runat="server" Name="ChkFilterEnabledUsersOnly" ID="ChkFilterEnabledUsersOnly" Text="Exclude disabled users (as documented in <a href='http://support.microsoft.com/kb/827754/' target='blank'>KB 827754</a>)." />
        <br /><br />
        <asp:Checkbox Checked="false" Runat="server" Name="ChkFilterSecurityGroupsOnly" ID="ChkFilterSecurityGroupsOnly" Text="Exclude distribution lists" />
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
<wssuc:InputFormSection runat="server" Title="Require exact match" Description="Set to only return results that exactly match the user input (case-insensitive).">
    <template_inputformcontrols>
		<asp:Checkbox Checked="false" Runat="server" Name="ChkFilterExactMatchOnly" ID="ChkFilterExactMatchOnly" Text="Require exact match" />
	</template_inputformcontrols>
</wssuc:InputFormSection>
<wssuc:InputFormSection runat="server" Title="Additional LDAP filter for user attributes" Description="Specify a custom LDAP filter that will be applied to all user attributes.<br/>By default this filter is set to exclude computer accounts: (!(objectClass=computer))<br/><br/>As an example, this filter excludes computer accounts and includes only users that are member of a specific security group:<br/> (!(objectClass=computer)) (memberof=CN=group1,CN=Users,DC=YvanHost,DC=local)<br/><br/>Important notes:<br/>If the filter is incorrect, every user resolution may break.<br/>This filter only applies to entries with &quot;SPClaimEntityTypes&quot; set to &quot;User&quot;.">
    <template_inputformcontrols>
		<label for="<%= TxtAdditionalUserLdapFilter.ClientID %>">Additional LDAP filter for entries with <a href='http://msdn.microsoft.com/en-us/library/office/microsoft.sharepoint.administration.claims.spclaimentitytypes_members(v=office.15).aspx' target='_blank'>SPClaimEntityTypes</a> set to &quot;User&quot; (leave blank to remove):</label><br/>
		<wssawc:InputFormTextBox title="Additional LDAP filter" class="ms-input" ID="TxtAdditionalUserLdapFilter" Columns="50" Runat="server" />
        <p><asp:Button runat="server" ID="BtnUpdateAdditionalUserLdapFilter" Text="Apply to all user attributes" onclick="BtnUpdateAdditionalUserLdapFilter_Click" CssClass="ms-ButtonHeightWidth" OnClientClick="return confirm('This will apply LDAP filter to all user attributes, do you want to continue?');" /></p>
        <p class="ldapcp-success"><asp:Label ID="LabelUpdateAdditionalLdapFilterOk" Runat="server" EnableViewState="False" /></p>
	</template_inputformcontrols>
</wssuc:InputFormSection>
<wssuc:InputFormSection runat="server" Title="Reset LDAPCP configuration" Description="This will delete the LDAPCP persisted object in configuration database and recreate one with default values. Every custom settings, including customized claim types, will be deleted.">
    <template_inputformcontrols>
		<asp:Button runat="server" ID="BtnResetLDAPCPConfig" Text="Reset LDAPCP configuration" onclick="BtnResetLDAPCPConfig_Click" class="ms-ButtonHeightWidth" OnClientClick="return confirm('Do you really want to reset LDAPCP configuration?');" />
	</template_inputformcontrols>
</wssuc:InputFormSection>

<wssuc:ButtonSection ID="ValidateSection" runat="server" Visible="<%# ShowValidateSection %>">
    <template_buttons>
		<asp:Button UseSubmitBehavior="false" runat="server" class="ms-ButtonHeightWidth" OnClick="BtnOK_Click" Text="<%$Resources:wss,multipages_okbutton_text%>" id="BtnOK" accesskey="<%$Resources:wss,okbutton_accesskey%>"/>
	</template_buttons>
</wssuc:ButtonSection>
