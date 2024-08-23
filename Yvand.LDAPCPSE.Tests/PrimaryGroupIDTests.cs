using NUnit.Framework;
using Yvand.LdapClaimsProvider.Configuration;

namespace Yvand.LdapClaimsProvider.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    internal class PrimaryGroupIdTests : ClaimsProviderTestsBase
    {
        protected override void InitializeSettings()
        {
            base.InitializeSettings();
            ClaimTypeConfig ctConfigPgidAttribute = new ClaimTypeConfig
            {
                ClaimType = TestContext.Parameters["MultiPurposeCustomClaimType"], // WIF4_5.ClaimTypes.PrimaryGroupSid
                ClaimTypeDisplayName = "primaryGroupID",
                DirectoryObjectType = DirectoryObjectType.User,
                SPEntityType = ClaimsProviderConstants.GroupClaimEntityType,
                DirectoryObjectClass = "user",
                DirectoryObjectAttribute = "primaryGroupID",
                DirectoryObjectAttributeSupportsWildcard = false,
            };
            Settings.ClaimTypes.Add(ctConfigPgidAttribute);
            base.ApplySettings();
        }

        [Test]
        public override void CheckSettingsTest()
        {
            base.CheckSettingsTest();
        }

// No custom additional claim type available in the trust in live tests
#if DEBUG
        [TestCase(@"513", 1, @"513")]
        [TestCase(@"51", 0, @"")]
        [TestCase(@"5133", 0, @"")]
        public void TestPrimaryGroupIdClaimType(string inputValue, int expectedResultCount, string expectedEntityClaimValue)
        {
            base.TestSearchOperation(inputValue, expectedResultCount, expectedEntityClaimValue);
            base.TestValidationOperation(TestContext.Parameters["MultiPurposeCustomClaimType"], expectedEntityClaimValue, expectedResultCount == 0 ? false : true);
        }
#endif

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
