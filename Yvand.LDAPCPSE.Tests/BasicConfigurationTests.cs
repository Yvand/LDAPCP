using NUnit.Framework;

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

        [Test, TestCaseSource(typeof(SearchEntityDataSource), nameof(SearchEntityDataSource.GetTestData), new object[] { EntityDataSourceType.AllAccounts })]
        [Repeat(UnitTestsHelper.TestRepeatCount)]
        public void TestSearch(SearchEntityData registrationData)
        {
            base.TestSearchOperation(registrationData.Input, registrationData.SearchResultCount, registrationData.SearchResultSingleEntityClaimValue);
        }

        [Test, TestCaseSource(typeof(ValidateEntityDataSource), nameof(ValidateEntityDataSource.GetTestData), new object[] { EntityDataSourceType.AllAccounts })]
        [MaxTime(UnitTestsHelper.MaxTime)]
        [Repeat(UnitTestsHelper.TestRepeatCount)]
        public void TestValidation(ValidateEntityData registrationData)
        {
            base.TestValidationOperation(registrationData);
        }

        /// <summary>
        /// Tests if the augmentation works as expected.
        /// </summary>
        /// <param name="registrationData"></param>
        [Test, TestCaseSource(typeof(ValidateEntityDataSource), nameof(ValidateEntityDataSource.GetTestData), new object[] { EntityDataSourceType.AllAccounts })]
        [Repeat(UnitTestsHelper.TestRepeatCount)]
        public virtual void TestAugmentationOperation(ValidateEntityData registrationData)
        {
            TestAugmentationOperation(registrationData.ClaimValue, registrationData.IsMemberOfTrustedGroup, UnitTestsHelper.ValidGroupName);
        }

        [TestCase("FakeAccount", false)]
        [TestCase("yvand@contoso.local", true)]
        public void TestAugmentationOperation(string claimValue, bool isMemberOfTrustedGroup)
        {
            base.TestAugmentationOperation(claimValue, isMemberOfTrustedGroup, UnitTestsHelper.ValidGroupName);
        }

#if DEBUG
        ////[TestCaseSource(typeof(SearchEntityDataSourceCollection))]
        //public void DEBUG_SearchEntitiesFromCollection(string inputValue, string expectedCount, string expectedClaimValue)
        //{
        //    if (!TestSearchTest) { return; }

        //    TestSearchOperation(inputValue, Convert.ToInt32(expectedCount), expectedClaimValue);
        //}

        //[TestCase(@"group\ch", 1, @"contoso.local\group\chartest")]
        [TestCase(@"test_special)", 1, @"test_special_char@contoso.local")]
        //[TestCase(@"group\ch", 1, @"group\chartest")]
        [TestCase(@"testLdapcpseUser_001", 1, @"testLdapcpseUser_001@contoso.local")]
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
