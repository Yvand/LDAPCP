using ldapcp;
using Microsoft.SharePoint.Administration.Claims;
using Microsoft.SharePoint.WebControls;
using NUnit.Framework;
using System;
using System.Linq;
using System.Security.Claims;

namespace LDAPCP.Tests
{
    [TestFixture]
    public class CustomConfigTests
    {
        public const string ClaimsProviderConfigName = "LDAPCPConfig";
        public const string NonExistingClaimType = "http://schemas.yvand.com/ws/claims/random";
        public static string GroupsClaimType = ClaimsProviderConstants.DefaultMainGroupClaimType;

        private LDAPCPConfig Config;
        private LDAPCPConfig BackupConfig;

        [OneTimeSetUp]
        public void Init()
        {
            Console.WriteLine($"Starting custom config test {TestContext.CurrentContext.Test.Name}...");
            Config = LDAPCPConfig.GetConfiguration(ClaimsProviderConfigName);
            BackupConfig = Config.CopyPersistedProperties();
            Config.ResetClaimTypesList();
        }

        [OneTimeTearDown]
        public void Cleanup()
        {
            Config.ApplyConfiguration(BackupConfig);
            Config.Update();
            Console.WriteLine($"Restored actual configuration.");
        }

        [TestCase("ext:externalUser@contoso.com", 1, "externalUser@contoso.com")]
        [TestCase("ext:", 0, "")]
        public void TestPrefixToBypassLookup(string inputValue, int expectedCount, string expectedClaimValue)
        {
            ClaimTypeConfig ctConfig = Config.ClaimTypes.FirstOrDefault(x => String.Equals(UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType, x.ClaimType, StringComparison.InvariantCultureIgnoreCase));
            ctConfig.PrefixToBypassLookup = "ext:";
            Config.Update();

            SPProviderHierarchyTree[] providerResults = UnitTestsHelper.DoSearchOperation(inputValue);
            UnitTestsHelper.VerifySearchResult(providerResults, expectedCount, expectedClaimValue);

            if (expectedCount > 0)
            {
                SPClaim inputClaim = new SPClaim(UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType, expectedClaimValue, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));
                PickerEntity[] entities = UnitTestsHelper.DoValidationOperation(inputClaim);
                UnitTestsHelper.VerifyValidationResult(entities, true, expectedClaimValue);
            }
        }

        [TestCase("Domain Users")]
        [TestCase("Domain Admins")]
        public void TestDynamicTokens(string inputValue)
        {
            string domainNetbios = "contoso";
            string domainFQDN = "contoso.local";
            ClaimTypeConfig ctConfig = Config.ClaimTypes.FirstOrDefault(x => String.Equals(GroupsClaimType, x.ClaimType, StringComparison.InvariantCultureIgnoreCase));

            string expectedValue = inputValue;
            ctConfig.ClaimValuePrefix = String.Empty;
            Config.Update();
            SPProviderHierarchyTree[] providerResults = UnitTestsHelper.DoSearchOperation(inputValue);
            UnitTestsHelper.VerifySearchResult(providerResults, 1, inputValue);
            SPClaim inputClaim = new SPClaim(GroupsClaimType, expectedValue, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));
            PickerEntity[] entities = UnitTestsHelper.DoValidationOperation(inputClaim);
            UnitTestsHelper.VerifyValidationResult(entities, true, expectedValue);

            expectedValue = $@"{domainNetbios}\{inputValue}";
            ctConfig.ClaimValuePrefix = @"{domain}\";
            Config.Update();
            providerResults = UnitTestsHelper.DoSearchOperation(inputValue);
            UnitTestsHelper.VerifySearchResult(providerResults, 1, expectedValue);
            inputClaim = new SPClaim(GroupsClaimType, expectedValue, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));
            entities = UnitTestsHelper.DoValidationOperation(inputClaim);
            UnitTestsHelper.VerifyValidationResult(entities, true, expectedValue);

            expectedValue = $@"{domainFQDN}\{inputValue}";
            ctConfig.ClaimValuePrefix = @"{fqdn}\";
            Config.Update();
            providerResults = UnitTestsHelper.DoSearchOperation(inputValue);
            UnitTestsHelper.VerifySearchResult(providerResults, 1, expectedValue);
            inputClaim = new SPClaim(GroupsClaimType, expectedValue, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));
            entities = UnitTestsHelper.DoValidationOperation(inputClaim);
            UnitTestsHelper.VerifyValidationResult(entities, true, expectedValue);
        }

        [Test]
        public void BypassServer()
        {
            Config.BypassLDAPLookup = true;
            Config.Update();

            SPProviderHierarchyTree[] providerResults = UnitTestsHelper.DoSearchOperation(UnitTestsHelper.NonExistentClaimValue);
            UnitTestsHelper.VerifySearchResult(providerResults, 3, UnitTestsHelper.NonExistentClaimValue);

            SPClaim inputClaim = new SPClaim(UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType, UnitTestsHelper.NonExistentClaimValue, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));
            PickerEntity[] entities = UnitTestsHelper.DoValidationOperation(inputClaim);
            UnitTestsHelper.VerifyValidationResult(entities, true, UnitTestsHelper.NonExistentClaimValue);

            Config.BypassLDAPLookup = false;
            Config.Update();
        }
    }
}
