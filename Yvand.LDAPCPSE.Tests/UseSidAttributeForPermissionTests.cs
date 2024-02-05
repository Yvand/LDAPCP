using NUnit.Framework;
using System;
using System.Diagnostics;
using Yvand.LdapClaimsProvider.Configuration;

namespace Yvand.LdapClaimsProvider.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class UseSidAttributeAsUserIdentifierPermissionTests : ClaimsProviderTestsBase
    {
        protected override void InitializeSettings()
        {
            base.InitializeSettings();
            base.Settings.ClaimTypes.UpdateUserIdentifier("user", "objectSid");
            Trace.TraceInformation($"{DateTime.Now:s} [{this.GetType().Name}] Initialized custom settings.");
            base.ApplySettings();
        }

        [Test]
        public override void CheckSettingsTest()
        {
            base.CheckSettingsTest();
        }

        [TestCase(UnitTestsHelper.ValidTrustedUserSid, 1, UnitTestsHelper.ValidTrustedUserSid)]
        [TestCase(@"testLdapcpseUser_001", 1, UnitTestsHelper.ValidTrustedUserSid)]
        [TestCase(@"S-1-5-21-0000000000-1611586658-188888215-107206", 0, @"")]
        public void TestSidAttribute(string inputValue, int expectedResultCount, string expectedEntityClaimValue)
        {
            base.TestSearchOperation(inputValue, expectedResultCount, expectedEntityClaimValue);
            base.TestValidationOperation(base.UserIdentifierClaimType, expectedEntityClaimValue, expectedResultCount == 0 ? false : true);
        }
    }

    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class UseSidAsSearchAttributeForUserIdentifierTests : ClaimsProviderTestsBase
    {
        protected override void InitializeSettings()
        {
            base.InitializeSettings();
            ClaimTypeConfig ctConfigPgidAttribute = new ClaimTypeConfig
            {
                ClaimType = String.Empty,
                IsAdditionalLdapSearchAttribute = true,
                DirectoryObjectType = DirectoryObjectType.User,
                DirectoryObjectClass = "user",
                DirectoryObjectAttribute = "objectSid",
                DirectoryObjectAttributeSupportsWildcard = false,
                SPEntityDataKey = "User",
            };
            base.Settings.ClaimTypes.Add(ctConfigPgidAttribute);
            Trace.TraceInformation($"{DateTime.Now:s} [{this.GetType().Name}] Initialized custom settings.");
            base.ApplySettings();
        }

        [Test]
        public override void CheckSettingsTest()
        {
            base.CheckSettingsTest();
        }

        [TestCase(UnitTestsHelper.ValidTrustedUserSid, 1, @"testLdapcpseUser_001@contoso.local")]
        [TestCase(@"testLdapcpseUser_001", 1, @"testLdapcpseUser_001@contoso.local")]
        public void TestSidAttribute(string inputValue, int expectedResultCount, string expectedEntityClaimValue)
        {
            base.TestSearchOperation(inputValue, expectedResultCount, expectedEntityClaimValue);
            base.TestValidationOperation(base.UserIdentifierClaimType, expectedEntityClaimValue, expectedResultCount == 0 ? false : true);
        }
    }

    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class UseSidAttributeForUserPermissionTests : ClaimsProviderTestsBase
    {
        protected override void InitializeSettings()
        {
            base.InitializeSettings();
            ClaimTypeConfig ctConfigPgidAttribute = new ClaimTypeConfig
            {
                ClaimType = TestContext.Parameters["MultiPurposeCustomClaimType"], // WIF4_5.ClaimTypes.PrimaryGroupSid
                ClaimTypeDisplayName = "SID",
                DirectoryObjectType = DirectoryObjectType.User,
                DirectoryObjectClass = "user",
                DirectoryObjectAttribute = "objectSid",
                DirectoryObjectAttributeSupportsWildcard = false,
            };
            base.Settings.ClaimTypes.Add(ctConfigPgidAttribute);
            Trace.TraceInformation($"{DateTime.Now:s} [{this.GetType().Name}] Initialized custom settings.");
            base.ApplySettings();
        }

        [Test]
        public override void CheckSettingsTest()
        {
            base.CheckSettingsTest();
        }

        [TestCase(UnitTestsHelper.ValidTrustedUserSid, 1, UnitTestsHelper.ValidTrustedUserSid)]
        [TestCase(@"S-1-5-21-0000000000-1611586658-188888215-107206", 0, @"")]
        public void TestSidAttribute(string inputValue, int expectedResultCount, string expectedEntityClaimValue)
        {
            base.TestSearchOperation(inputValue, expectedResultCount, expectedEntityClaimValue);
            base.TestValidationOperation(TestContext.Parameters["MultiPurposeCustomClaimType"], inputValue, expectedResultCount == 0 ? false : true);
        }
    }

    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class UseSidAttributeAsGroupIdentifierTests : ClaimsProviderTestsBase
    {
        protected override void InitializeSettings()
        {
            base.InitializeSettings();
            base.Settings.ClaimTypes.UpdateGroupIdentifier("group", "objectSid");
            base.Settings.ClaimTypes.GetMainConfigurationForDirectoryObjectType(DirectoryObjectType.Group).ClaimValueLeadingToken = String.Empty;
            ClaimTypeConfig ctConfigPgidAttribute = new ClaimTypeConfig
            {
                ClaimType = String.Empty,
                IsAdditionalLdapSearchAttribute = true,
                DirectoryObjectType = DirectoryObjectType.Group,
                DirectoryObjectClass = "group",
                DirectoryObjectAttribute = "sAMAccountName",
            };
            base.Settings.ClaimTypes.Add(ctConfigPgidAttribute);
            Trace.TraceInformation($"{DateTime.Now:s} [{this.GetType().Name}] Initialized custom settings.");
            base.ApplySettings();
        }

        [Test]
        public override void CheckSettingsTest()
        {
            base.CheckSettingsTest();
        }

        [TestCase(UnitTestsHelper.ValidTrustedGroupSid, 1, UnitTestsHelper.ValidTrustedGroupSid)]
        [TestCase("group1", 1, UnitTestsHelper.ValidTrustedGroupSid)]
        public void TestGroupSidAttribute(string inputValue, int expectedResultCount, string expectedEntityClaimValue)
        {
            base.TestSearchOperation(inputValue, expectedResultCount, expectedEntityClaimValue);
            base.TestValidationOperation(base.GroupIdentifierClaimType, expectedEntityClaimValue, expectedResultCount == 0 ? false : true);
        }

        [TestCase("FakeAccount", false)]
        [TestCase("yvand@contoso.local", true)]
        public void TestAugmentationOperation(string claimValue, bool isMemberOfTrustedGroup)
        {
            base.TestAugmentationOperation(claimValue, isMemberOfTrustedGroup, UnitTestsHelper.ValidTrustedGroupSid);
        }
    }
}
