using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Utilities;
using System;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Reflection;
using System.Text.RegularExpressions;
using Yvand.LdapClaimsProvider.Logging;

namespace Yvand.LdapClaimsProvider.Configuration
{
    public class DirectoryConnection : SPAutoSerializingObject
    {
        public Guid Identifier
        {
            get => Id;
            set => Id = value;
        }
        [Persisted]
        private Guid Id = Guid.NewGuid();

        /// <summary>
        /// LDAP _LdapPath of the connection LDAP://contoso.local:port/DC=contoso,DC=local
        /// </summary>
        public string LdapPath  // Also required as a property for ASP.NET server side controls in admin pages.
        {
            get => _LdapPath;
            set => _LdapPath = value;
        }
        [Persisted]
        private string _LdapPath;

        public string Username
        {
            get => _Username;
            set => _Username = value;
        }
        [Persisted]
        private string _Username;

        public string Password
        {
            get => _Password;
            set => _Password = value;
        }
        [Persisted]
        private string _Password;

        public string AdditionalMetadata
        {
            get => Metadata;
            set => Metadata = value;
        }
        [Persisted]
        private string Metadata;

        public AuthenticationTypes AuthenticationType
        {
            get => _AuthenticationSettings;
            set => _AuthenticationSettings = value;
        }
        [Persisted]
        private AuthenticationTypes _AuthenticationSettings;

        public bool UseDefaultADConnection
        {
            get => _UseDefaultADConnection;
            set => _UseDefaultADConnection = value;
        }
        [Persisted]
        private bool _UseDefaultADConnection;

        /// <summary>
        /// If true: this LDAPConnection will be be used for augmentation
        /// </summary>
        public bool EnableAugmentation  // Also required as a property for ASP.NET server side controls in admin pages.
        {
            get => _EnableAugmentation;
            set => _EnableAugmentation = value;
        }
        [Persisted]
        private bool _EnableAugmentation = true;

        /// <summary>
        /// If true: get group membership with UserPrincipal.GetAuthorizationGroups()
        /// If false: get group membership with LDAP queries
        /// </summary>
        public bool GetGroupMembershipUsingDotNetHelpers    // Also required as a property for ASP.NET server side controls in admin pages.
        {
            get => _GetGroupMembershipUsingDotNetHelpers;
            set => _GetGroupMembershipUsingDotNetHelpers = value;
        }
        [Persisted]
        private bool _GetGroupMembershipUsingDotNetHelpers = true;

        /// <summary>
        /// Contains the name of LDAP attributes where membership of users is stored
        /// </summary>
        public string[] GroupMembershipLdapAttributes
        {
            get => _GroupMembershipLdapAttributes;
            set => _GroupMembershipLdapAttributes = value;
        }
        [Persisted]
        private string[] _GroupMembershipLdapAttributes = new string[] { "memberOf", "uniquememberof" };

        /// <summary>
        /// DirectoryEntry used to make LDAP queries
        /// </summary>
        public DirectoryEntry LdapEntry
        {
            get
            {
                if (this._LdapEntry != null)
                {
                    return this._LdapEntry;
                }

                this._LdapEntry = GetDirectoryEntry();
                if (this.UseDefaultADConnection)
                {
                    // Property LDAPConnection.AuthenticationType must be set, in order to build the PrincipalContext correctly in GetGroupsFromActiveDirectory()
                    this.AuthenticationType = this._LdapEntry.AuthenticationType;
                }
                this._LdapEntry.Disposed += _DirectoryConnection_Disposed;
                return this._LdapEntry;
            }
        }

        private void _DirectoryConnection_Disposed(object sender, EventArgs e)
        {
            _LdapEntry = null;
        }

        private DirectoryEntry _LdapEntry;

        public bool InitializationSuccessful { get; private set; } = false;

        /// <summary>
        /// Domain name, for example "contoso"
        /// </summary>
        public string DomainName { get; private set; }

        /// <summary>
        /// Fully qualified domain name, for example "contoso.local"
        /// </summary>
        public string DomainFQDN { get; private set; }

        /// <summary>
        /// Root container to connect to, for example "DC=contoso,DC=local"
        /// </summary>
        public string DomaindistinguishedName { get; private set; }

        public DirectoryConnection()
        {
        }

        public DirectoryConnection(bool useDefaultADConnection)
        {
            this.UseDefaultADConnection = useDefaultADConnection;
            if (useDefaultADConnection == true)
            {
                this.EnableAugmentation = true;
                this.GetGroupMembershipUsingDotNetHelpers = true;
            }
        }

        public bool Initialize()
        {
            if (InitializationSuccessful)
            {
                return InitializationSuccessful;
            }

            // This block does LDAP operations
            using (new SPMonitoredScope($"[{LDAPCPSE.ClaimsProviderName}] Get domain names / root container information about LDAP server \"{this.LdapEntry.Path}\"", 2000))
            {
                // Do these operations using the application pool account privileges to avoid a DirectoryServicesCOMException due to lack of permissions
                SPSecurity.RunWithElevatedPrivileges(delegate ()
                {
                    try
                    {
#if DEBUGx
                        this.AuthenticationType = AuthenticationTypes.None;
                        Logger.LogDebug($"Hardcoded property DirectoryEntry.AuthenticationType to {AuthenticationType} for \"{this.LdapEntry.Path}\"");
#endif
                        // Method PropertyCollection.Contains("distinguishedName") does a LDAP bind
                        // In AD LDS: property "distinguishedName" = "CN=LDSInstance2,DC=ADLDS,DC=local", properties "name" and "cn" = "LDSInstance2"
                        if (this.LdapEntry.Properties.Contains("distinguishedName"))
                        {
                            this.DomaindistinguishedName = this.LdapEntry.Properties["distinguishedName"].Value.ToString();
                            string domainName, domainFQDN;
                            Utils.GetDomainInformation(this.DomaindistinguishedName, out domainName, out domainFQDN);
                            this.DomainName = domainName;
                            this.DomainFQDN = domainFQDN;
                        }
                        else if (this.LdapEntry.Properties.Contains("name"))
                        {
                            this.DomainName = this.LdapEntry.Properties["name"].Value.ToString();
                        }
                        else if (this.LdapEntry.Properties.Contains("cn"))
                        {
                            // Tivoli stores domain name in property "cn" (properties "distinguishedName" and "name" don't exist)
                            this.DomainName = this.LdapEntry.Properties["cn"].Value.ToString();
                        }
                        InitializationSuccessful = true;
                    }
                    catch (DirectoryServicesCOMException ex)
                    {
                        Logger.LogException("", $"while getting domain names information for LDAP connection {this.LdapEntry.Path} (DirectoryServicesCOMException)", TraceCategory.Configuration, ex);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException("", $"while getting domain names information for LDAP connection {this.LdapEntry.Path} (Exception)", TraceCategory.Configuration, ex);
                    }
                });
            }
            return InitializationSuccessful;
        }

        /// <summary>
        /// Returns a copy of the current object. This copy does not have any member of the base SharePoint base class set
        /// </summary>
        /// <returns></returns>
        internal DirectoryConnection CopyConfiguration()
        {
            DirectoryConnection copy = new DirectoryConnection();
            // Copy non-inherited public properties
            PropertyInfo[] propertiesToCopy = this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (PropertyInfo property in propertiesToCopy)
            {
                if (property.CanWrite)
                {
                    object value = property.GetValue(this);
                    if (value != null)
                        property.SetValue(copy, value);
                }
            }
            return copy;
        }

        public DirectoryEntry GetDirectoryEntry(string customLdapPath = "")
        {
            DirectoryEntry directoryEntry;
            if (!this.UseDefaultADConnection)
            {
                string ldapPath = String.IsNullOrWhiteSpace(customLdapPath) ?
                    this.LdapPath :
                    customLdapPath;
                directoryEntry = new DirectoryEntry(ldapPath, this.Username, this.Password, this.AuthenticationType);
            }
            else
            {
                directoryEntry = Domain.GetComputerDomain().GetDirectoryEntry();
                if (!String.IsNullOrWhiteSpace(customLdapPath))
                {
                    directoryEntry.Path = customLdapPath;
                }
            }
            return directoryEntry;
        }

        /// <summary>
        /// Returns only the LDAP base (without the distinguished name) from the value in LdapEntry.Path, in format LDAP://contoso.local:636/DC=contoso,DC=local
        /// </summary>
        /// <returns>The LDAP path in format: LDAP://contoso.local:636</returns>
        public string GetLdapBasePath()
        {
            if (LdapEntry == null || String.IsNullOrWhiteSpace(LdapEntry.Path))
            {
                return String.Empty;
            }
            string patternDetectDistinguishedName = @"\/\w\w=";
            Match m = Regex.Match(LdapEntry.Path, patternDetectDistinguishedName, RegexOptions.IgnoreCase);
            if (m.Success)
            {
                return LdapEntry.Path.Substring(0, m.Index);
            }
            else
            {
                // Remove the trailing '/' if it is present
                return LdapEntry.Path.TrimEnd('/');
            }
        }
    }
}
