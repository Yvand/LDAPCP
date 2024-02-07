using NUnit.Framework;
using Yvand.LdapClaimsProvider.Configuration;

namespace Yvand.LdapClaimsProvider.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class BypassDirectoryOnClaimTypesTests : ClaimsProviderTestsBase
    {
        protected override void InitializeSettings()
        {
            base.InitializeSettings();
            Settings.ClaimTypes.GetIdentifierConfiguration(DirectoryObjectType.User).LeadingKeywordToBypassDirectory = "bypass-user:";
            Settings.ClaimTypes.GetIdentifierConfiguration(DirectoryObjectType.Group).LeadingKeywordToBypassDirectory = "bypass-group:";
            base.ApplySettings();
        }

        [Test]
        public override void CheckSettingsTest()
        {
            base.CheckSettingsTest();
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
    }
}
