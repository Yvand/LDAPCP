using NUnit.Framework;

namespace Yvand.LdapClaimsProvider.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    internal class BasicTests : EntityTestsBase
    {
        [Test, TestCaseSource(typeof(SearchEntityDataSource), nameof(SearchEntityDataSource.GetTestData), new object[] { EntityDataSourceType.AllAccounts })]
        [Repeat(UnitTestsHelper.TestRepeatCount)]
        public override void SearchEntities(SearchEntityData registrationData)
        {
            base.SearchEntities(registrationData);
        }

        [Test, TestCaseSource(typeof(ValidateEntityDataSource), nameof(ValidateEntityDataSource.GetTestData), new object[] { EntityDataSourceType.AllAccounts })]
        [MaxTime(UnitTestsHelper.MaxTime)]
        [Repeat(UnitTestsHelper.TestRepeatCount)]
        public override void ValidateClaim(ValidateEntityData registrationData)
        {
            base.ValidateClaim(registrationData);
        }

        [Test, TestCaseSource(typeof(ValidateEntityDataSource), nameof(ValidateEntityDataSource.GetTestData), new object[] { EntityDataSourceType.AllAccounts })]
        [Repeat(UnitTestsHelper.TestRepeatCount)]
        public override void AugmentEntity(ValidateEntityData registrationData)
        {
            base.AugmentEntity(registrationData);
        }

#if DEBUG
        ////[TestCaseSource(typeof(SearchEntityDataSourceCollection))]
        //public void DEBUG_SearchEntitiesFromCollection(string inputValue, string expectedCount, string expectedClaimValue)
        //{
        //    if (!TestSearch) { return; }

        //    TestSearchOperation(inputValue, Convert.ToInt32(expectedCount), expectedClaimValue);
        //}

        //[TestCase(@"group\ch", 1, @"contoso.local\group\chartest")]
        [TestCase(@"test_special)", 1, @"test_special_char@contoso.local")]
        //[TestCase(@"group\ch", 1, @"group\chartest")]
        [TestCase(@"testLdapcpseUser_001", 1, @"testLdapcpseUser_001@contoso.local")]
        public override void SearchEntities(string inputValue, int expectedResultCount, string expectedEntityClaimValue)
        {
            base.SearchEntities(inputValue, expectedResultCount, expectedEntityClaimValue);
        }

        //[TestCase("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", @"contoso.local\group\chartest", true)]
        //[TestCase("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", @"test)char@contoso.local", true)]
        //[TestCase("http://yvand.com/customType1", @"group\chartest", true)]
        [TestCase("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", @"yvan", false)]
        public override void ValidateClaim(string claimType, string claimValue, bool shouldValidate)
        {
            base.ValidateClaim(claimType, claimValue, shouldValidate);
        }

        [TestCase("FakeAccount", false)]
        [TestCase("yvand@contoso.local", true)]
        public override void AugmentEntity(string claimValue, bool shouldHavePermissions)
        {
            base.AugmentEntity(claimValue, shouldHavePermissions);
        }
#endif
    }
}
