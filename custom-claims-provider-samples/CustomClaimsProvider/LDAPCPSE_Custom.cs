using System.Collections.Generic;
using Yvand.LdapClaimsProvider;
using Yvand.LdapClaimsProvider.Configuration;

namespace CustomClaimsProvider
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
            //EntraIDTenant tenant = new EntraIDTenant
            //{
            //    AzureCloud = AzureCloudName.AzureGlobal,
            //    Name = "TENANTNAME.onmicrosoft.com",
            //    ClientId = "CLIENTID",
            //    ClientSecret = "CLIENTSECRET",
            //};
            //settings.EntraIDTenants = new List<EntraIDTenant>() { tenant };
            return settings;
        }
    }
}
