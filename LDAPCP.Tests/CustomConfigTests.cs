using ldapcp;
using Microsoft.SharePoint.Administration.Claims;
using NUnit.Framework;
using System;
using System.Linq;
using System.Security.Claims;

namespace LDAPCP.Tests
{
    [TestFixture]
    public class CustomConfigTests : ModifyConfigBase
    {
        public static string GroupsClaimType = ClaimsProviderConstants.DefaultMainGroupClaimType;

        public override void InitializeConfiguration()
        {
            base.InitializeConfiguration();

            // Extra initialization for current test class
            Config.EnableAugmentation = true;
            Config.LDAPConnectionsProp.ForEach(x => x.AugmentationEnabled = true);
            Config.ClaimTypes.GetByClaimType(UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType).PrefixToBypassLookup = "bypass-user:";
            Config.ClaimTypes.GetByClaimType(UnitTestsHelper.TrustedGroupToAdd_ClaimType).PrefixToBypassLookup = "bypass-group:";
            Config.ClaimTypes.GetByClaimType(UnitTestsHelper.TrustedGroupToAdd_ClaimType).ClaimValuePrefix = @"{fqdn}\";
            Config.Update();
        }

        [TestCase("bypass-user:externalUser@contoso.com", 1, "externalUser@contoso.com")]
        [TestCase("externalUser@contoso.com", 0, "")]
        [TestCase("bypass-user:", 0, "")]
        public void BypassLookupOnIdentityClaimTest(string inputValue, int expectedCount, string expectedClaimValue)
        {
            //Config.ClaimTypes.GetByClaimType(UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType).PrefixToBypassLookup = "bypass-user:";
            //Config.Update();

            try
            {
                UnitTestsHelper.TestSearchOperation(inputValue, expectedCount, expectedClaimValue);

                if (expectedCount > 0)
                {
                    SPClaim inputClaim = new SPClaim(UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType, expectedClaimValue, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));
                    UnitTestsHelper.TestValidationOperation(inputClaim, true, expectedClaimValue);
                }
            }
            finally
            {
                //Config.ClaimTypes.GetByClaimType(UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType).PrefixToBypassLookup = String.Empty;
                //Config.Update();
            }
        }

        [TestCase(@"bypass-group:domain\groupValue", 1, @"domain\groupValue")]
        [TestCase(@"domain\groupValue", 0, "")]
        [TestCase("bypass-group:", 0, "")]
        public void BypassLookupOnGroupClaimTest(string inputValue, int expectedCount, string expectedClaimValue)
        {
            //ClaimTypeConfig ctConfig = Config.ClaimTypes.FirstOrDefault(x => String.Equals(UnitTestsHelper.TrustedGroupToAdd_ClaimType, x.ClaimType, StringComparison.InvariantCultureIgnoreCase));
            //ctConfig.PrefixToBypassLookup = "bypass-group:";
            Config.Update();

            try
            {
                UnitTestsHelper.TestSearchOperation(inputValue, expectedCount, expectedClaimValue);

                if (expectedCount > 0)
                {
                    SPClaim inputClaim = new SPClaim(UnitTestsHelper.TrustedGroupToAdd_ClaimType, expectedClaimValue, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));
                    UnitTestsHelper.TestValidationOperation(inputClaim, true, expectedClaimValue);
                }
            }
            finally
            {
                //ctConfig.PrefixToBypassLookup = String.Empty;
                //Config.Update();
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
                UnitTestsHelper.TestSearchOperation(UnitTestsHelper.RandomClaimValue, 3, UnitTestsHelper.RandomClaimValue);

                SPClaim inputClaim = new SPClaim(UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType, UnitTestsHelper.RandomClaimValue, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));
                UnitTestsHelper.TestValidationOperation(inputClaim, true, UnitTestsHelper.RandomClaimValue);
            }
            catch { }
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
            catch { }
            finally
            {
                Config.FilterExactMatchOnlyProp = false;
                Config.Update();
            }
        }
    }
}
