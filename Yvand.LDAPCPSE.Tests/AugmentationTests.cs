using NUnit.Framework;
using System;

namespace Yvand.LdapClaimsProvider.Tests
{
    public class AugmentUsingCustomConnectionAndNoHelperTestss : ClaimsProviderTestsBase
    {
        protected override void InitializeSettings()
        {
            base.InitializeSettings();
            Settings.LdapConnections[0].GetGroupMembershipUsingDotNetHelpers = false;
            base.ApplySettings();
        }

        [Test]
        public override void CheckSettingsTest()
        {
            base.CheckSettingsTest();
        }

        [Test, TestCaseSource(typeof(TestEntitySourceManager), nameof(TestEntitySourceManager.GetUsersMembersOfNestedGroups), null)]
        public void TestUsersMembersOfNestedGroups(TestUser user)
        {
            base.TestAugmentationAgainstGroupsWithNestedGroupsAsMembers(user);
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
        //[TestCase("testLdapcpseUser_001@contoso.local", true, @"contoso.local\testLdapcpseGroup_2")]
        //public void TestAugmentationOperationGroupRecursive(string claimValue, bool isMemberOfTrustedGroup, string groupValue)
        //{
        //    base.TestAugmentationOperation(claimValue, isMemberOfTrustedGroup, groupValue);
        //}
#endif
    }

    public class AugmentUsingDefaultConnectionAndNoHelperTestss : ClaimsProviderTestsBase
    {
        protected override void InitializeSettings()
        {
            base.InitializeSettings();
            Settings.LdapConnections[0].UseDefaultADConnection = true;
            Settings.LdapConnections[0].LdapPath = String.Empty;
            Settings.LdapConnections[0].GetGroupMembershipUsingDotNetHelpers = false;
            base.ApplySettings();
        }

        [Test]
        public override void CheckSettingsTest()
        {
            base.CheckSettingsTest();
        }

        [Test, TestCaseSource(typeof(TestEntitySourceManager), nameof(TestEntitySourceManager.GetUsersMembersOfNestedGroups), null)]
        public void TestUsersMembersOfNestedGroups(TestUser user)
        {
            base.TestAugmentationAgainstGroupsWithNestedGroupsAsMembers(user);
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

    public class AugmentUsingCustomConnectionAndUsingHelperTestss : ClaimsProviderTestsBase
    {
        protected override void InitializeSettings()
        {
            base.InitializeSettings();
            Settings.LdapConnections[0].GetGroupMembershipUsingDotNetHelpers = true;
            base.ApplySettings();
        }

        [Test]
        public override void CheckSettingsTest()
        {
            base.CheckSettingsTest();
        }

        [Test, TestCaseSource(typeof(TestEntitySourceManager), nameof(TestEntitySourceManager.GetUsersMembersOfNestedGroups), null)]
        public void TestUsersMembersOfNestedGroups(TestUser user)
        {
            base.TestAugmentationAgainstGroupsWithNestedGroupsAsMembers(user);
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

    public class AugmentUsingDefaultConnectionAndUsingHelperTestss : ClaimsProviderTestsBase
    {
        protected override void InitializeSettings()
        {
            base.InitializeSettings();
            Settings.LdapConnections[0].UseDefaultADConnection = true;
            Settings.LdapConnections[0].LdapPath = String.Empty;
            Settings.LdapConnections[0].GetGroupMembershipUsingDotNetHelpers = true;
            base.ApplySettings();
        }

        [Test]
        public override void CheckSettingsTest()
        {
            base.CheckSettingsTest();
        }

        [Test, TestCaseSource(typeof(TestEntitySourceManager), nameof(TestEntitySourceManager.GetUsersMembersOfNestedGroups), null)]
        public void TestUsersMembersOfNestedGroups(TestUser user)
        {
            base.TestAugmentationAgainstGroupsWithNestedGroupsAsMembers(user);
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
        [TestCase("testLdapcpUser_040", "testLdapcpGroup_022")]
        public void DebugTestUser(string upnPrefix, string groupName)
        {
            TestUser user = TestEntitySourceManager.FindUser(upnPrefix);
            TestGroup group = TestEntitySourceManager.FindGroup(groupName);
            TestAugmentationAgainstGroup(user, group);
        }
#endif
    }

    public class AugmentUsingLdapQueryAndSidAsGroupValueTests : ClaimsProviderTestsBase
    {
        protected override void InitializeSettings()
        {
            base.InitializeSettings();
            Settings.LdapConnections[0].GetGroupMembershipUsingDotNetHelpers = false;
            base.Settings.ClaimTypes.UpdateGroupIdentifier("group", "objectSid");
            base.Settings.ClaimTypes.GroupIdentifierConfig.ClaimValueLeadingToken = String.Empty;
            base.ApplySettings();
        }

        [Test]
        public override void CheckSettingsTest()
        {
            base.CheckSettingsTest();
        }

        [Test, TestCaseSource(typeof(TestEntitySourceManager), nameof(TestEntitySourceManager.GetUsersMembersOfNestedGroups), null)]
        public void TestUsersMembersOfNestedGroups(TestUser user)
        {
            base.TestAugmentationAgainstGroupsWithNestedGroupsAsMembers(user);
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
