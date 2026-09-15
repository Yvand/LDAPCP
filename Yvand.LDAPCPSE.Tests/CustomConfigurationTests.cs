using NUnit.Framework;
using System.Text.RegularExpressions;

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
            Settings.ClaimTypes.GroupIdentifierConfig.ClaimValueLeadingToken = string.Empty; // It makes TestSearchGroupInputDoesNotStartWithResult() easier
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

        [Test, TestCaseSource(typeof(TestEntitySourceManager), nameof(TestEntitySourceManager.GetSomeUsers), new object[] { TestEntitySourceManager.MaxNumberOfUsersToTest })]
        public void TestSearchUserInputDoesNotStartWithResult(TestUser user)
        {
            base.TestSearchOperation(user.SamAccountName.Substring(6), 1, user.UserPrincipalName);
            base.TestSearchOperation(user.UserPrincipalName.Substring(6), 1, user.UserPrincipalName);
        }

        [Test, TestCaseSource(typeof(TestEntitySourceManager), nameof(TestEntitySourceManager.GetSomeGroups), new object[] { TestEntitySourceManager.MaxNumberOfGroupsToTest })]
        public void TestSearchGroupInputDoesNotStartWithResult(TestGroup group)
        {
            base.TestSearchOperation(group.SamAccountName.Substring(6), 1, group.SamAccountName);
        }


#if DEBUG
        [Test, TestCaseSource(typeof(TestEntitySourceManager), nameof(TestEntitySourceManager.AllValidationEntities), null)]
        [MaxTime(UnitTestsHelper.MaxTime)]
        [Repeat(UnitTestsHelper.TestRepeatCount)]
        public void TestValidation(ValidateEntityScenario registrationData)
        {
            base.TestValidationOperation(registrationData);
        }

        [TestCase("testLdapcpUser_007")]
        public void DebugTestUser(string upnPrefix)
        {
            TestUser user = TestEntitySourceManager.FindUser(upnPrefix);
            base.TestSearchOperation(upnPrefix.Substring(6), 1, user.UserPrincipalName);
        }
#endif
    }
}
