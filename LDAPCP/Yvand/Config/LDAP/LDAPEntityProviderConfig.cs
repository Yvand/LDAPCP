using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yvand.Config
{
    public interface ILDAPSettings : IEntityProviderSettings
    {
        /// <summary>
        /// Gets the list of LDAP connections to use to get entities
        /// </summary>
        List<LDAPConnection> LDAPConnections { get; }
    }

    internal class LDAPEntityProviderConfig : EntityProviderSettings, ILDAPSettings
    {
        public List<LDAPConnection> LDAPConnections => throw new NotImplementedException();
    }
}
