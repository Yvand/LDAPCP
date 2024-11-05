using Microsoft.SharePoint.Administration;
using System;
using Yvand.LdapClaimsProvider;
using Yvand.LdapClaimsProvider.Configuration;
using Yvand.LdapClaimsProvider.Logging;

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
            //settings.UserIdentifierClaimTypeConfig.DirectoryObjectAttributeForDisplayText = "displayName";
            return settings;
        }

        public override void ValidateRuntimeSettings(OperationContext operationContext)
        {
            Uri currentSite = operationContext.UriContext;
            string currentUser = operationContext.UserInHttpContext?.Value;
            Logger.Log($"New request with input {operationContext.Input} from URL {currentSite} and user {currentUser}", TraceSeverity.High, EventSeverity.Information, TraceCategory.Custom);
            // Returns all groups, or only users members of group testLdapcpGroup_002
            string customFilter = "(|(objectClass=group)(memberOf=CN=testLdapcpGroup_002,OU=ldapcp,DC=contoso,DC=local))";
            customFilter = "(|(&(objectClass=group)(|(sAMAccountName=testLdapcpGroup_002)(sAMAccountName=testLdapcpGroup_003)))(memberOf=CN=testLdapcpGroup_002,OU=ldapcp,DC=contoso,DC=local))";
            operationContext.LdapConnections[0].CustomFilter = customFilter;
            //if (currentSite.Port == 6000)
            //{
            //    operationContext.LdapConnections[0].CustomFilter = "(telephoneNumber=00110011)";
            //}
            Logger.Log($"Apply custom LDAP filter \"{customFilter}\"", TraceSeverity.High, EventSeverity.Information, TraceCategory.Custom);
        }
    }
}
