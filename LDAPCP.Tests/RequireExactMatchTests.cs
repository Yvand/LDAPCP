using NUnit.Framework;

namespace LDAPCP.Tests
{
    [TestFixture]
    public class RequireExactMatchOnBaseConfigTests : BackupCurrentConfig
    {
        public override void InitializeConfiguration()
        {
            base.InitializeConfiguration();

            // Extra initialization for current test class
            Config.FilterExactMatchOnlyProp = true;
            Config.Update();
        }

        [Test, TestCaseSource(typeof(ValidateEntityDataSource), "GetTestData")]
        [Repeat(UnitTestsHelper.TestRepeatCount)]
        public void RequireExactMatchDuringSearch(ValidateEntityData registrationData)
        {
            int expectedCount = registrationData.ShouldValidate ? 1 : 0;
            UnitTestsHelper.TestSearchOperation(registrationData.ClaimValue, expectedCount, registrationData.ClaimValue);
        }
    }

    [TestFixture]
    public class RequireExactMatchOnCustomConfigTests : CustomConfigTests
    {
        public override void InitializeConfiguration()
        {
            base.InitializeConfiguration();

            // Extra initialization for current test class
            Config.FilterExactMatchOnlyProp = true;
            Config.Update();
        }

        [Test, TestCaseSource(typeof(ValidateEntityDataSource), "GetTestData")]
        [Repeat(UnitTestsHelper.TestRepeatCount)]
        public void RequireExactMatchDuringSearch(ValidateEntityData registrationData)
        {
            int expectedCount = registrationData.ShouldValidate ? 1 : 0;
            UnitTestsHelper.TestSearchOperation(registrationData.ClaimValue, expectedCount, registrationData.ClaimValue);
        }
    }
}
