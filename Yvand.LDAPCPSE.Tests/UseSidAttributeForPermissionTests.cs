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

        [Test, TestCaseSource(typeof(TestEntitySourceManager), nameof(TestEntitySourceManager.GetSomeUsers), new object[] { TestEntitySourceManager.MaxNumberOfUsersToTest })]
        public void TestUsers(TestUser user)
        {
            base.TestSearchAndValidateForTestUser(user);
            base.TestAugmentationAgainst1RandomGroup(user);
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

        [Test, TestCaseSource(typeof(TestEntitySourceManager), nameof(TestEntitySourceManager.GetSomeUsers), new object[] { TestEntitySourceManager.MaxNumberOfUsersToTest })]
        public void TestUsers(TestUser user)
        {
            base.TestSearchAndValidateForTestUser(user);
            base.TestAugmentationAgainst1RandomGroup(user);
        }

        [Test, TestCaseSource(typeof(TestEntitySourceManager), nameof(TestEntitySourceManager.GetSomeUsers), new object[] { TestEntitySourceManager.MaxNumberOfUsersToTest })]
        public void TestUsersUsingSidAsInput(TestUser user)
        {
            string searchInput = user.SID;
            string claimValue = user.UserPrincipalName;
            base.TestSearchOperation(searchInput, 1, claimValue);
            base.TestValidationOperation(base.UserIdentifierClaimType, claimValue, true);
        }

#if DEBUG
        [TestCase(@"testLdapcpUser_001", 1, @"testLdapcpUser_001@contoso.local")]
        public void TestSidAttribute(string inputValue, int expectedResultCount, string expectedEntityClaimValue)
        {
            base.TestSearchOperation(inputValue, expectedResultCount, expectedEntityClaimValue);
            base.TestValidationOperation(base.UserIdentifierClaimType, expectedEntityClaimValue, expectedResultCount == 0 ? false : true);
        }
#endif
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

#if DEBUG
        [TestCase(UnitTestsHelper.ValidUserSid, 1, UnitTestsHelper.ValidUserSid)]
        [TestCase(@"S-1-5-21-0000000000-1611586658-188888215-107206", 0, @"")]
        public void TestSidAttribute(string inputValue, int expectedResultCount, string expectedEntityClaimValue)
        {
            base.TestSearchOperation(inputValue, expectedResultCount, expectedEntityClaimValue);
            base.TestValidationOperation(TestContext.Parameters["MultiPurposeCustomClaimType"], inputValue, expectedResultCount == 0 ? false : true);
        }
#endif
    }

    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class UseSidAttributeAsGroupIdentifierTests : ClaimsProviderTestsBase
    {
        protected override void InitializeSettings()
        {
            base.InitializeSettings();
            base.Settings.ClaimTypes.UpdateGroupIdentifier("group", "objectSid");
            base.Settings.ClaimTypes.GroupIdentifierConfig.ClaimValueLeadingToken = String.Empty;
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

#if DEBUG
        [TestCase(UnitTestsHelper.ValidGroupSid, 1, UnitTestsHelper.ValidGroupSid)]
        [TestCase("group1", 1, UnitTestsHelper.ValidGroupSid)]
        public void TestGroupSidAttribute(string inputValue, int expectedResultCount, string expectedEntityClaimValue)
        {
            base.TestSearchOperation(inputValue, expectedResultCount, expectedEntityClaimValue);
            base.TestValidationOperation(base.GroupIdentifierClaimType, expectedEntityClaimValue, expectedResultCount == 0 ? false : true);
        }

        [TestCase("FakeAccount", false)]
        [TestCase("yvand@contoso.local", true)]
        public void TestAugmentationOperation(string claimValue, bool isMemberOfTrustedGroup)
        {
            base.TestAugmentationOperation(claimValue, isMemberOfTrustedGroup, UnitTestsHelper.ValidGroupSid);
        }
#endif
    }
}
