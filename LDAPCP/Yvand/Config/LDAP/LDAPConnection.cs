using Microsoft.SharePoint.Administration;
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Yvand.Config
{
    public class LDAPConnection : SPAutoSerializingObject
    {
        public Guid Identifier
        {
            get => Id;
            set => Id = value;
        }
        [Persisted]
        private Guid Id = Guid.NewGuid();

        /// <summary>
        /// LDAP Path of the connection LDAP://contoso.local:port/DC=contoso,DC=local
        /// </summary>
        public string LDAPPath  // Also required as a property for ASP.NET server side controls in admin pages.
        {
            get => Path;
            set => Path = value;
        }
        [Persisted]
        private string Path;

        public string LDAPUsername
        {
            get => Username;
            set => Username = value;
        }
        [Persisted]
        private string Username;

        public string LDAPPassword
        {
            get => Password;
            set => Password = value;
        }
        [Persisted]
        private string Password;

        public string AdditionalMetadata
        {
            get => Metadata;
            set => Metadata = value;
        }
        [Persisted]
        private string Metadata;

        public AuthenticationTypes AuthenticationSettings
        {
            get => AuthenticationTypes;
            set => AuthenticationTypes = value;
        }
        [Persisted]
        private AuthenticationTypes AuthenticationTypes;

        public bool UseSPServerConnectionToAD
        {
            get => UserServerDirectoryEntry;
            set => UserServerDirectoryEntry = value;
        }
        [Persisted]
        private bool UserServerDirectoryEntry;

        /// <summary>
        /// If true: this LDAPConnection will be be used for augmentation
        /// </summary>
        public bool EnableAugmentation  // Also required as a property for ASP.NET server side controls in admin pages.
        {
            get => AugmentationEnabled;
            set => AugmentationEnabled = value;
        }
        [Persisted]
        private bool AugmentationEnabled;

        /// <summary>
        /// If true: get group membership with UserPrincipal.GetAuthorizationGroups()
        /// If false: get group membership with LDAP queries
        /// </summary>
        public bool GetGroupMembershipUsingDotNetHelpers    // Also required as a property for ASP.NET server side controls in admin pages.
        {
            get => GetGroupMembershipAsADDomain;
            set => GetGroupMembershipAsADDomain = value;
        }
        [Persisted]
        private bool GetGroupMembershipAsADDomain = true;

        /// <summary>
        /// Contains the name of LDAP attributes where membership of users is stored
        /// </summary>
        public string[] GroupMembershipLDAPAttributes
        {
            get => GroupMembershipAttributes;
            set => GroupMembershipAttributes = value;
        }
        [Persisted]
        private string[] GroupMembershipAttributes = new string[] { "memberOf", "uniquememberof" };

        /// <summary>
        /// DirectoryEntry used to make LDAP queries
        /// </summary>
        public DirectoryEntry Directory
        {
            get => _Directory;
            set => _Directory = value;
        }
        private DirectoryEntry _Directory;

        /// <summary>
        /// LDAP filter
        /// </summary>
        public string Filter
        {
            get => _Filter;
            set => _Filter = value;
        }
        private string _Filter;

        /// <summary>
        /// Domain name, for example "contoso"
        /// </summary>
        public string DomainName
        {
            get => _DomainName;
            set => _DomainName = value;
        }
        private string _DomainName;

        /// <summary>
        /// Fully qualified domain name, for example "contoso.local"
        /// </summary>
        public string DomainFQDN
        {
            get => _DomainFQDN;
            set => _DomainFQDN = value;
        }
        private string _DomainFQDN;

        /// <summary>
        /// Root container to connect to, for example "DC=contoso,DC=local"
        /// </summary>
        public string RootContainer
        {
            get => _RootContainer;
            set => _RootContainer = value;
        }
        private string _RootContainer;

        public LDAPConnection()
        {
        }

        /// <summary>
        /// Returns a copy of the current object. This copy does not have any member of the base SharePoint base class set
        /// </summary>
        /// <returns></returns>
        internal LDAPConnection CopyConfiguration()
        {
            LDAPConnection copy = new LDAPConnection();
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
