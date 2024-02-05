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
            TestAugmentationOperation(registrationData.ClaimValue, registrationData.IsMemberOfTrustedGroup, UnitTestsHelper.TrustedGroupToAdd_ClaimValue);
        }

        [TestCase("FakeAccount", false)]
        [TestCase("yvand@contoso.local", true)]
        public void TestAugmentationOperation(string claimValue, bool isMemberOfTrustedGroup)
        {
            base.TestAugmentationOperation(claimValue, isMemberOfTrustedGroup, UnitTestsHelper.TrustedGroupToAdd_ClaimValue);
        }
    }

    public class AugmentUsingLdapQueryAndSidAsGroupValueTests : ClaimsProviderTestsBase
    {
        protected override void InitializeSettings()
        {
            base.InitializeSettings();
            Settings.LdapConnections[0].GetGroupMembershipUsingDotNetHelpers = false;
            base.Settings.ClaimTypes.UpdateGroupIdentifier("group", "objectSid");
            base.ApplySettings();
        }

        [Test]
        public override void CheckSettingsTest()
        {
            base.CheckSettingsTest();
        }

        // Using the objectSid on groups is not supported if GetGroupMembershipUsingDotNetHelpers == false
        //[TestCase("FakeAccount", false)]
        //[TestCase("yvand@contoso.local", true)]
        //public void TestAugmentationOperation(string claimValue, bool isMemberOfTrustedGroup)
        //{
        //    base.TestAugmentationOperation(claimValue, isMemberOfTrustedGroup, UnitTestsHelper.ValidTrustedGroupSid);
        //}
    }
}
