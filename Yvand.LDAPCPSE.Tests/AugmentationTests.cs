using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        [Test, TestCaseSource(typeof(TestEntitySourceManager), nameof(TestEntitySourceManager.AllValidationEntities), null)]
        [Repeat(UnitTestsHelper.TestRepeatCount)]
        public virtual void TestAugmentationOperation(ValidateEntityScenario registrationData)
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
        [TestCase("testLdapcpseUser_001@contoso.local", true, @"contoso.local\testLdapcpseGroup_2")]
        public void TestAugmentationOperationGroupRecursive(string claimValue, bool isMemberOfTrustedGroup, string groupValue)
        {
            base.TestAugmentationOperation(claimValue, isMemberOfTrustedGroup, groupValue);
        }
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

        [Test, TestCaseSource(typeof(TestEntitySourceManager), nameof(TestEntitySourceManager.AllValidationEntities), null)]
        [Repeat(UnitTestsHelper.TestRepeatCount)]
        public virtual void TestAugmentationOperation(ValidateEntityScenario registrationData)
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
        [TestCase("testLdapcpseUser_001@contoso.local", true, @"contoso.local\testLdapcpseGroup_2")]
        public void TestAugmentationOperationGroupRecursive(string claimValue, bool isMemberOfTrustedGroup, string groupValue)
        {
            base.TestAugmentationOperation(claimValue, isMemberOfTrustedGroup, groupValue);
        }
#endif
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

        [Test, TestCaseSource(typeof(TestEntitySourceManager), nameof(TestEntitySourceManager.AllValidationEntities), null)]
        [Repeat(UnitTestsHelper.TestRepeatCount)]
        public virtual void TestAugmentationOperation(ValidateEntityScenario registrationData)
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
        [TestCase("testLdapcpseUser_001@contoso.local", true, @"contoso.local\testLdapcpseGroup_2")]
        public void TestAugmentationOperationGroupRecursive(string claimValue, bool isMemberOfTrustedGroup, string groupValue)
        {
            base.TestAugmentationOperation(claimValue, isMemberOfTrustedGroup, groupValue);
        }
#endif
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

        [Test, TestCaseSource(typeof(TestEntitySourceManager), nameof(TestEntitySourceManager.AllValidationEntities), null)]
        [Repeat(UnitTestsHelper.TestRepeatCount)]
        public virtual void TestAugmentationOperation(ValidateEntityScenario registrationData)
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
        [TestCase("testLdapcpseUser_001@contoso.local", true, @"contoso.local\testLdapcpseGroup_2")]
        public void TestAugmentationOperationGroupRecursive(string claimValue, bool isMemberOfTrustedGroup, string groupValue)
        {
            base.TestAugmentationOperation(claimValue, isMemberOfTrustedGroup, groupValue);
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

#if DEBUG
        [TestCase("FakeAccount", false)]
        [TestCase("yvand@contoso.local", true)]
        public void TestAugmentationOperation(string claimValue, bool isMemberOfTrustedGroup)
        {
            base.TestAugmentationOperation(claimValue, isMemberOfTrustedGroup, UnitTestsHelper.ValidGroupSid);
        }

        [TestCase("testLdapcpseUser_001@contoso.local", true, @"S-1-5-21-2647467245-1611586658-188888215-110602")] // testLdapcpseGroup_2
        public void TestAugmentationOperationGroupRecursive(string claimValue, bool isMemberOfTrustedGroup, string groupValue)
        {
            base.TestAugmentationOperation(claimValue, isMemberOfTrustedGroup, groupValue);
        }
#endif
    }
}
