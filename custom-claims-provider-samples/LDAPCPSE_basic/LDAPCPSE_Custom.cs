using System;
using Yvand.LdapClaimsProvider;
using Yvand.LdapClaimsProvider.Configuration;

namespace LDAPCPSE_basic
{
    public class LDAPCPSE_Custom : LDAPCPSE
    {
        /// <summary>
        /// Sets the name of the claims provider, also set in (Get-SPTrustedIdentityTokenIssuer).ClaimProviderName property
        /// </summary>
        public new const string ClaimsProviderName = "LDAPCPSE_Custom";
        
        /// <summary>
        /// Do not remove or change this property
        /// </summary>
        public override string Name => ClaimsProviderName;

        public LDAPCPSE_Custom(string displayName) : base(displayName)
        {
        }

        public override ILdapProviderSettings GetSettings()
        {
            ClaimsProviderSettings settings = ClaimsProviderSettings.GetDefaultSettings(ClaimsProviderName);
            settings.EntityDisplayTextPrefix = "(custom) ";
            return settings;
        }

        public override void ValidateRuntimeSettings(OperationContext operationContext)
        {
            Uri currentSite = operationContext.UriContext;
            string currentUser = operationContext.UserInHttpContext?.Value;
            if (currentSite.Port == 6000)
            {
                operationContext.LdapConnections[0].CustomFilter = "(telephoneNumber=00110011)";
            }
        }
    }
}
