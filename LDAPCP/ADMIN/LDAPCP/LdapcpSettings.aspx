<%@ Assembly Name="$SharePoint.Project.AssemblyFullName$" %>
<%@ Page Language="C#" AutoEventWireup="true" Inherits="Microsoft.SharePoint.WebControls.LayoutsPageBase" MasterPageFile="~/_admin/admin.master" %>
<%@ Register TagPrefix="Ldapcp" TagName="GlobalSettings" src="GlobalSettings.ascx" %>
<%@ Import Namespace="ldapcp" %>
<%@ Import Namespace="System.Diagnostics" %>
<%@ Import Namespace="System.Reflection" %>

<asp:Content ID="PageHead" ContentPlaceHolderID="PlaceHolderAdditionalPageHead" runat="server" />
<asp:Content ID="PageTitle" ContentPlaceHolderID="PlaceHolderPageTitle" runat="server">LDAPCP Configuration</asp:Content>
<asp:Content ID="PageTitleInTitleArea" ContentPlaceHolderID="PlaceHolderPageTitleInTitleArea" runat="server">
<%= String.Format("LDAPCP {0} - <a href=\"{1}\" target=\"_blank\">LDAPCP.com</a>", FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(LDAPCP)).Location).FileVersion, ClaimsProviderConstants.PUBLICSITEURL) %>
</asp:Content>
<asp:Content ID="Main" ContentPlaceHolderID="PlaceHolderMain" runat="server">
    <table border="0" cellspacing="0" cellpadding="0" width="100%">
        <Ldapcp:GlobalSettings ID="LdapcpGlobalSettings" Runat="server" ClaimsProviderName="LDAPCP" PersistedObjectName="<%# ClaimsProviderConstants.CONFIG_NAME %>" PersistedObjectID="<%# ClaimsProviderConstants.CONFIG_ID %>" />
    </table>
</asp:Content>
