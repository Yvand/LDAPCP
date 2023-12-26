using Microsoft.SharePoint.Administration;
using System;
using System.Collections.Generic;
using System.DirectoryServices;
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

        public AuthenticationTypes AuthenticationSettings
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
        private bool _EnableAugmentation;

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
        public DirectoryEntry DirectoryConnection { get; private set; }

        /// <summary>
        /// LDAP filter
        /// </summary>
        public string Filter { get; private set; }

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
        public string RootContainer { get; private set; }

        public LdapConnection()
        {
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
