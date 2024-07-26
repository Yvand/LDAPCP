using NUnit.Framework;
using System.Linq;

namespace Yvand.LdapClaimsProvider.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    internal class BasicConfigurationTests : ClaimsProviderTestsBase
    {
        protected override void InitializeSettings()
        {
            base.InitializeSettings();
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
            TestSearchAndValidateForETestGroup(group);
        }

        [Test]
        [Repeat(2)]
        public override void TestAugmentationOfGoldUsersAgainstRandomGroups()
        {
            base.TestAugmentationOfGoldUsersAgainstRandomGroups();
        }


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

        ///// <summary>
        ///// Tests if the augmentation works as expected.
        ///// </summary>
        ///// <param name="registrationData"></param>
        //[Test, TestCaseSource(typeof(TestEntitySourceManager), nameof(TestEntitySourceManager.AllValidationEntities), null)]
        //[Repeat(UnitTestsHelper.TestRepeatCount)]
        //public virtual void TestAugmentationOperation(ValidateEntityScenario registrationData)
        //{
        //    TestAugmentationOperation(registrationData.ClaimValue, registrationData.IsMemberOfTrustedGroup, UnitTestsHelper.ValidGroupName);
        //}

        //[TestCase("FakeAccount", false)]
        //[TestCase("yvand@contoso.local", true)]
        //public void TestAugmentationOperation(string claimValue, bool isMemberOfTrustedGroup)
        //{
        //    base.TestAugmentationOperation(claimValue, isMemberOfTrustedGroup, UnitTestsHelper.ValidGroupName);
        //}

#if DEBUG
        [TestCase("testLdapcpUser_001")]
        [TestCase("testLdapcpUser_007")]
        public void DebugTestUser(string upnPrefix)
        {
            TestUser user = TestEntitySourceManager.AllTestUsers.First(x => x.UserPrincipalName.StartsWith(upnPrefix));
            base.TestSearchAndValidateForTestUser(user);
        }

        ////[TestCaseSource(typeof(SearchEntityDataSourceCollection))]
        //public void DEBUG_SearchEntitiesFromCollection(string inputValue, string expectedCount, string expectedClaimValue)
        //{
        //    if (!TestSearchTest) { return; }

        //    TestSearchOperation(inputValue, Convert.ToInt32(expectedCount), expectedClaimValue);
        //}

        //[TestCase(@"group\ch", 1, @"contoso.local\group\chartest")]
        [TestCase(@"test_special)", 1, @"testLdapcpUser_003@contoso.local")]
        [TestCase(@"firstname_002", 1, @"testLdapcpUser_002@contoso.local")]
        //[TestCase(@"group\ch", 1, @"group\chartest")]
        [TestCase(@"testLdapcpUser_001", 1, @"testLdapcpUser_001@contoso.local")]
        public void TestSearch(string inputValue, int expectedResultCount, string expectedEntityClaimValue)
        {
            base.TestSearchOperation(inputValue, expectedResultCount, expectedEntityClaimValue);
        }

        //[TestCase("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", @"contoso.local\group\chartest", true)]
        //[TestCase("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", @"test)char@contoso.local", true)]
        //[TestCase("http://yvand.com/customType1", @"group\chartest", true)]
        [TestCase("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn", @"yvan", false)]
        public void TestValidation(string claimType, string claimValue, bool shouldValidate)
        {
            base.TestValidationOperation(claimType, claimValue, shouldValidate);
        }
#endif
    }
}
