using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ldapcp
{
    public static class Constants
    {
        public const string LDAPCPCONFIG_ID = "5D306A02-A262-48AC-8C44-BDB927620227";
        public const string LDAPCPCONFIG_NAME = "LdapcpConfig";
        public const string LDAPCPCONFIG_TOKENDOMAINNAME = "{domain}"; //NETBIOS DOMAIN NAME
        public const string LDAPCPCONFIG_TOKENDOMAINFQDN = "{fqdn}";
        public const int LDAPCPCONFIG_TIMEOUT = 10;

        public const string TextErrorNoGroupClaimType = "There is no claim type associated with an entity type 'FormsRole' or 'SecurityGroup'.";
        public const string TextErrorLDAPFieldsMissing = "Some mandatory fields are missing.";
        public const string TextErrorTestLdapConnection = "Unable to connect to LDAP for following reason:<br/>{0}<br/>It may be expected if w3wp process of central admin has intentionally no access to LDAP server.";
        public const string TextErrorNetBiosDomainName = "Unable to resolve NetBios domain name for following reason:<br/>{0}<br/>";
        public const string TextConnectionSuccessful = "Connection successful.";
        public const string TextSharePointDomain = "Connect to SharePoint domain";
        public const string TextUpdateAdditionalLdapFilterOk = "LDAP filter was successfully applied to all LDAP attributes of class 'user'.";
    }
}
