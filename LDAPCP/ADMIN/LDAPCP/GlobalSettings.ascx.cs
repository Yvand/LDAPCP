using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
using Microsoft.SharePoint.WebControls;
using System;
using System.Collections.Generic;
using System.Data;
using System.DirectoryServices;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.UI.WebControls;

namespace ldapcp.ControlTemplates
{
  public partial class GlobalSettings : LdapcpUserControl
  {
    public bool ShowValidateSection
    {
      get { return ValidateSection.Visible; }
      set { ValidateSection.Visible = ValidateTopSection.Visible = value; }
    }

    public bool ShowCurrentLdapConnectionSection
    {
      get { return CurrentLdapConnectionSection.Visible; }
      set { CurrentLdapConnectionSection.Visible = value; }
    }

    public bool ShowNewLdapConnectionSection
    {
      get { return NewLdapConnectionSection.Visible; }
      set { NewLdapConnectionSection.Visible = value; }
    }

    public bool ShowAugmentationSection
    {
      get { return AugmentationSection.Visible; }
      set { AugmentationSection.Visible = value; }
    }

    string TextErrorNoGroupClaimType = "There is no claim type associated with an entity type 'FormsRole' or 'SecurityGroup'.";
    string TextErrorLDAPFieldsMissing = "Some mandatory fields are missing.";
    string TextErrorTestLdapConnection = "Unable to connect to LDAP for following reason:<br/>{0}<br/>It may be expected if w3wp process of central admin has intentionally no access to LDAP server.";
    string TextErrorNetBiosDomainName = "Unable to resolve NetBios domain name for following reason:<br/>{0}<br/>";
    string TextConnectionSuccessful = "Connection successful.";
    string TextSharePointDomain = "Connect to SharePoint domain";
    string TextUpdateAdditionalLdapFilterOk = "LDAP filter was successfully applied to all LDAP attributes of class 'user'.";

    protected void Page_Load(object sender, EventArgs e)
    {
      ViewState["IsDefaultADConnectionCreated"] = false;
      ViewState["ForceCheckCustomLdapConnection"] = false;

      if (ValidatePrerequisite() != ConfigStatus.AllGood)
      {
        this.LabelErrorMessage.Text = base.MostImportantError;
        this.BtnOK.Enabled = this.BtnOKTop.Enabled = false;
        return;
      }

      if (!this.IsPostBack) Initialize();

      if (ViewState["LDAPpwd"] != null)
      {
        ViewState["LDAPpwd"] = TxtLdapPassword.Text;
        TxtLdapPassword.Attributes.Add("value", ViewState["LDAPpwd"].ToString());
      }
      else
      {
        ViewState["LDAPpwd"] = TxtLdapPassword.Text;
      }
    }

    public void Initialize()
    {
      PopulateLdapConnectionGrid();
      PopulateCblAuthenticationTypes();
      InitializeAugmentation();
      InitializeGeneralSettings();
    }

    private void InitializeAugmentation()
    {
      IEnumerable<AttributeHelper> potentialGroupClaimTypes = PersistedObject.AttributesListProp.Where(x => x.ClaimEntityType == SPClaimEntityTypes.FormsRole || x.ClaimEntityType == SPClaimEntityTypes.SecurityGroup);
      if (potentialGroupClaimTypes == null || potentialGroupClaimTypes.Count() == 0)
      {
        LabelErrorMessage.Text = TextErrorNoGroupClaimType;
        return;
      }

      foreach (var potentialGroup in potentialGroupClaimTypes)
      {
        DdlClaimTypes.Items.Add(potentialGroup.ClaimType);
      }

      ChkEnableAugmentation.Checked = PersistedObject.AugmentationEnabledProp;

      if (!String.IsNullOrEmpty(PersistedObject.AugmentationClaimTypeProp) && DdlClaimTypes.Items.FindByValue(PersistedObject.AugmentationClaimTypeProp) != null)
        DdlClaimTypes.SelectedValue = PersistedObject.AugmentationClaimTypeProp;

      // Initialize grid for LDAP connections
      var spDomainCoco = PersistedObject.LDAPConnectionsProp.FirstOrDefault(x => x.UserServerDirectoryEntry);
      if (spDomainCoco != null) spDomainCoco.Path = TextSharePointDomain;

      GridLdapConnections.DataSource = PersistedObject.LDAPConnectionsProp;
      GridLdapConnections.DataKeyNames = new string[] { "IdProp" };
      GridLdapConnections.DataBind();
    }

    void PopulateLdapConnectionGrid()
    {
      if (PersistedObject.LDAPConnectionsProp != null)
      {
        PropertyCollectionBinder pcb = new PropertyCollectionBinder();
        foreach (LDAPConnection coco in PersistedObject.LDAPConnectionsProp)
        {
          if (coco.UserServerDirectoryEntry)
          {
            ViewState["IsDefaultADConnectionCreated"] = true;

            pcb.AddRow(coco.Id, TextSharePointDomain, "Process account");
          }
          else
          {
            pcb.AddRow(coco.Id, coco.Path, coco.Username);
          }
        }
        pcb.BindGrid(grdLDAPConnections);
      }
    }

    void PopulateCblAuthenticationTypes()
    {
      Dictionary<int, string> authenticationTypesDS = EnumToList(typeof(AuthenticationTypes));
      foreach (KeyValuePair<int, string> authNType in authenticationTypesDS)
      {
        ListItem authNTypeItem = new ListItem();
        authNTypeItem.Text = authNType.Value;
        authNTypeItem.Value = authNType.Key.ToString();
        CblAuthenticationTypes.Items.Add(authNTypeItem);
      }
    }

    private void InitializeGeneralSettings()
    {
      this.ChkIdentityShowAdditionalAttribute.Checked = PersistedObject.DisplayLdapMatchForIdentityClaimTypeProp;
      if (String.IsNullOrEmpty(IdentityClaim.LDAPAttributeToDisplayProp))
      {
        this.RbIdentityDefault.Checked = true;
      }
      else
      {
        this.RbIdentityCustomLDAP.Checked = true;
        this.TxtLdapAttributeToDisplay.Text = IdentityClaim.LDAPAttributeToDisplayProp;
      }

      this.ChkAlwaysResolveUserInput.Checked = PersistedObject.AlwaysResolveUserInputProp;
      this.ChkFilterEnabledUsersOnly.Checked = PersistedObject.FilterEnabledUsersOnlyProp;
      this.ChkFilterSecurityGroupsOnly.Checked = PersistedObject.FilterSecurityGroupsOnlyProp;
      this.ChkFilterExactMatchOnly.Checked = PersistedObject.FilterExactMatchOnlyProp;
      this.txtTimeout.Text = PersistedObject.TimeoutProp.ToString();

      // Deprecated options that are not shown anymore in LDAPCP configuration page
      //this.ChkAddWildcardInFront.Checked = PersistedObject.AddWildcardInFrontOfQueryProp;
      //this.TxtPickerEntityGroupName.Text = PersistedObject.PickerEntityGroupNameProp;
    }

    public override bool UpdatePersistedObjectProperties(bool commitChanges)
    {
      if (ValidatePrerequisite() != ConfigStatus.AllGood) return false;
      UpdateLdapSettings();
      UpdateAugmentationSettings();
      UpdateGeneralSettings();
      if (commitChanges) CommitChanges();
      return true;
    }

    private void UpdateGeneralSettings()
    {
      // Handle identity claim type
      PersistedObject.DisplayLdapMatchForIdentityClaimTypeProp = this.ChkIdentityShowAdditionalAttribute.Checked;
      if (this.RbIdentityCustomLDAP.Checked)
      {
        IdentityClaim.LDAPAttributeToDisplayProp = this.TxtLdapAttributeToDisplay.Text;
      }
      else
      {
        IdentityClaim.LDAPAttributeToDisplayProp = String.Empty;
      }

      PersistedObject.AlwaysResolveUserInputProp = this.ChkAlwaysResolveUserInput.Checked;
      PersistedObject.FilterEnabledUsersOnlyProp = this.ChkFilterEnabledUsersOnly.Checked;
      PersistedObject.FilterSecurityGroupsOnlyProp = this.ChkFilterSecurityGroupsOnly.Checked;
      PersistedObject.FilterExactMatchOnlyProp = this.ChkFilterExactMatchOnly.Checked;
      // Deprecated options that are not shown anymore in LDAPCP configuration page
      //PersistedObject.AddWildcardInFrontOfQuery = this.ChkAddWildcardInFront.Checked;
      //PersistedObject.PickerEntityGroupName = this.TxtPickerEntityGroupName.Text;

      int timeOut;
      if (!Int32.TryParse(this.txtTimeout.Text, out timeOut) || timeOut < 0)
        timeOut = Constants.LDAPCPCONFIG_TIMEOUT; //set to default if unable to parse
      PersistedObject.TimeoutProp = timeOut;
    }

    private void UpdateLdapSettings()
    {
      foreach (GridViewRow item in GridLdapConnections.Rows)
      {
        CheckBox chkAugEn = (CheckBox)item.FindControl("ChkAugmentationEnableOnCoco");
        TextBox txtId = (TextBox)item.FindControl("IdPropHidden");

        PersistedObject.LDAPConnectionsProp.First(x => x.Id == new Guid(txtId.Text)).AugmentationEnabled = chkAugEn.Checked;
      }
    }

    private void UpdateAugmentationSettings()
    {
      PersistedObject.AugmentationEnabledProp = ChkEnableAugmentation.Checked;
      PersistedObject.AugmentationClaimTypeProp = DdlClaimTypes.SelectedValue.Equals("none", StringComparison.InvariantCultureIgnoreCase) ? String.Empty : DdlClaimTypes.SelectedValue;
    }

    protected void BtnOK_Click(Object sender, EventArgs e)
    {
      if (ValidatePrerequisite() != ConfigStatus.AllGood) return;
      if (UpdatePersistedObjectProperties(true)) Response.Redirect("/Security.aspx", false);
      else LabelErrorMessage.Text = MostImportantError;
    }

    protected void BtnResetLDAPCPConfig_Click(Object sender, EventArgs e)
    {
      ResetConfiguration();
    }

    protected virtual void ResetConfiguration()
    {
      LDAPCPConfig.DeleteLDAPCPConfig();
      Response.Redirect(Request.RawUrl, false);
    }

    protected void BtnUpdateAdditionalUserLdapFilter_Click(Object sender, EventArgs e)
    {
      UpdateAdditionalUserLdapFilter();
    }

    void UpdateAdditionalUserLdapFilter()
    {
      if (PersistedObject == null) return;
      foreach (var userAttr in this.PersistedObject.AttributesListProp.FindAll(x => x.ClaimEntityType == SPClaimEntityTypes.User || x.CreateAsIdentityClaim))
      {
        userAttr.AdditionalLDAPFilterProp = this.TxtAdditionalUserLdapFilter.Text;
      }
      this.CommitChanges();
      LabelUpdateAdditionalLdapFilterOk.Text = this.TextUpdateAdditionalLdapFilterOk;
    }

    protected void BtnTestLdapConnection_Click(Object sender, EventArgs e)
    {
      this.ValidateLdapConnection();
    }

    private TableCell GetTableCell(string Value)
    {
      TableCell tc = new TableCell();
      tc.Text = Value;
      return tc;
    }

    protected void BtnAddLdapConnection_Click(object sender, EventArgs e)
    {
      AddLdapConnection();
    }

    /// <summary>
    /// Add new LDAP connection to collection in persisted object
    /// </summary>
    void AddLdapConnection()
    {
      if (ValidatePrerequisite() != ConfigStatus.AllGood) return;

      if (this.RbUseCustomConnection.Checked && (this.TxtLdapConnectionString.Text == String.Empty || this.TxtLdapUsername.Text == String.Empty || this.TxtLdapPassword.Text == String.Empty))
      {
        this.LabelErrorTestLdapConnection.Text = TextErrorLDAPFieldsMissing;
        return;
      }

      if (this.RbUseServerDomain.Checked)
      {
        PersistedObject.LDAPConnectionsProp.Add(new LDAPConnection { UserServerDirectoryEntry = true });
      }
      else
      {
        AuthenticationTypes authNType = GetSelectedAuthenticationTypes(true);
        PersistedObject.LDAPConnectionsProp.Add(
            new LDAPConnection
            {
              UserServerDirectoryEntry = false,
              Path = this.TxtLdapConnectionString.Text,
              Username = this.TxtLdapUsername.Text,
              Password = this.TxtLdapPassword.Text,
              AuthenticationTypes = authNType,
              ResolvedNetBiosDomainNames = ResolveNetBiosDomainName()
            }
        );
      }

      // Update object in database
      CommitChanges();
      LdapcpLogging.Log(
             String.Format("Added a new LDAP connection in PersistedObject {0}", Constants.LDAPCPCONFIG_NAME),
             TraceSeverity.Medium,
             EventSeverity.Information,
             LdapcpLogging.Categories.Configuration);

      PopulateLdapConnectionGrid();
      InitializeAugmentation();
      ViewState["LDAPpwd"] = String.Empty;
      TxtLdapPassword.Attributes.Remove("value");
      this.TxtLdapUsername.Text = this.TxtLdapPassword.Text = String.Empty;
      this.TxtLdapConnectionString.Text = "LDAP://";
    }

    protected void ValidateLdapConnection()
    {
      ViewState["ForceCheckCustomLdapConnection"] = true;
      if (this.TxtLdapConnectionString.Text == String.Empty || this.TxtLdapPassword.Text == String.Empty || this.TxtLdapUsername.Text == String.Empty)
      {
        this.LabelErrorTestLdapConnection.Text = TextErrorLDAPFieldsMissing;
        return;
      }

      DirectoryEntry de = null;
      DirectorySearcher deSearch = new DirectorySearcher();
      try
      {
        AuthenticationTypes authNTypes = GetSelectedAuthenticationTypes(false);
        de = new DirectoryEntry(this.TxtLdapConnectionString.Text, this.TxtLdapUsername.Text, this.TxtLdapPassword.Text, authNTypes);
        deSearch.SearchRoot = de;
        deSearch.FindOne();

        this.LabelTestLdapConnectionOK.Text = TextConnectionSuccessful;
        ResolveNetBiosDomainName(de, TxtLdapUsername.Text, TxtLdapPassword.Text, authNTypes);

      }
      catch (Exception ex)
      {
        LdapcpLogging.LogException(LDAPCP._ProviderInternalName, "while testing LDAP connection", LdapcpLogging.Categories.Configuration, ex);
        this.LabelErrorTestLdapConnection.Text = String.Format(TextErrorTestLdapConnection, ex.Message);
      }
      finally
      {
        if (deSearch != null) deSearch.Dispose();
        if (de != null) de.Dispose();
      }

      // Required to set radio buttons of LDAP connections correctly in UI
      PopulateLdapConnectionGrid();

    }

    protected List<string> ResolveNetBiosDomainName()
    {
      var results = new List<string>();
      if (TxtLdapConnectionString.Text == String.Empty || TxtLdapPassword.Text == String.Empty || TxtLdapUsername.Text == String.Empty)
      {
        LabelErrorTestLdapConnection.Text = TextErrorLDAPFieldsMissing;
        return null;
      }

      DirectoryEntry de = null;
      
      try
      {
        AuthenticationTypes authNTypes = GetSelectedAuthenticationTypes(false);
        de = new DirectoryEntry(this.TxtLdapConnectionString.Text, this.TxtLdapUsername.Text, this.TxtLdapPassword.Text, authNTypes);
        results = ResolveNetBiosDomainName(de, TxtLdapUsername.Text, TxtLdapPassword.Text, authNTypes);
      }
      catch (Exception ex)
      {
        LdapcpLogging.LogException(LDAPCP._ProviderInternalName, "while testing LDAP connection", LdapcpLogging.Categories.Configuration, ex);
        LabelErrorTestLdapConnection.Text = String.Format(TextErrorTestLdapConnection, ex.Message);
      }
      finally
      {
        if (de != null) de.Dispose();
      }

      return results;
    }

    protected List<string> ResolveNetBiosDomainName(DirectoryEntry directoryEntry, string username, string password, AuthenticationTypes authenticationType)
    {
      var netbiosDomainNames = new List<string>();
      var distinguishedName = String.Empty;

      DirectorySearcher searcher = new DirectorySearcher();
      try
      {
        // TODO: LDAP connection string can be LDAPS as well
        var directoryPath = directoryEntry.Path;
        var provider = directoryPath.Split(new[] { @"://" }, StringSplitOptions.None)[0];
        var directory = directoryPath.Split(new[] { @"://" }, StringSplitOptions.None)[1];
        var dnsDomainName = string.Empty;

        dnsDomainName = ResolveDomainFromDirectoryPath(directory);

        searcher = ResolveRootDirectorySearcher(directoryEntry, distinguishedName, provider, dnsDomainName, username, password, authenticationType);
        searcher.SearchScope = SearchScope.OneLevel;
        searcher.PropertiesToLoad.Add("netbiosname");
        searcher.Filter = "netBIOSName=*";
        SearchResultCollection results = null;
        
        results = searcher.FindAll();

        if (results.Count > 0)
        {
          foreach (SearchResult res in results)
          {
            var netbiosDomainName = res.Properties["netbiosname"][0].ToString();
            if (!netbiosDomainNames.Contains(netbiosDomainName))
            {
              netbiosDomainNames.Add(netbiosDomainName);
            }
          }
        }

        LabelTestLdapConnectionOK.Text += String.Format("<br>Resolved NetBios Domain Name/s: {0}<br>", String.Join("<br>", netbiosDomainNames.Select(x => x).ToArray()));
      }
      catch (Exception ex)
      {
        LdapcpLogging.LogException(LDAPCP._ProviderInternalName, "in ResolveNetBiosDomainName", LdapcpLogging.Categories.Configuration, ex);
        LabelErrorTestLdapConnection.Text = String.Format(TextErrorNetBiosDomainName, ex.Message);
      }
      finally
      {
        searcher.Dispose();
      }

      return netbiosDomainNames;
    }

    private DirectorySearcher ResolveRootDirectorySearcher(DirectoryEntry directoryEntry, string distinguishedName, string provider, string dnsDomainName, string username, string password, AuthenticationTypes authenticationType )
    {
      DirectoryEntry searchRoot = null;

      if (directoryEntry.Properties["distinguishedName"].Value != null)
      {
        distinguishedName = directoryEntry.Properties["distinguishedName"].Value.ToString();
      }

      if (distinguishedName.ToUpper().Contains("OU="))
      {
        // distinguished name contains OU (Organizational Units), so we need to parse to only have DC (Domain Components) elements in our DirectoryEntry path
        var domainComponents = ResolveDnsDomainName(distinguishedName).Split('.');
        distinguishedName = string.Empty;
        var componentCount = 1;
        foreach (var component in domainComponents)
        {
          distinguishedName += "DC=" + component + (componentCount < domainComponents.Length ? "," : "");
          componentCount++;
        }
      }

      // Every AD forest does have Configuration Node. Here is how we target it e.g. LDAP://contoso.com/cn=Partitions,cn=Configuration,dn=contoso,dn=com
      searchRoot = new DirectoryEntry(String.Format("{0}://{1}/cn=Partitions,cn=Configuration,{2}", provider, dnsDomainName,distinguishedName), username, password, authenticationType);

      var searcher = new DirectorySearcher(searchRoot);
      return searcher;
    }

    private string ResolveDomainFromDirectoryPath(string directory)
    {
      var dnsDomainName = String.Empty;

      if (directory.Contains("/"))
      {
          var domainConfiguration = directory.Split('/')[0];
        // example for validating connection string similar to following: <domain>/ou=<some_value>,ou=<some_value>,dc=<subdomain>,dc=<domain>,dc=<ch>
        if (!IsValidDomain(domainConfiguration) && (domainConfiguration.Contains("DC") || (domainConfiguration.Contains("dc"))))
        {
          // it is not a domain name, resolve all DC (Domain Component) parameters as a valid domain and ignore all the rest
          dnsDomainName = ResolveDnsDomainName(domainConfiguration);
        }
        else
        {
          // it is valid domain name, extract it
          dnsDomainName = domainConfiguration;
        }
      }
      else
      {
        if (!IsValidDomain(directory))
        {
          // it is not a domain name, resolve all DC (Domain Component) parameters as a valid domain and ignore all the rest
          dnsDomainName = ResolveDnsDomainName(directory);
        }
        else
        {
          // it is valid domain name, extract it
          dnsDomainName = directory;
        }
      }
      return dnsDomainName;
    }

    private bool IsValidDomain(string directoryPath)
    {
      if (Regex.IsMatch(directoryPath, @" # Rev:2013-03-26
              # Match DNS host domain having one or more subdomains.
              # Top level domain subset taken from IANA.ORG. See:
              # http://data.iana.org/TLD/tlds-alpha-by-domain.txt
              ^                  # Anchor to start of string.
              (?!.{256})         # Whole domain must be 255 or less.
              (?:                # Group for one or more sub-domains.
                [a-z0-9]         # Either subdomain length from 2-63.
                [a-z0-9-]{0,61}  # Middle part may have dashes.
                [a-z0-9]         # Starts and ends with alphanum.
                \.               # Dot separates subdomains.
              | [a-z0-9]         # or subdomain length == 1 char.
                \.               # Dot separates subdomains.
              )+                 # One or more sub-domains.
              (?:                # Top level domain alternatives.
                [a-z]{2}         # Either any 2 char country code,
              | AERO|ARPA|ASIA|BIZ|CAT|COM|COOP|EDU|  # or TLD 
                GOV|INFO|INT|JOBS|MIL|MOBI|MUSEUM|    # from list.
                NAME|NET|ORG|POST|PRO|TEL|TRAVEL  # IANA.ORG
              )                  # End group of TLD alternatives.
              $                  # Anchor to end of string.",
        RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace))
      {
        return true;
      }

      return false;
    }

    private string ResolveDnsDomainName(string configuration)
    {
      string pattern = @"(s*)dc=([^,]+)";
      string output = String.Empty;

      MatchCollection matches = Regex.Matches(configuration, pattern, RegexOptions.IgnoreCase);

      var matchCount = 1;
      foreach (Match match in matches)
      {
        output += match.Value.Split(new[] { "Dc=", "DC=", "dc=", "dC=" }, StringSplitOptions.None)[1] + (matchCount < matches.Count ? "," : "");
        matchCount++;
      }

      if (output.Contains(","))
      {
        var components = output.Split(',');
        var domain = String.Empty;

        var componentCount = 1;
        foreach (var component in components)
        {
          domain += component + (componentCount < components.Length ? "." : "");
          componentCount++;
        }

        if (!String.IsNullOrEmpty(domain))
        {
          output = domain;
        }
      }

      return output;
    }

    protected void grdLDAPConnections_RowDeleting(object sender, GridViewDeleteEventArgs e)
    {
      if (ValidatePrerequisite() != ConfigStatus.AllGood) return;
      if (PersistedObject.LDAPConnectionsProp == null) return;
      GridViewRow rowToDelete = grdLDAPConnections.Rows[e.RowIndex];

      Guid Id = new Guid(rowToDelete.Cells[0].Text);
      PersistedObject.LDAPConnectionsProp.Remove(PersistedObject.LDAPConnectionsProp.Find(x => x.Id == Id));

      // Update object in database
      CommitChanges();
      LdapcpLogging.Log(
              String.Format("Removed a LDAP connection in PersistedObject {0}", Constants.LDAPCPCONFIG_NAME),
              TraceSeverity.Medium,
              EventSeverity.Information,
              LdapcpLogging.Categories.Configuration);

      InitializeAugmentation();
      PopulateLdapConnectionGrid();
    }

    public static Dictionary<int, string> EnumToList(Type t)
    {
      Dictionary<int, string> list = new Dictionary<int, string>();
      foreach (var v in Enum.GetValues(t))
      {
        string name = Enum.GetName(t, (int)v);
        // Encryption and SecureSocketsLayer have same value and it will violate uniqueness of key if attempt to add both to Dictionary
        if (String.Equals(name, "Encryption", StringComparison.InvariantCultureIgnoreCase) && list.ContainsValue("Encryption")) continue;
        list.Add((int)v, name);
      }
      return list;
    }

    /// <summary>
    /// Parse checkbox list CblAuthenticationTypes to find authentication modes selected
    /// </summary>
    /// <returns></returns>
    AuthenticationTypes GetSelectedAuthenticationTypes(bool ClearSelection)
    {
      AuthenticationTypes authNTypes = 0;
      foreach (ListItem item in this.CblAuthenticationTypes.Items)
      {
        if (!item.Selected) continue;
        int selectedType;
        if (!Int32.TryParse(item.Value, out selectedType)) continue;
        authNTypes += selectedType;
        if (ClearSelection) item.Selected = false;
      }
      return authNTypes;
    }
  }

  public class PropertyCollectionBinder
  {
    protected DataTable PropertyCollection = new DataTable();
    public PropertyCollectionBinder()
    {
      PropertyCollection.Columns.Add("Id", typeof(Guid));
      PropertyCollection.Columns.Add("Path", typeof(string));
      PropertyCollection.Columns.Add("Username", typeof(string));
    }

    public void AddRow(Guid Id, string Path, string Username)
    {
      DataRow newRow = PropertyCollection.Rows.Add();
      newRow["Id"] = Id;
      newRow["Path"] = Path;
      newRow["Username"] = Username;
    }

    public void BindGrid(SPGridView grid)
    {
      grid.DataSource = PropertyCollection.DefaultView;
      grid.DataBind();
    }
  }
}
