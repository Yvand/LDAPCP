using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ldapcp.Constants;

namespace ldapcp.Helpers
{
    /// <summary>
    /// Helper methods for resolving domain names
    /// </summary>
    public class DomainHelper
    {
        public List<string> ResolveNetBiosDomainName()
        {
            var results = new List<string>();
            if (TxtLdapConnectionString.Text == String.Empty || TxtLdapPassword.Text == String.Empty || TxtLdapUsername.Text == String.Empty)
            {
                LabelErrorTestLdapConnection.Text = Constant.TextErrorLDAPFieldsMissing;
                return null;
            }

            DirectoryEntry de = null;

            try
            {
                AuthenticationTypes authNTypes = GetSelectedAuthenticationTypes(false);
                de = new DirectoryEntry(this.TxtLdapConnectionString.Text, this.TxtLdapUsername.Text, this.TxtLdapPassword.Text, authNTypes);
                results = ResolveNetBiosDomainName(de, TxtLdapUsername.Text, TxtLdapPassword.Text, authNTypes);
            }
            catch (Exception ex)
            {
                LdapcpLogging.LogException(LDAPCP._ProviderInternalName, "while testing LDAP connection", LdapcpLogging.Categories.Configuration, ex);
                LabelErrorTestLdapConnection.Text = String.Format(TextErrorTestLdapConnection, ex.Message);
            }
            finally
            {
                if (de != null) de.Dispose();
            }

            return results;
        }

        public List<string> ResolveNetBiosDomainName(DirectoryEntry directoryEntry, string username, string password, AuthenticationTypes authenticationType)
        {
            var netbiosDomainNames = new List<string>();
            var distinguishedName = string.Empty;

            DirectorySearcher searcher = new DirectorySearcher();
            try
            {
                // TODO: LDAP connection string can be LDAPS as well
                var directoryPath = directoryEntry.Path;
                var provider = directoryPath.Split(new[] { @"://" }, StringSplitOptions.None)[0];
                var directory = directoryPath.Split(new[] { @"://" }, StringSplitOptions.None)[1];
                var dnsDomainName = string.Empty;

                dnsDomainName = ResolveDomainFromDirectoryPath(directory);

                searcher = ResolveRootDirectorySearcher(directoryEntry, distinguishedName, provider, dnsDomainName, username, password, authenticationType);
                searcher.SearchScope = SearchScope.OneLevel;
                searcher.PropertiesToLoad.Add("netbiosname");
                searcher.Filter = "netBIOSName=*";
                SearchResultCollection results = null;

                results = searcher.FindAll();

                if (results.Count > 0)
                {
                    foreach (SearchResult res in results)
                    {
                        var netbiosDomainName = res.Properties["netbiosname"][0].ToString();
                        if (!netbiosDomainNames.Contains(netbiosDomainName))
                        {
                            netbiosDomainNames.Add(netbiosDomainName);
                        }
                    }
                }

                LabelTestLdapConnectionOK.Text += string.Format("<br>Resolved NetBios Domain Name/s: {0}<br>", String.Join("<br>", netbiosDomainNames.Select(x => x).ToArray()));
            }
            catch (Exception ex)
            {
                LdapcpLogging.LogException(LDAPCP._ProviderInternalName, "in ResolveNetBiosDomainName", LdapcpLogging.Categories.Configuration, ex);
                LabelErrorTestLdapConnection.Text = string.Format(Constant.TextErrorNetBiosDomainName, ex.Message);
            }
            finally
            {
                searcher.Dispose();
            }

            return netbiosDomainNames;
        }

        public DirectorySearcher ResolveRootDirectorySearcher(DirectoryEntry directoryEntry, string distinguishedName, string provider, string dnsDomainName, string username, string password, AuthenticationTypes authenticationType)
        {
            DirectoryEntry searchRoot = null;

            if (directoryEntry.Properties["distinguishedName"].Value != null)
            {
                distinguishedName = directoryEntry.Properties["distinguishedName"].Value.ToString();
            }

            if (distinguishedName.ToUpper().Contains("OU="))
            {
                // distinguished name contains OU (Organizational Units), so we need to parse to only have DC (Domain Components) elements in our DirectoryEntry path
                var domainComponents = ResolveDnsDomainName(distinguishedName).Split('.');
                distinguishedName = string.Empty;
                var componentCount = 1;
                foreach (var component in domainComponents)
                {
                    distinguishedName += "DC=" + component + (componentCount < domainComponents.Length ? "," : "");
                    componentCount++;
                }
            }

            // Every AD forest does have Configuration Node. Here is how we target it e.g. LDAP://contoso.com/cn=Partitions,cn=Configuration,dn=contoso,dn=com
            searchRoot = new DirectoryEntry(string.Format("{0}://{1}/cn=Partitions,cn=Configuration,{2}", provider, dnsDomainName, distinguishedName), username, password, authenticationType);

            var searcher = new DirectorySearcher(searchRoot);
            return searcher;
        }

        private string ResolveDomainFromDirectoryPath(string directory)
        {
            var dnsDomainName = string.Empty;

            if (directory.Contains("/"))
            {
                var domainConfiguration = directory.Split('/')[0];
                // example for validating connection string similar to following: <domain>/ou=<some_value>,ou=<some_value>,dc=<subdomain>,dc=<domain>,dc=<ch>
                if (!IsValidDomain(domainConfiguration) && (domainConfiguration.Contains("DC") || (domainConfiguration.Contains("dc"))))
                {
                    // it is not a domain name, resolve all DC (Domain Component) parameters as a valid domain and ignore all the rest
                    dnsDomainName = ResolveDnsDomainName(domainConfiguration);
                }
                else
                {
                    // it is valid domain name, extract it
                    dnsDomainName = domainConfiguration;
                }
            }
            else
            {
                dnsDomainName = !IsValidDomain(directory) ? ResolveDnsDomainName(directory) : directory;
            }
            return dnsDomainName;
        }

        private bool IsValidDomain(string directoryPath)
        {
            if (Regex.IsMatch(directoryPath, @" # Rev:2013-03-26
                      # Match DNS host domain having one or more subdomains.
                      # Top level domain subset taken from IANA.ORG. See:
                      # http://data.iana.org/TLD/tlds-alpha-by-domain.txt
                      ^                  # Anchor to start of string.
                      (?!.{256})         # Whole domain must be 255 or less.
                      (?:                # Group for one or more sub-domains.
                        [a-z0-9]         # Either subdomain length from 2-63.
                        [a-z0-9-]{0,61}  # Middle part may have dashes.
                        [a-z0-9]         # Starts and ends with alphanum.
                        \.               # Dot separates subdomains.
                      | [a-z0-9]         # or subdomain length == 1 char.
                        \.               # Dot separates subdomains.
                      )+                 # One or more sub-domains.
                      (?:                # Top level domain alternatives.
                        [a-z]{2}         # Either any 2 char country code,
                      | AERO|ARPA|ASIA|BIZ|CAT|COM|COOP|EDU|  # or TLD 
                        GOV|INFO|INT|JOBS|MIL|MOBI|MUSEUM|    # from list.
                        NAME|NET|ORG|POST|PRO|TEL|TRAVEL  # IANA.ORG
                      )                  # End group of TLD alternatives.
                      $                  # Anchor to end of string.",
                RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace))
            {
                return true;
            }
            return false;
        }

        private string ResolveDnsDomainName(string configuration)
        {
            string pattern = @"(s*)dc=([^,]+)";
            string output = string.Empty;

            MatchCollection matches = Regex.Matches(configuration, pattern, RegexOptions.IgnoreCase);

            var matchCount = 1;
            foreach (Match match in matches)
            {
                output += match.Value.Split(new[] { "Dc=", "DC=", "dc=", "dC=" }, StringSplitOptions.None)[1] + (matchCount < matches.Count ? "," : "");
                matchCount++;
            }

            if (output.Contains(","))
            {
                var components = output.Split(',');
                var domain = string.Empty;

                var componentCount = 1;
                foreach (var component in components)
                {
                    domain += component + (componentCount < components.Length ? "." : "");
                    componentCount++;
                }

                if (!string.IsNullOrEmpty(domain))
                {
                    output = domain;
                }
            }

            return output;
        }
    }
}
