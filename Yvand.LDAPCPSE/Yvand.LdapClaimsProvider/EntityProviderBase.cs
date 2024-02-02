using System.Collections.Generic;
using System.DirectoryServices;
using Yvand.LdapClaimsProvider.Configuration;

namespace Yvand.LdapClaimsProvider
{
    public abstract class EntityProviderBase
    {
        /// <summary>
        /// Gets the name of the claims provider using this entity provider
        /// </summary>
        public string ClaimsProviderName { get; }

        /// <summary>
        /// Returns a list of users and groups
        /// </summary>
        /// <param name="currentContext"></param>
        /// <returns></returns>
        public abstract List<LdapEntityProviderResult> SearchOrValidateEntities(OperationContext currentContext);

        /// <summary>
        /// Returns the groups the user is member of
        /// </summary>
        /// <param name="currentContext"></param>
        /// <param name="groupClaimTypeConfig"></param>
        /// <returns></returns>
        public abstract List<string> GetEntityGroups(OperationContext currentContext);

        public EntityProviderBase(string claimsProviderName)
        {
            this.ClaimsProviderName = claimsProviderName;
        }
    }
}
