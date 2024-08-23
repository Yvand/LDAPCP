using NUnit.Framework;
using Yvand.LdapClaimsProvider.Configuration;

namespace Yvand.LdapClaimsProvider.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class BypassDirectoryOnClaimTypesTests : ClaimsProviderTestsBase
    {
        const string PrefixBypassUserSearch = "bypass-user:";
        const string PrefixBypassGroupSearch = "bypass-group:";
        protected override void InitializeSettings()
        {
            base.InitializeSettings();
            Settings.ClaimTypes.UserIdentifierConfig.LeadingKeywordToBypassDirectory = PrefixBypassUserSearch;
            Settings.ClaimTypes.GroupIdentifierConfig.LeadingKeywordToBypassDirectory = PrefixBypassGroupSearch;
            base.ApplySettings();
        }

        [Test]
        public override void CheckSettingsTest()
        {
            base.CheckSettingsTest();
        }

        [Test, TestCaseSource(typeof(TestEntitySourceManager), nameof(TestEntitySourceManager.GetSomeUsers), new object[] { TestEntitySourceManager.MaxNumberOfUsersToTest })]
        public void TestUsers(TestUser user)
        {
            // Normal scenario
            base.TestSearchAndValidateForTestUser(user);
            base.TestAugmentationAgainst1RandomGroup(user);

            // Scenario with bypass keyword passed
            // TestSearchAndValidateForTestUser() uses the SamAccountName is used as the input so it should be set with the expected claim value
            user.SamAccountName = $"{PrefixBypassUserSearch}{user.UserPrincipalName}";
            base.TestSearchAndValidateForTestUser(user);
        }

        [Test]
        [Repeat(5)]
        public override void TestAugmentationOfGoldUsersAgainstRandomGroups()
        {
            base.TestAugmentationOfGoldUsersAgainstRandomGroups();
        }

        [TestCase("bypass-user:externalUser@contoso.com", 1, "externalUser@contoso.com")]
        [TestCase("externalUser@contoso.com", 0, "")]
        [TestCase("bypass-user:", 0, "")]
        [TestCase(@"bypass-group:domain\groupValue", 1, @"domain\groupValue")]
        [TestCase(@"domain\groupValue", 0, "")]
        [TestCase("bypass-group:", 0, "")]
        public void TestBypassDirectoryByClaimType(string inputValue, int expectedCount, string expectedClaimValue)
        {
            TestSearchOperation(inputValue, expectedCount, expectedClaimValue);

            if (expectedCount > 0)
            {
                TestValidationOperation(UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType, expectedClaimValue, true);
            }
        }
    }

    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class BypassDirectoryGloballyTests : ClaimsProviderTestsBase
    {
        protected override void InitializeSettings()
        {
            base.InitializeSettings();
            Settings.AlwaysResolveUserInput = true;
            base.ApplySettings();
        }

        [Test]
        public override void CheckSettingsTest()
        {
            base.CheckSettingsTest();
        }

        [Test]
        public void TestBypassDirectoryGlobally()
        {
            TestSearchOperation(UnitTestsHelper.RandomClaimValue, 2, UnitTestsHelper.RandomClaimValue);
            TestValidationOperation(base.UserIdentifierClaimType, UnitTestsHelper.RandomClaimValue, true);
            TestValidationOperation(base.GroupIdentifierClaimType, UnitTestsHelper.RandomClaimValue, true);
        }

        [Test, TestCaseSource(typeof(TestEntitySourceManager), nameof(TestEntitySourceManager.GetSomeUsers), new object[] { TestEntitySourceManager.MaxNumberOfUsersToTest })]
        public void TestUsers(TestUser user)
        {
            base.TestSearchAndValidateForTestUser(user);
            base.TestAugmentationAgainst1RandomGroup(user);
        }

        [Test, TestCaseSource(typeof(TestEntitySourceManager), nameof(TestEntitySourceManager.GetSomeGroups), new object[] { TestEntitySourceManager.MaxNumberOfGroupsToTest })]
        public void TestGroups(TestGroup group)
        {
            TestSearchAndValidateForTestGroup(group);
        }

        [Test]
        [Repeat(5)]
        public override void TestAugmentationOfGoldUsersAgainstRandomGroups()
        {
            base.TestAugmentationOfGoldUsersAgainstRandomGroups();
        }
    }
}
