using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yvand.LdapClaimsProvider.Tests
{
    public class AugmentUsingLdapQueryTestss : ClaimsProviderTestsBase
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

        [TestCase("testLdapcpseUser_001@contoso.local", true, @"contoso.local\testLdapcpseGroup_2")]
        public void TestAugmentationOperationGroupRecursive(string claimValue, bool isMemberOfTrustedGroup, string groupValue)
        {
            base.TestAugmentationOperation(claimValue, isMemberOfTrustedGroup, groupValue);
        }
    }

    public class AugmentUsingLdapQueryAndSidAsGroupValueTests : ClaimsProviderTestsBase
    {
        protected override void InitializeSettings()
        {
            base.InitializeSettings();
            Settings.LdapConnections[0].GetGroupMembershipUsingDotNetHelpers = false;
            base.Settings.ClaimTypes.UpdateGroupIdentifier("group", "objectSid");
            base.Settings.ClaimTypes.GetMainConfigurationForDirectoryObjectType(Configuration.DirectoryObjectType.Group).ClaimValueLeadingToken = String.Empty;
            base.ApplySettings();
        }

        [Test]
        public override void CheckSettingsTest()
        {
            base.CheckSettingsTest();
        }

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
    }
}
