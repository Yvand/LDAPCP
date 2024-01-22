using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Utilities;
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.DirectoryServices.Protocols;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Yvand.LdapClaimsProvider.Configuration
{
    public class LdapConnection : SPAutoSerializingObject
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
        public DirectoryEntry DirectoryConnection
        {
            get
            {
                if (this._DirectoryConnection != null)
                {
                    return this._DirectoryConnection;
                }

                if (!this.UseDefaultADConnection)
                {
                    this._DirectoryConnection = new DirectoryEntry(this.LdapPath, this.Username, this.Password, this.AuthenticationType);
                }
                else
                {
                    try
                    {
                        // Do these operations using the application pool account privileges to avoid COMException due to lack of permissions
                        SPSecurity.RunWithElevatedPrivileges(delegate ()
                        {
                            // This try block is to get domain name information about AD domain of current computer
                            // If this fails, execution should still continue as:
                            // - It will be attempted again in a different way in OperationContext.GetDomainInformation(), so it should be given a chance
                            // - It often (only) fails with COMException, which tend to occur only in some code path, but finally works depending on how LDAPCP is called
                            // - It's not essential, even though it can have serious impacts, for example, value of role claims miss the domain name
                            Domain computerDomain = Domain.GetComputerDomain();
                            this._DirectoryConnection = computerDomain.GetDirectoryEntry();

                            // Set properties LDAPConnection.DomainFQDN and LDAPConnection.DomainName here as a workaround to issue https://github.com/Yvand/LDAPCP/issues/87
                            this.DomainFQDN = computerDomain.Name;
                            this.DomainName = Utils.GetDomainName(this.DomainFQDN);

                            // Property LDAPConnection.AuthenticationType must be set, in order to build the PrincipalContext correctly in GetGroupsFromActiveDirectory()
                            this.AuthenticationType = this.DirectoryConnection.AuthenticationType;
                        });
                    }
                    catch (System.Runtime.InteropServices.COMException ex)
                    {
                        // Domain.GetDomain() may fail with the following error: System.Runtime.InteropServices.COMException: Retrieving the COM class factory for component with CLSID {080D0D78-F421-11D0-A36E-00C04FB950DC} failed due to the following error: 800703fa Illegal operation attempted on a registry key that has been marked for deletion. (Exception from HRESULT: 0x800703FA).
                        Logger.LogException("", $"while getting domain names information about AD domain of current computer (COMException)", TraceCategory.Configuration, ex);
                    }
                    catch (Exception ex)
                    {
                        // Domain.GetDomain() may fail with the following error: System.Runtime.InteropServices.COMException: Retrieving the COM class factory for component with CLSID {080D0D78-F421-11D0-A36E-00C04FB950DC} failed due to the following error: 800703fa Illegal operation attempted on a registry key that has been marked for deletion. (Exception from HRESULT: 0x800703FA).
                        Logger.LogException("", $"while getting domain names information about AD domain of current computer", TraceCategory.Configuration, ex);
                    }
                }
                this._DirectoryConnection.Disposed += _DirectoryConnection_Disposed;
                return this._DirectoryConnection;
            }
        }

        private void _DirectoryConnection_Disposed(object sender, EventArgs e)
        {
            _DirectoryConnection = null;
        }

        private DirectoryEntry _DirectoryConnection;

        /// <summary>
        /// LDAP filter
        /// </summary>
        //public string Filter { get; private set; }

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
        //public string RootContainer { get; private set; }

        /// <summary>
        /// Root container to connect to, for example "DC=contoso,DC=local"
        /// </summary>
        public string DomaindistinguishedName { get; private set; }

        public LdapConnection()
        {
        }

        public bool Initialize()
        {
            if (InitializationSuccessful)
            {
                return InitializationSuccessful;
            }



            // This block does LDAP operations
            using (new SPMonitoredScope($"[{LDAPCPSE.ClaimsProviderName}] Get domain names / root container information about LDAP server \"{this.DirectoryConnection.Path}\"", 2000))
            {
                try
                {
#if DEBUG
                    this.AuthenticationType = AuthenticationTypes.None;
                    Logger.LogDebug($"Hardcoded property DirectoryEntry.AuthenticationType to {AuthenticationType} for \"{this.DirectoryConnection.Path}\"");
#endif

                    // Method PropertyCollection.Contains("distinguishedName") does a LDAP bind
                    // In AD LDS: property "distinguishedName" = "CN=LDSInstance2,DC=ADLDS,DC=local", properties "name" and "cn" = "LDSInstance2"
                    if (this.DirectoryConnection.Properties.Contains("distinguishedName"))
                    {
                        DomaindistinguishedName = this.DirectoryConnection.Properties["distinguishedName"].Value.ToString();
                        string domainName;
                        string domainFQDN;
                        Utils.GetDomainInformation(DomaindistinguishedName, out domainName, out domainFQDN);
                        this.DomainName = domainName;
                        this.DomainFQDN = domainFQDN;
                    }
                    else if (this.DirectoryConnection.Properties.Contains("name"))
                    {
                        DomainName = this.DirectoryConnection.Properties["name"].Value.ToString();
                    }
                    else if (this.DirectoryConnection.Properties.Contains("cn"))
                    {
                        // Tivoli stores domain name in property "cn" (properties "distinguishedName" and "name" don't exist)
                        DomainName = this.DirectoryConnection.Properties["cn"].Value.ToString();
                    }
                    InitializationSuccessful = true;
                }
                catch (DirectoryServicesCOMException ex)
                {
                    Logger.LogException("", $"while getting domain names information for LDAP connection {this.DirectoryConnection.Path} (DirectoryServicesCOMException)", TraceCategory.Configuration, ex);
                }
                catch (Exception ex)
                {
                    Logger.LogException("", $"while getting domain names information for LDAP connection {this.DirectoryConnection.Path} (Exception)", TraceCategory.Configuration, ex);
                }
            }
            return InitializationSuccessful;
        }

        /// <summary>
        /// Returns a copy of the current object. This copy does not have any member of the base SharePoint base class set
        /// </summary>
        /// <returns></returns>
        internal LdapConnection CopyConfiguration()
        {
            LdapConnection copy = new LdapConnection();
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
    }
}
