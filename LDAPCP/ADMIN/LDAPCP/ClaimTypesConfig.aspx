<%@ Page Language="C#" AutoEventWireup="true" Inherits="Microsoft.SharePoint.WebControls.LayoutsPageBase" MasterPageFile="~/_admin/admin.master" %>
<%@ Register TagPrefix="asp" Namespace="System.Web.UI" Assembly="System.Web.Extensions, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35" %>
<%@ Register TagPrefix="Ldapcp" TagName="ClaimTypesConfigUC" src="ClaimTypesConfig.ascx" %>
<%@ Import Namespace="ldapcp" %>
<%@ Import Namespace="System.Diagnostics" %>
<%@ Import Namespace="System.Reflection" %>

<asp:Content ID="PageHead" ContentPlaceHolderID="PlaceHolderAdditionalPageHead" runat="server">
</asp:Content>
<asp:Content ID="PageTitle" ContentPlaceHolderID="PlaceHolderPageTitle" runat="server">
    Claim types configuration for LDAPCP
</asp:Content>
<asp:Content ID="PageTitleInTitleArea" ContentPlaceHolderID="PlaceHolderPageTitleInTitleArea" runat="server">
    <asp:Literal runat="server" ID="TitleInTitleText" />
</asp:Content>
<asp:Content ID="Main" ContentPlaceHolderID="PlaceHolderMain" runat="server">
    <table border="0" cellspacing="0" cellpadding="0" width="100%">
        <Ldapcp:ClaimTypesConfigUC ID="LdapcpClaimsList" Runat="server" ClaimsProviderName="LDAPCP" PersistedObjectName="<%# ClaimsProviderConstants.LDAPCPCONFIG_NAME %>" PersistedObjectID="<%# ClaimsProviderConstants.LDAPCPCONFIG_ID %>" />
    </table>
</asp:Content>
