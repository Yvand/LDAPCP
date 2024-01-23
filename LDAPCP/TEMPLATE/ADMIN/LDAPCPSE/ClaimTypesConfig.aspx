<%@ Page Language="C#" AutoEventWireup="true" Inherits="Microsoft.SharePoint.WebControls.LayoutsPageBase" MasterPageFile="~/_admin/admin.master" %>
<%@ Register TagPrefix="LDAPCP" TagName="ClaimTypesConfigUC" src="ClaimTypesConfig.ascx" %>
<%@ Import Namespace="Yvand.LdapClaimsProvider.Configuration" %>
<%@ Import Namespace="Yvand.LdapClaimsProvider" %>
<%@ Import Namespace="System.Diagnostics" %>
<%@ Import Namespace="System.Reflection" %>

<asp:Content ID="PageHead" ContentPlaceHolderID="PlaceHolderAdditionalPageHead" runat="server" />
<asp:Content ID="PageTitle" ContentPlaceHolderID="PlaceHolderPageTitle" runat="server">LDAPCP Second Edition - Claim types configuration</asp:Content>
<asp:Content ID="PageTitleInTitleArea" ContentPlaceHolderID="PlaceHolderPageTitleInTitleArea" runat="server">
    <%= String.Format("<a href=\"{1}\" target=\"_blank\">LDAPCP</a> {0}", ClaimsProviderConstants.ClaimsProviderVersion, ClaimsProviderConstants.PUBLICSITEURL) %>
</asp:Content>
<asp:Content ID="Main" ContentPlaceHolderID="PlaceHolderMain" runat="server">
    <table border="0" cellspacing="0" cellpadding="0" width="100%">
        <LDAPCP:ClaimTypesConfigUC ID="LdapcpClaimsList" Runat="server" ClaimsProviderName="<%# LDAPCPSE.ClaimsProviderName %>" ConfigurationName="<%# ClaimsProviderConstants.CONFIGURATION_NAME %>" ConfigurationID="<%# new Guid(ClaimsProviderConstants.CONFIGURATION_ID) %>" />
    </table>
</asp:Content>
