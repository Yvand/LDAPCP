<%@ Assembly Name="$SharePoint.Project.AssemblyFullName$" %>
<%@ Register TagPrefix="asp" Namespace="System.Web.UI" Assembly="System.Web.Extensions, Version=3.5.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35" %>
<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="ClaimsList.ascx.cs" Inherits="ldapcp.ControlTemplates.ClaimsList" %>

<script type="text/javascript" src="/_layouts/15/ldapcp/jquery-1.9.1.min.js"></script>
<style type="text/css">
    #divTblClaims th a:link {
        color: white;
        text-decoration: underline;
    }

    #divTblClaims th a:visited {
        color: white;
    }

    .ms-error {
        margin-bottom: 10px;
        display: block;
    }

    .ms-inputformcontrols {
        width: 500px;
    }

    .ldapcp-rowidentityclaim {
        font-weight: bold;
        color: green;
    }

    #divNewItem label {
        display: inline-block;
        line-height: 1.8;
        vertical-align: top;
        width: 250px;
    }

    #divNewItem fieldset {
        border: 0;
    }

        #divNewItem fieldset ol {
            margin: 0;
            padding: 0;
        }

        #divNewItem fieldset li {
            list-style: none;
            padding: 5px;
            margin: 0;
        }

    #divNewItem em {
        font-weight: bold;
        font-style: normal;
        color: #f00;
    }

    #divNewItem div label {
        width: 700px;
    }

    .divbuttons input {
        margin: 10px;
    }

    #divTblClaims table, #divTblClaims th, #divTblClaims td {
        border: 1px solid black;
        padding: 4px;
        border-collapse: collapse;
        word-wrap: normal;
    }

    #divTblClaims th {
        background-color: #0072c6;
        color: #fff;
    }

    .ldapcp-rowClaimTypeNotUsedInTrust {
        color: red;
        font-style: italic;
        text-decoration: line-through;
    }

    .ldapcp-rowUserProperty {
        color: green;
    }

    .ldapcp-rowMainGroupClaimType {
        font-weight: bold;
        color: #0072c6;
    }

    .ldapcp-rowGroupProperty {
        color: #0072c6;
    }

    #divBtnsFullScreenMode {
        margin-bottom: 10px;
    }

    #divLegend {
        margin-top: 10px;
    }

        #divLegend fieldset {
            border: 0;
        }

            #divLegend fieldset ol {
                margin: 0 0 0 5px;
                padding: 0;
            }

            #divLegend fieldset li {
                list-style: none;
                padding: 5px;
            }
</style>
<script type="text/javascript">
    // Builds unique namespace
    window.Ldapcp = window.Ldapcp || {};
    window.Ldapcp.ClaimsTablePage = window.Ldapcp.ClaimsTablePage || {};

    // Hide labels and show input controls
    window.Ldapcp.ClaimsTablePage.EditItem = function (ItemId) {
        $('#span_claimtype_' + ItemId).hide('fast');
        $('#span_attrname_' + ItemId).hide('fast');
        $('#span_attrclass_' + ItemId).hide('fast');
        $('#span_LDAPAttrToDisplay_' + ItemId).hide('fast');
        $('#span_Metadata_' + ItemId).hide('fast');
        $('#span_ClaimEntityType_' + ItemId).hide('fast');
        $('#span_AddLDAPFilter_' + ItemId).hide('fast');
        $('#span_KeywordToValidateInputWithoutLookup_' + ItemId).hide('fast');
        $('#span_PrefixToAddToValueReturned_' + ItemId).hide('fast');
        $('#editLink_' + ItemId).hide('fast');
        $('#<%= DeleteItemLink_.ClientID %>' + ItemId).hide('fast');

        $('#input_claimtype_' + ItemId).show('fast');
        $('#input_attrname_' + ItemId).show('fast');
        $('#input_attrclass_' + ItemId).show('fast');
        $('#input_LDAPAttrToDisplay_' + ItemId).show('fast');
        $('#list_Metadata_' + ItemId).show('fast');
        $('#list_ClaimEntityType_' + ItemId).show('fast');
        $('#input_AddLDAPFilter_' + ItemId).show('fast');
        $('#input_KeywordToValidateInputWithoutLookup_' + ItemId).show('fast');
        $('#input_PrefixToAddToValueReturned_' + ItemId).show('fast');
        $('#<%= UpdateItemLink_.ClientID %>' + ItemId).show('fast');
        $('#cancelLink_' + ItemId).show('fast');

        $('#chk_ShowClaimNameInDisplayText_' + ItemId).removeAttr("disabled");
    }

    // Show labels and hide input controls
    window.Ldapcp.ClaimsTablePage.CancelEditItem = function (ItemId) {
        $('#span_claimtype_' + ItemId).show('fast');
        $('#span_attrname_' + ItemId).show('fast');
        $('#span_attrclass_' + ItemId).show('fast');
        $('#span_LDAPAttrToDisplay_' + ItemId).show('fast');
        $('#span_Metadata_' + ItemId).show('fast');
        $('#span_ClaimEntityType_' + ItemId).show('fast');
        $('#span_AddLDAPFilter_' + ItemId).show('fast');
        $('#span_KeywordToValidateInputWithoutLookup_' + ItemId).show('fast');
        $('#span_PrefixToAddToValueReturned_' + ItemId).show('fast');
        $('#editLink_' + ItemId).show('fast');
        $('#<%= DeleteItemLink_.ClientID %>' + ItemId).show('fast');

        $('#input_claimtype_' + ItemId).hide('fast');
        $('#input_attrname_' + ItemId).hide('fast');
        $('#input_attrclass_' + ItemId).hide('fast');
        $('#input_LDAPAttrToDisplay_' + ItemId).hide('fast');
        $('#list_Metadata_' + ItemId).hide('fast');
        $('#list_ClaimEntityType_' + ItemId).hide('fast');
        $('#input_AddLDAPFilter_' + ItemId).hide('fast');
        $('#input_KeywordToValidateInputWithoutLookup_' + ItemId).hide('fast');
        $('#input_PrefixToAddToValueReturned_' + ItemId).hide('fast');
        $('#<%= UpdateItemLink_.ClientID %>' + ItemId).hide('fast');
        $('#cancelLink_' + ItemId).hide('fast');

        $('#chk_ShowClaimNameInDisplayText_' + ItemId).attr("disabled", true);
    }

    //$(document).ready(function () {
    //});

    // Register initialization method to run when DOM is ready and most SP JS functions loaded
    _spBodyOnLoadFunctionNames.push("window.Ldapcp.ClaimsTablePage.Init");

    window.Ldapcp.ClaimsTablePage.Init = function () {
        // Variables initialized from server side code
        window.Ldapcp.ClaimsTablePage.ShowNewItemForm = <%= ShowNewItemForm.ToString().ToLower() %>;
        window.Ldapcp.ClaimsTablePage.HideAllContent = <%= HideAllContent.ToString().ToLower() %>;
        window.Ldapcp.ClaimsTablePage.TrustName = "<%= CurrentTrustedLoginProviderName %>";

        // Check if all content should be hidden (most probably because LDAPCP is not associated with any SPTrustedLoginProvider)
        if (window.Ldapcp.ClaimsTablePage.HideAllContent) {
            $('#divMainContent').hide();
            return;
        }

        // Replace placeholder with actual SPTrustedIdentityTokenIssuer name
        $('#divMainContent').find("span").each(function (ev) {
            $(this).text($(this).text().replace("{trustname}", window.Ldapcp.ClaimsTablePage.TrustName));
        });

        // ONLY FOR SP 2013
        // Display only current page in full screen mode
        window.Ldapcp.ClaimsTablePage.SetFullScreenModeInCurPageOnly();

        // Initialize display
        var rdbGroupName = $('#<%= RdbNewItemClassicClaimType.ClientID %>').attr('name');
        if (Ldapcp.ClaimsTablePage.ShowNewItemForm) {
            $('#divTblClaims').hide('fast');
            $('#divNewItem').show('fast');

            var id = $("input[name='" + rdbGroupName + "']:checked").attr('id');
            $('#' + id).trigger('click');
        }
        else {
            $("input[name='" + rdbGroupName + "']:checked").removeAttr('checked');
        }
    }

    // Display only current page in full screen mode
    window.Ldapcp.ClaimsTablePage.SetFullScreenModeInCurPageOnly = function () {
        // Remove call to OOB InitFullScreenMode function. If not removed, it will disable full screen mode just after because it will not find cookie WSS_FullScreenMode
        _spBodyOnLoadFunctions.pop("InitFullScreenMode");

        // Copied from OOB SetFullScreenMode method (SP 2013 SP1 15.0.4569.1000)
        var bodyElement = document.body;
        var fsmButtonElement = document.getElementById('fullscreenmode');
        var efsmButtonElement = document.getElementById('exitfullscreenmode');
        AddCssClassToElement(bodyElement, "ms-fullscreenmode");
        if (fsmButtonElement != null && efsmButtonElement != null) {
            fsmButtonElement.style.display = 'none';
            efsmButtonElement.style.display = '';
        }
        if ('undefined' != typeof document.createEvent && 'function' == typeof window.dispatchEvent) {
            var evt = document.createEvent("Event");

            evt.initEvent("resize", false, false);
            window.dispatchEvent(evt);
        }
        else if ('undefined' != typeof document.createEventObject) {
            document.body.fireEvent('onresize');
        }
        CallWorkspaceResizedEventHandlers();

        PreventDefaultNavigation();
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
<div id="divMainContent">
    <asp:LinkButton ID="DeleteItemLink_" runat="server" Visible="false"></asp:LinkButton>
    <asp:LinkButton ID="UpdateItemLink_" runat="server" Visible="false"></asp:LinkButton>
    <div id="divBtnsFullScreenMode">
        <input type="button" value="Quit page" onclick="location.href = '/';" class="ms-ButtonHeightWidth" />
        <input id="btnDisableFullScreenMode" type="button" value="Show navigation" onclick="SetFullScreenMode(false); PreventDefaultNavigation(); $('#btnDisableFullScreenMode').hide(); $('#btnEnableFullScreenMode').show(); return false;" class="ms-ButtonHeightWidth" />
        <input id="btnEnableFullScreenMode" type="button" value="Maximize content" onclick="window.Ldapcp.ClaimsTablePage.SetFullScreenModeInCurPageOnly(); $('#btnEnableFullScreenMode').hide(); $('#btnDisableFullScreenMode').show(); return false;" style="display: none;" class="ms-ButtonHeightWidth" />
    </div>
    <div id="divTblClaims">
        <span style="display: block; margin-bottom: 10px;">This table is used by LDAPCP to map claim types set in SPTrustedIdentityTokenIssuer &quot;{trustname}&quot; with LDAP objects.</span>
        <asp:Table ID="TblClaimsMapping" runat="server"></asp:Table>
        <div id="divLegend">
            <fieldset>
                <legend>Formatting legend:</legend>
                <ol>
                    <li><span class="ldapcp-rowidentityclaim">This formatting</span><span> shows the identity claim type set in SPTrust &quot;{trustname}&quot;. It is required for LDAPCP to work.</span></li>
                    <li><span class="ldapcp-rowUserProperty">This formatting</span><span> shows an additional property used to search a User. Permission will be created using identity claim type configuration.</span></li>
                    <li><span class="ldapcp-rowMainGroupClaimType">This formatting</span><span> shows the main &quot;Group&quot; claim type, used for augmentation (which can be enabled or disabled in LDAPCP global settings page).</span></li>
					<li><span class="ldapcp-rowGroupProperty">This formatting</span><span> shows an additional property used to search a Group. Permission will be created using the main &quot;Group&quot; claim type configuration.</span></li>
					<li><span class="ldapcp-rowClaimTypeNotUsedInTrust">This formatting</span><span> shows a claim type not set in SPTrust &quot;{trustname}&quot;, it will be ignored by LDAPCP and may be deleted.</span></li>
                </ol>
            </fieldset>
        </div>
        <div class="divbuttons">
            <input type="button" value="New item" onclick="$('#divTblClaims').hide('fast'); $('#divNewItem').show('fast');" />
            <asp:Button ID="BtnReset" runat="server" Text="Reset" OnClick="BtnReset_Click" OnClientClick="javascript:return confirm('This will reset table to default mapping. Do you want to continue?');" />
        </div>
    </div>
    <div id="divNewItem" style="display: none;">
        <fieldset>
            <legend><b>Add a new item to the list</b></legend>
            <ol>
                <li>
                    <label>Select which type of entry to create: <em>*</em></label>
                    <div>
                        <asp:RadioButton ID="RdbNewItemClassicClaimType" runat="server" GroupName="RdgGroupNewItem" Text="Add a LDAP attribute to query and specify claim type of the permission." AutoPostBack="false" OnClick="$('#divNewItemControls').show('slow'); $('#rowClaimType').show('slow'); $('#emPermissionMetadata').hide('slow'); $('#rowClaimEntityType').show('slow');" />
                    </div>
                    <div>
                        <asp:RadioButton ID="RdbNewItemLinkdedToIdClaim" runat="server" GroupName="RdgGroupNewItem" Text="Add a LDAP attribute to query. Claim type of permission will depend on LDAP object class specified." AutoPostBack="false" OnClick="$('#divNewItemControls').show('slow'); $('#rowClaimType').hide('slow'); $('#emPermissionMetadata').hide('slow'); $('#rowClaimEntityType').hide('slow');" />
                    </div>
                    <div>
                        <asp:RadioButton ID="RdbNewItemPermissionMetadata" runat="server" GroupName="RdgGroupNewItem" Text="Add a LDAP attribute to use only as a <a href='http://msdn.microsoft.com/en-us/library/microsoft.sharepoint.webcontrols.peopleeditorentitydatakeys_members.aspx' target='_blank'>metadata</a> of the new permission." AutoPostBack="false" OnClick="$('#divNewItemControls').show('slow'); $('#rowClaimType').hide('slow'); $('#emPermissionMetadata').show('slow'); $('#rowClaimEntityType').hide('slow');" />
                    </div>
                </li>
                <div id="divNewItemControls" style="display: none;">
                    <li id="rowClaimType" style="display: none;">
                        <label for="<%= TxtNewClaimType.ClientID %>">Claim type: <em>*</em></label>
                        <asp:TextBox ID="TxtNewClaimType" runat="server" CssClass="ms-inputformcontrols"></asp:TextBox>
                    </li>
                    <li id="rowPermissionMetadata">
                        <label for="<%= New_DdlPermissionMetadata.ClientID %>">Type of <a href="http://msdn.microsoft.com/en-us/library/microsoft.sharepoint.webcontrols.peopleeditorentitydatakeys_members.aspx" target="_blank">permission metadata: </a><em id="emPermissionMetadata" style="display: none;">*</em></label>
                        <asp:DropDownList ID="New_DdlPermissionMetadata" runat="server" CssClass="ms-inputformcontrols"></asp:DropDownList>
                    </li>
                    <li>
                        <label for="<%= TxtNewAttrName.ClientID %>">LDAP Attribute: <em>*</em></label>
                        <asp:TextBox ID="TxtNewAttrName" runat="server" CssClass="ms-inputformcontrols"></asp:TextBox>
                    </li>
                    <li>
                        <label for="<%= TxtNewObjectClass.ClientID %>">LDAP Object class: <em>*</em></label>
                        <asp:TextBox ID="TxtNewObjectClass" runat="server" CssClass="ms-inputformcontrols"></asp:TextBox>
                    </li>
                    <li id="rowClaimEntityType">
                        <label for="<%=New_DdlClaimEntityType.ClientID %>"><a href="https://msdn.microsoft.com/en-us/library/office/microsoft.sharepoint.administration.claims.spclaimentitytypes_members.aspx" target="_blank">Claim entity type: </a><em>*</em></label>
                        <asp:DropDownList ID="New_DdlClaimEntityType" runat="server" CssClass="ms-inputformcontrols"></asp:DropDownList>
                    </li>
                </div>
            </ol>
            <div class="divbuttons">
                <asp:Button ID="BtnCreateNewItem" runat="server" Text="Create" OnClick="BtnCreateNewItem_Click" />
                <input type="button" value="Cancel" onclick="$('#divNewItem').hide('fast'); $('#divTblClaims').show('fast');" />
            </div>
        </fieldset>
    </div>
</div>
