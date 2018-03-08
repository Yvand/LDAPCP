<%@ Assembly Name="$SharePoint.Project.AssemblyFullName$" %>
<%@ Page Language="C#" AutoEventWireup="true" Inherits="Microsoft.SharePoint.WebControls.LayoutsPageBase" MasterPageFile="~/_admin/admin.master" %>
<%@ Import Namespace="ldapcp" %>
<%@ Import Namespace="System.Diagnostics" %>
<%@ Import Namespace="System.Reflection" %>
<%@ Register Src="GlobalSettings.ascx" TagName="GlobalSettings" TagPrefix="Ldapcp"%>

<asp:Content ID="PageHead" ContentPlaceHolderID="PlaceHolderAdditionalPageHead" runat="server" />
<asp:Content ID="PageTitle" ContentPlaceHolderID="PlaceHolderPageTitle" runat="server">LDAPCP Configuration</asp:Content>
<asp:Content ID="PageTitleInTitleArea" ContentPlaceHolderID="PlaceHolderPageTitleInTitleArea" runat="server">
<%= String.Format("LDAPCP v{0} - <a href=\"http://www.LDAPCP.com\" target=\"_blank\">LDAPCP.com</a>", FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(LDAPCP)).Location).FileVersion) %>
</asp:Content>
<asp:Content ID="Main" ContentPlaceHolderID="PlaceHolderMain" runat="server">
    <table border="0" cellspacing="0" cellpadding="0" width="100%">
        <Ldapcp:GlobalSettings ID="LdapcpGlobalSettings" Runat="server" ClaimsProviderName="LDAPCP" PersistedObjectName="<%# Constants.LDAPCPCONFIG_NAME %>" PersistedObjectID="<%# Constants.LDAPCPCONFIG_ID %>" />
    </table>
</asp:Content>
