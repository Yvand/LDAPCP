using NUnit.Framework;
using Yvand.LdapClaimsProvider.Configuration;

namespace Yvand.LdapClaimsProvider.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    internal class PrimaryGroupIdTests : ClaimsProviderTestsBase
    {
        public override void InitializeSettings()
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
            //if (applyChanges)
            //{
                TestSettingsAndApplyThemIfValid();
            //}
        }

        [TestCase(@"513", 1, @"513")]
        [TestCase(@"51", 0, @"")]
        [TestCase(@"5133", 0, @"")]
        public void TestPrimaryGroupIdClaimType(string inputValue, int expectedResultCount, string expectedEntityClaimValue)
        {
            base.TestSearchOperation(inputValue, expectedResultCount, expectedEntityClaimValue);
            base.TestValidationOperation(TestContext.Parameters["MultiPurposeCustomClaimType"], inputValue, expectedResultCount == 0 ? false : true);
        }
    }
}
