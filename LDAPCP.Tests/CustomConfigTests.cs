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
        public static string GroupsClaimType = ClaimsProviderConstants.DefaultMainGroupClaimType;
        private LDAPCPConfig Config;
        private LDAPCPConfig BackupConfig;

        [OneTimeSetUp]
        public void Init()
        {
            Console.WriteLine($"Starting custom config test {TestContext.CurrentContext.Test.Name}...");
            Config = LDAPCPConfig.GetConfiguration(UnitTestsHelper.ClaimsProviderConfigName);
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

            UnitTestsHelper.TestSearchOperation(inputValue, expectedCount, expectedClaimValue);

            if (expectedCount > 0)
            {
                SPClaim inputClaim = new SPClaim(UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType, expectedClaimValue, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));
                UnitTestsHelper.TestValidationOperation(inputClaim, true, expectedClaimValue);
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
            UnitTestsHelper.TestSearchOperation(inputValue, 1, inputValue);
            SPClaim inputClaim = new SPClaim(GroupsClaimType, expectedValue, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));
            UnitTestsHelper.TestValidationOperation(inputClaim, true, expectedValue);

            expectedValue = $@"{domainNetbios}\{inputValue}";
            ctConfig.ClaimValuePrefix = @"{domain}\";
            Config.Update();
            UnitTestsHelper.TestSearchOperation(inputValue, 1, expectedValue);
            inputClaim = new SPClaim(GroupsClaimType, expectedValue, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));
            UnitTestsHelper.TestValidationOperation(inputClaim, true, expectedValue);

            expectedValue = $@"{domainFQDN}\{inputValue}";
            ctConfig.ClaimValuePrefix = @"{fqdn}\";
            Config.Update();
            UnitTestsHelper.TestSearchOperation(inputValue, 1, expectedValue);
            inputClaim = new SPClaim(GroupsClaimType, expectedValue, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));
            UnitTestsHelper.TestValidationOperation(inputClaim, true, expectedValue);
        }

        [Test]
        public void BypassServer()
        {
            Config.BypassLDAPLookup = true;
            Config.Update();

            try
            {
                UnitTestsHelper.TestSearchOperation(UnitTestsHelper.RandomClaimValue, 4, UnitTestsHelper.RandomClaimValue);

                SPClaim inputClaim = new SPClaim(UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType, UnitTestsHelper.RandomClaimValue, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));
                UnitTestsHelper.TestValidationOperation(inputClaim, true, UnitTestsHelper.RandomClaimValue);
            }
            finally
            {
                Config.BypassLDAPLookup = false;
                Config.Update();
            }
        }

        [Test, TestCaseSource(typeof(ValidateEntityDataSource), "GetTestData")]
        [Repeat(UnitTestsHelper.TestRepeatCount)]
        public void TestAlternativeAugmentation(ValidateEntityData registrationData)
        {
            foreach (LDAPConnection ldapConn in Config.LDAPConnectionsProp)
            {
                ldapConn.GetGroupMembershipAsADDomainProp = !ldapConn.GetGroupMembershipAsADDomainProp;
            }
            Config.Update();
            UnitTestsHelper.TestAugmentationOperation(UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType, registrationData.ClaimValue, registrationData.IsMemberOfTrustedGroup);
        }

        [Test, TestCaseSource(typeof(ValidateEntityDataSource), "GetTestData")]
        [Repeat(UnitTestsHelper.TestRepeatCount)]
        public void RequireExactMatchDuringSearch(ValidateEntityData registrationData)
        {
            Config.FilterExactMatchOnlyProp = true;
            Config.Update();

            try
            {
                int expectedCount = registrationData.ShouldValidate ? 1 : 0;
                UnitTestsHelper.TestSearchOperation(registrationData.ClaimValue, expectedCount, registrationData.ClaimValue);
            }
            finally
            {
                Config.FilterExactMatchOnlyProp = false;
                Config.Update();
            }
        }
    }
}
