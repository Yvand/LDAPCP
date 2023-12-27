using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Yvand.LdapClaimsProvider.Configuration;

namespace Yvand.LdapClaimsProvider
{
    internal class LdapEntityProvider : EntityProviderBase
    {
        public LdapEntityProvider(string claimsProviderName) : base(claimsProviderName) { }

        public override Task<List<string>> GetEntityGroupsAsync(OperationContext currentContext, ClaimTypeConfig groupClaimTypeConfig)
        {
            throw new NotImplementedException();
        }

        public override Task<List<SearchResultCollection>> SearchOrValidateEntitiesAsync(OperationContext currentContext)
        {
            throw new NotImplementedException();
        }
    }
}
