using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using Yvand.LdapClaimsProvider.Logging;

namespace Yvand.LdapClaimsProvider.Configuration
{
    public static class Utils
    {
        /// <summary>
        /// Gets the first SharePoint TrustedLoginProvider that has its property ClaimProviderName equals to <paramref name="claimsProviderName"/>
        /// LIMITATION: The same claims provider (uniquely identified by its name) cannot be associated to multiple TrustedLoginProvider because at runtime there is no way to determine what TrustedLoginProvider is currently calling
        /// </summary>
        /// <param name="claimsProviderName"></param>
        /// <returns></returns>
        public static SPTrustedLoginProvider GetSPTrustAssociatedWithClaimsProvider(string claimsProviderName)
        {
            if (String.IsNullOrWhiteSpace(claimsProviderName)) { throw new ArgumentNullException(nameof(claimsProviderName)); }

            var lp = SPSecurityTokenServiceManager.Local.TrustedLoginProviders.Where(x => String.Equals(x.ClaimProviderName, claimsProviderName, StringComparison.OrdinalIgnoreCase));

            if (lp != null && lp.Count() == 1)
            {
                return lp.First();
            }

            if (lp != null && lp.Count() > 1)
            {
                Logger.Log($"[{claimsProviderName}] Cannot continue because '{claimsProviderName}' is set with multiple SPTrustedIdentityTokenIssuer", TraceSeverity.Unexpected, EventSeverity.Error, TraceCategory.Core);
            }
            Logger.Log($"[{claimsProviderName}] Cannot continue because '{claimsProviderName}' is not set with any SPTrustedIdentityTokenIssuer.\r\nVisit {ClaimsProviderConstants.PUBLICSITEURL} for more information.", TraceSeverity.High, EventSeverity.Warning, TraceCategory.Core);
            return null;
        }

        /// <summary>
        /// Checks if the claims provider <paramref name="claimsProviderName"/> should run in the specified <paramref name="context"/>
        /// </summary>
        /// <param name="context">The URI of the current site, or null</param>
        /// <param name="claimsProviderName">The name of the claims provider</param>
        /// <returns></returns>
        public static bool IsClaimsProviderUsedInCurrentContext(Uri context, string claimsProviderName)
        {
            if (String.IsNullOrWhiteSpace(claimsProviderName)) { throw new ArgumentNullException(nameof(claimsProviderName)); }
            if (context == null) { return true; }
            var webApp = SPWebApplication.Lookup(context);
            if (webApp == null) { return false; }
            if (webApp.IsAdministrationWebApplication) { return true; }

            // Not central admin web app, enable EntraCP only if current web app uses it
            // It is not possible to exclude zones where EntraCP is not used because:
            // Consider following scenario: default zone is WinClaims, intranet zone is Federated:
            // In intranet zone, when creating permission, EntraCP will be called 2 times. The 2nd time (in FillResolve (SPClaim)), the context will always be the URL of the default zone
            foreach (var zone in Enum.GetValues(typeof(SPUrlZone)))
            {
                SPIisSettings iisSettings = webApp.GetIisSettingsWithFallback((SPUrlZone)zone);
                if (!iisSettings.UseTrustedClaimsAuthenticationProvider)
                {
                    continue;
                }

                // Get the list of authentication providers associated with the zone
                foreach (SPAuthenticationProvider prov in iisSettings.ClaimsAuthenticationProviders)
                {
                    if (prov.GetType() == typeof(SPTrustedAuthenticationProvider))
                    {
                        // Check if the current SPTrustedAuthenticationProvider is associated with the claim provider
                        if (String.Equals(prov.ClaimProviderName, claimsProviderName, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public static IEnumerable<string> GetNonWellKnownUserClaimTypesFromTrust(string claimsProviderName)
        {
            if (String.IsNullOrWhiteSpace(claimsProviderName)) { throw new ArgumentNullException(nameof(claimsProviderName)); }

            SPTrustedLoginProvider trust = GetSPTrustAssociatedWithClaimsProvider(claimsProviderName);
            if (trust == null)
            {
                return null;
            }

            // Return all claim types registered in the SPTrustedLoginProvider that do not match a known user claim type
            IEnumerable<string> nonWellKnownUserClaimTypes = trust.ClaimTypeInformation
                .Where(x => ClaimsProviderConstants.GetDefaultSettingsPerUserClaimType(x.MappedClaimType) == null)
                .Select(x => x.MappedClaimType);
            return nonWellKnownUserClaimTypes;
        }

        /// <summary>
        /// Copy in target all the fields of source which have the decoration [Persisted] set on the specified type (fields inherited from parent types are ignored)
        /// </summary>
        /// <param name="T"></param>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns>The target object with fields decorated with [Persisted] set from the source object</returns>
        public static object CopyPersistedFields(Type T, object source, object target)
        {
            List<FieldInfo> persistedFields = T
            .GetRuntimeFields()
            .Where(field => field.GetCustomAttributes(typeof(PersistedAttribute), inherit: false).Any())
            .ToList();

            foreach (FieldInfo field in persistedFields)
            {
                field.SetValue(target, field.GetValue(source));
            }
            return target;
        }

        /// <summary>
        /// Copy the value of all the public properties in object source, which can be set, even if the setter is private, to object target.
        /// Only the properties declared in the type T are considered, inherited types are ignored.
        /// </summary>
        /// <param name="T">Type of the source and target objects</param>
        /// <param name="source">Object to copy from</param>
        /// <param name="target">Object to copy to</param>
        /// <returns></returns>
        public static object CopyPublicProperties(Type T, object source, object target)
        {
            PropertyInfo[] propertiesToCopy = T.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            foreach (PropertyInfo property in propertiesToCopy)
            {
                if (property.CanWrite)
                {
                    object value = property.GetValue(source);
                    if (value != null)
                    {
                        property.SetValue(target, value);
                    }
                }
            }
            return target;
        }

        public static object CopyAllProperties(Type T, object source, object target)
        {
            PropertyInfo[] propertiesToCopy = T.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (PropertyInfo property in propertiesToCopy)
            {
                if (property.CanWrite)
                {
                    object value = property.GetValue(source);
                    if (value != null)
                    {
                        property.SetValue(target, value);
                    }
                }
            }
            return target;
        }

        /// <summary>
        /// Returns the account from "domain\account"
        /// </summary>
        /// <param name="fullAccountName">e.g. "contoso.local\account"</param>
        /// <returns>account</returns>
        public static string GetAccountFromFullAccountName(string fullAccountName)
        {
            if (fullAccountName.Contains(@"\"))
            {
                return fullAccountName.Split(new char[] { '\\' }, 2)[1];
            }
            else
            {
                return fullAccountName;
            }
        }

        /// <summary>
        /// Returns the domain from "domain\account"
        /// </summary>
        /// <param name="fullAccountName">e.g. "contoso.local\account"</param>
        /// <returns>e.g. "contoso.local"</returns>
        public static string GetDomainFromFullAccountName(string fullAccountName)
        {
            if (fullAccountName.Contains(@"\"))
            {
                return fullAccountName.Split(new char[] { '\\' }, 2)[0];
            }
            else
            {
                return fullAccountName;
            }
        }

        /// <summary>
        /// Return the domain FQDN from the given email
        /// </summary>
        /// <param name="email">e.g. yvand@contoso.local</param>
        /// <returns>e.g. contoso.local</returns>
        public static string GetFQDNFromEmail(string email)
        {
            return Regex.Replace(email, ClaimsProviderConstants.RegexFullDomainFromEmail, "$1", RegexOptions.None);
        }

        public static string GetFirstSubString(string value, string separator)
        {
            int stop = value.IndexOf(separator);
            return (stop > -1) ? value.Substring(0, stop) : string.Empty;
        }

        /// <summary>
        /// Return the domain name from the domain FQDN
        /// </summary>
        /// <param name="domainFQDN">Fully qualified domain name</param>
        /// <returns>Domain name</returns>
        public static string GetDomainName(string domainFQDN)
        {
            string domainName = String.Empty;
            if (domainFQDN.Contains("."))
            {
                domainName = domainFQDN.Split(new char[] { '.' })[0];
            }
            return domainName;
        }

        /// <summary>
        /// Extract domain name information from the distinguishedName supplied
        /// </summary>
        /// <param name="distinguishedName">distinguishedName to use to extract domain name information</param>
        /// <param name="domainName">Domain name</param>
        /// <param name="domainFQDN">Fully qualified domain name</param>
        public static void GetDomainInformation(string distinguishedName, out string domainName, out string domainFQDN)
        {
            StringBuilder sbDomainFQDN = new StringBuilder();
            domainName = String.Empty;
            // String search in distinguishedName should not be case sensitive - https://github.com/Yvand/LDAPCP/issues/147
            if (distinguishedName.IndexOf("DC=", StringComparison.InvariantCultureIgnoreCase) >= 0)
            {
                int start = distinguishedName.IndexOf("DC=", StringComparison.InvariantCultureIgnoreCase);
                string[] dnSplitted = distinguishedName.Substring(start).ToLower().Split(new string[] { "dc=" }, StringSplitOptions.RemoveEmptyEntries);
                bool setDomainName = true;
                foreach (string dc in dnSplitted)
                {
                    sbDomainFQDN.Append(dc.Replace(',', '.'));
                    if (setDomainName)
                    {
                        domainName = dc.Trim(new char[] { ',' });
                        setDomainName = false;
                    }
                }
            }
            domainFQDN = sbDomainFQDN.ToString();
        }

        /// <summary>
        /// Query LDAP server to retrieve domain name information
        /// </summary>
        /// <param name="directory">LDAP Server to query</param>
        /// <param name="domainName">Domain name</param>
        /// <param name="domainFQDN">Fully qualified domain name</param>
        public static bool GetDomainInformation(DirectoryEntry directory, out string domaindistinguishedName, out string domainName, out string domainFQDN)
        {
            bool success = false;
            domaindistinguishedName = String.Empty;
            domainName = String.Empty;
            domainFQDN = String.Empty;

            try
            {
#if DEBUG
                directory.AuthenticationType = AuthenticationTypes.None;
                Logger.LogDebug($"Hardcoded property DirectoryEntry.AuthenticationType to {directory.AuthenticationType} for \"{directory.Path}\"");
#endif

                // Method PropertyCollection.Contains("distinguishedName") does a LDAP bind
                // In AD LDS: property "distinguishedName" = "CN=LDSInstance2,DC=ADLDS,DC=local", properties "name" and "cn" = "LDSInstance2"
                if (directory.Properties.Contains("distinguishedName"))
                {
                    domaindistinguishedName = directory.Properties["distinguishedName"].Value.ToString();
                    GetDomainInformation(domaindistinguishedName, out domainName, out domainFQDN);
                }
                else if (directory.Properties.Contains("name"))
                {
                    domainName = directory.Properties["name"].Value.ToString();
                }
                else if (directory.Properties.Contains("cn"))
                {
                    // Tivoli stores domain name in property "cn" (properties "distinguishedName" and "name" don't exist)
                    domainName = directory.Properties["cn"].Value.ToString();
                }

                success = true;
            }
            catch (DirectoryServicesCOMException ex)
            {
                Logger.LogException("", $"while getting domain names information for LDAP connection {directory.Path} (DirectoryServicesCOMException)", TraceCategory.Configuration, ex);
            }
            catch (Exception ex)
            {
                Logger.LogException("", $"while getting domain names information for LDAP connection {directory.Path} (Exception)", TraceCategory.Configuration, ex);
            }

            return success;
        }

        /// <summary>
        /// Return the value from a distinguished name, or an empty string if not found.
        /// </summary>
        /// <param name="distinguishedNameValue">e.g. "CN=group1,CN=Users,DC=contoso,DC=local"</param>
        /// <returns>e.g. "group1", or an empty string if not found</returns>
        public static string GetNameFromDistinguishedName(string distinguishedNameValue)
        {
            int equalsIndex = distinguishedNameValue.IndexOf("=", 1);
            int commaIndex = distinguishedNameValue.IndexOf(",", 1);
            if (equalsIndex != -1 && commaIndex != -1)
            {
                return distinguishedNameValue.Substring(equalsIndex + 1, commaIndex - equalsIndex - 1);
            }
            else
            {
                return String.Empty;
            }
        }

        public static string EscapeSpecialCharacters(string stringWithSpecialChars)
        {
            if (String.IsNullOrWhiteSpace(stringWithSpecialChars))
            {
                return String.Empty;
            }
            string result = stringWithSpecialChars;
            foreach (KeyValuePair<string, string> kvp in ClaimsProviderConstants.SpecialCharacters)
            {
                result = result.Replace(kvp.Key, kvp.Value);
            }
            return result;
        }

        //public static string UnescapeSpecialCharacters(string stringWithEscapedChars)
        //{
        //    string result = stringWithEscapedChars;
        //    foreach (KeyValuePair<string, string> kvp in ClaimsProviderConstants.SpecialCharacters)
        //    {
        //        result = result.Replace(kvp.Value, kvp.Key);
        //    }
        //    return result;
        //}

        public static string ConvertSidBinaryToString(byte[] sidBinary)
        {
            string sidString = String.Empty;
            try
            {
                // Works even if the sid is from an exteral domain
                SecurityIdentifier sid = new SecurityIdentifier(sidBinary, 0);
                sidString = sid.ToString();
            }
            catch (Exception e)
            {
            }
            return sidString;
        }

        public static byte[] ConvertSidStringToBinary(string sidString)
        {
            byte[] sidBinary = new byte[85];
            try
            {
                SecurityIdentifier sid = new SecurityIdentifier(sidString);
                sid.GetBinaryForm(sidBinary, 0);
            }
            catch (Exception e)
            {
            }
            return sidBinary;
        }

        public static bool IsDynamicTokenSet(string stringToVerify, string dynamicTokenToSearch)
        {
            return !String.IsNullOrWhiteSpace(stringToVerify) && stringToVerify.Contains(dynamicTokenToSearch);
        }

        public static string GetLdapValueAsString(object value, string directoryObjectAttributeName)
        {
            string directoryObjectPropertyValue = String.Empty;
            // Fix https://github.com/Yvand/LDAPCP/issues/43: properly test the type of the value
            if (value is string)
            {
                directoryObjectPropertyValue = value as string;
            }
            else if (value is Int32)
            {
                // This is true for ldap attribute primaryGroupID
                directoryObjectPropertyValue = value.ToString();
            }
            else if (value is byte[])
            {
                byte[] valueAsBytes = value as byte[];
                if (String.Equals(directoryObjectAttributeName, "objectsid", StringComparison.OrdinalIgnoreCase))
                {
                    directoryObjectPropertyValue = Utils.ConvertSidBinaryToString(valueAsBytes);
                }
                else
                {
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < valueAsBytes.Length; i++)
                    {
                        sb.Append(valueAsBytes[i].ToString("x2"));
                    }
                    directoryObjectPropertyValue = sb.ToString();
                }
            }
            return directoryObjectPropertyValue;
        }
    }
}
