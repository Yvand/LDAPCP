using NUnit.Framework;
using System;
using Yvand.LdapClaimsProvider.Configuration;

namespace Yvand.LdapClaimsProvider.Tests
{
    [TestFixture]
    public class WrongConfigNoIdentityClaimTypeTests : ClaimsProviderTestsBase
    {
        public override bool DoAugmentationTest => false;

        public override void InitializeSettings(bool applyChanges)
        {
            base.InitializeSettings(false);
            ClaimTypeConfig randomClaimTypeConfig = new ClaimTypeConfig
            {
                ClaimType = UnitTestsHelper.RandomClaimType,
                DirectoryObjectClass = "user",
                DirectoryObjectAttribute = UnitTestsHelper.RandomDirectoryObjectAttribute,
            };
            base.Settings.ClaimTypes = new ClaimTypeConfigCollection(UnitTestsHelper.SPTrust) { randomClaimTypeConfig };
            ConfigurationShouldBeValid = false;
            base.TestSettingsAndApplyThemIfValid();
        }
    }

    [TestFixture]
    public class WrongConfigMultipleGroupClaimTypesTests : ClaimsProviderTestsBase
    {
        public override bool DoAugmentationTest => false;

        [Test]
        public override void InitializeSettings(bool applyChanges)
        {
            base.InitializeSettings(false);
            ClaimTypeConfig newGroupClaimTypeConfig = new ClaimTypeConfig
            {
                ClaimType = UnitTestsHelper.RandomClaimType,
                DirectoryObjectType = DirectoryObjectType.Group,
                DirectoryObjectClass = "group",
                DirectoryObjectAttribute = UnitTestsHelper.RandomDirectoryObjectAttribute,
            };
            Assert.Throws<InvalidOperationException>(() => base.Settings.ClaimTypes.Add(newGroupClaimTypeConfig), "ClaimTypes.Add should throw a InvalidOperationException because the configuration is invalid");
        }
    }
}
