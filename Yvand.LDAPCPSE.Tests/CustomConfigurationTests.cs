using NUnit.Framework;

namespace Yvand.LdapClaimsProvider.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    internal class CustomConfigurationTests : ClaimsProviderTestsBase
    {
        protected override void InitializeSettings()
        {
            base.InitializeSettings();
            Settings.AddWildcardAsPrefixOfInput = true;
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

#if DEBUG
        [Test, TestCaseSource(typeof(TestEntitySourceManager), nameof(TestEntitySourceManager.AllSearchEntities), null)]
        [Repeat(UnitTestsHelper.TestRepeatCount)]
        public void TestSearch(SearchEntityScenario registrationData)
        {
            base.TestSearchOperation(registrationData.Input, registrationData.SearchResultCount, registrationData.SearchResultSingleEntityClaimValue);
        }

        [Test, TestCaseSource(typeof(TestEntitySourceManager), nameof(TestEntitySourceManager.AllValidationEntities), null)]
        [MaxTime(UnitTestsHelper.MaxTime)]
        [Repeat(UnitTestsHelper.TestRepeatCount)]
        public void TestValidation(ValidateEntityScenario registrationData)
        {
            base.TestValidationOperation(registrationData);
        }

        [TestCase("testLdapcpUser_014")]
        public void DebugTestUser(string upnPrefix)
        {
            TestUser user = TestEntitySourceManager.FindUser(upnPrefix);
            base.TestSearchAndValidateForTestUser(user);

            TestGroup group = TestEntitySourceManager.FindGroup("testLdapcpGroup_005");
            base.TestAugmentationAgainstGroup(user, group);
        }
#endif
    }
}
