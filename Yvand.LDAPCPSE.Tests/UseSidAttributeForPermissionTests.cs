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

        [Test, TestCaseSource(typeof(TestEntitySourceManager), nameof(TestEntitySourceManager.GetSomeUsers), new object[] { TestEntitySourceManager.MaxNumberOfUsersToTest })]
        public void TestSidAttribute(TestUser user)
        {
            string searchInput = user.SID;
            string claimValue = user.SID;
            string claimType = base.UserIdentifierClaimType;
            base.TestSearchOperation(searchInput, 1, claimValue);
            base.TestValidationOperation(claimType, claimValue, true);
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
        public void TestSidAttribute(TestUser user)
        {
            string searchInput = user.SID;
            string claimValue = user.UserPrincipalName;
            string claimType = base.UserIdentifierClaimType;
            base.TestSearchOperation(searchInput, 1, claimValue);
            base.TestValidationOperation(claimType, claimValue, true);
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
        [TestCase(@"testLdapcpUser_001", 1, @"testLdapcpUser_001@contoso.local")]
        public void TestSidAttribute(string inputValue, int expectedResultCount, string expectedEntityClaimValue)
        {
            base.TestSearchOperation(inputValue, expectedResultCount, expectedEntityClaimValue);
            base.TestValidationOperation(base.UserIdentifierClaimType, expectedEntityClaimValue, expectedResultCount == 0 ? false : true);
        }
#endif
    }

#if DEBUG
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

        [Test, TestCaseSource(typeof(TestEntitySourceManager), nameof(TestEntitySourceManager.GetSomeUsers), new object[] { TestEntitySourceManager.MaxNumberOfUsersToTest })]
        public void TestSidAttribute(TestUser user)
        {
            string searchInput = user.SID;
            string claimValue = user.SID;
            string claimType = TestContext.Parameters["MultiPurposeCustomClaimType"];
            base.TestSearchOperation(searchInput, 1, claimValue);
            base.TestValidationOperation(claimType, claimValue, true);
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
    }
#endif

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

        [Test, TestCaseSource(typeof(TestEntitySourceManager), nameof(TestEntitySourceManager.GetSomeGroups), new object[] { TestEntitySourceManager.MaxNumberOfGroupsToTest })]
        public void TestSidAttribute(TestGroup group)
        {
            string searchInput = group.SamAccountName;
            string claimValue = group.SID;
            string claimType = base.Settings.ClaimTypes.GroupIdentifierConfig.ClaimType;
            base.TestSearchOperation(searchInput, 1, claimValue);
            base.TestValidationOperation(claimType, claimValue, true);
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
    }
}
