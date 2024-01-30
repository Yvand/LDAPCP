using NUnit.Framework;
using System;
using Yvand.LdapClaimsProvider.Configuration;

namespace Yvand.LdapClaimsProvider.Tests
{
    [TestFixture]
    public class WrongConfigNoIdentityClaimTypeTests : ClaimsProviderTestsBase
    {
        public override bool DoAugmentationTest => false;

        public override void InitializeSettings()
        {
            base.InitializeSettings();
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
        public void TestAddSecondGroupClaimType()
        {
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

    [TestFixture]
    public class WrongUpdatesOnClaimTypesTests : ClaimsProviderTestsBase
    {
        public override bool DoAugmentationTest => false;
        const string ConfigUpdateErrorMessage = "Some changes made to list ClaimTypes are invalid and cannot be committed to configuration database. Inspect inner exception for more details about the error.";

        [Test]
        public void TryAddingWrongClaimTypeConfigTest()
        {
            ClaimTypeConfig ctConfig = new ClaimTypeConfig();

            // Add a ClaimTypeConfig with a claim type already set should throw exception InvalidOperationException
            ctConfig.ClaimType = UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType;
            ctConfig.DirectoryObjectClass = UnitTestsHelper.RandomDirectoryObjectClass;
            ctConfig.DirectoryObjectAttribute = UnitTestsHelper.RandomDirectoryObjectAttribute;
            Assert.Throws<InvalidOperationException>(() => base.Settings.ClaimTypes.Add(ctConfig), $"Add a ClaimTypeConfig with a claim type already set should throw exception InvalidOperationException with this message: \"Claim type '{UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType}' already exists in the collection\"");

            // Add a ClaimTypeConfig with IsAdditionalLdapSearchAttribute = false (default value) and LDAPAttribute / DirectoryObjectClass not set should throw an InvalidOperationException
            ctConfig.ClaimType = UnitTestsHelper.RandomClaimType;
            ctConfig.DirectoryObjectClass = String.Empty;
            ctConfig.DirectoryObjectAttribute = String.Empty;
            Assert.Throws<InvalidOperationException>(() => base.Settings.ClaimTypes.Add(ctConfig), $"Add a ClaimTypeConfig with IsAdditionalLdapSearchAttribute = false (default value) and LDAPAttribute / DirectoryObjectClass not set should throw exception InvalidOperationException with this message: \"Property LDAPAttribute and DirectoryObjectClass are required\"");

            // Add a ClaimTypeConfig with IsAdditionalLdapSearchAttribute = true and ClaimType set should throw an InvalidOperationException
            ctConfig.ClaimType = UnitTestsHelper.RandomClaimType;
            ctConfig.DirectoryObjectClass = UnitTestsHelper.RandomDirectoryObjectClass;
            ctConfig.DirectoryObjectAttribute = UnitTestsHelper.RandomDirectoryObjectAttribute;
            ctConfig.IsAdditionalLdapSearchAttribute = true;
            Assert.Throws<InvalidOperationException>(() => base.Settings.ClaimTypes.Add(ctConfig), $"Add a ClaimTypeConfig with IsAdditionalLdapSearchAttribute = true and ClaimType set should throw exception InvalidOperationException with this message: \"No claim type should be set if IsAdditionalLdapSearchAttribute is set to true\"");

            // Add a ClaimTypeConfig with EntityType 'Group' should should throw an InvalidOperationException
            ctConfig.ClaimType = UnitTestsHelper.RandomClaimType;
            ctConfig.DirectoryObjectAttribute = UnitTestsHelper.RandomDirectoryObjectAttribute;
            ctConfig.DirectoryObjectClass = UnitTestsHelper.RandomDirectoryObjectClass;
            ctConfig.DirectoryObjectType = DirectoryObjectType.Group;
            ctConfig.IsAdditionalLdapSearchAttribute = false;
            Assert.Throws<InvalidOperationException>(() => base.Settings.ClaimTypes.Add(ctConfig), "Add a ClaimTypeConfig with EntityType 'Group' should throw an InvalidOperationException");

            // Add a valid ClaimTypeConfig should succeed
            ctConfig.ClaimType = UnitTestsHelper.RandomClaimType;
            ctConfig.DirectoryObjectAttribute = UnitTestsHelper.RandomDirectoryObjectAttribute;
            ctConfig.DirectoryObjectClass = UnitTestsHelper.RandomDirectoryObjectClass;
            ctConfig.DirectoryObjectType = DirectoryObjectType.User;
            ctConfig.IsAdditionalLdapSearchAttribute = false;
            Assert.DoesNotThrow(() => base.Settings.ClaimTypes.Add(ctConfig), $"Add a valid ClaimTypeConfig should succeed");

            // Add a ClaimTypeConfig twice should throw an InvalidOperationException
            Assert.Throws<InvalidOperationException>(() => base.Settings.ClaimTypes.Add(ctConfig), $"Add a ClaimTypeConfig with a claim type already set should throw exception InvalidOperationException with this message: \"Claim type '{UnitTestsHelper.RandomClaimType}' already exists in the collection\"");

            // Delete the ClaimTypeConfig by calling method ClaimTypeConfigCollection.Remove(ClaimTypeConfig) should succeed
            Assert.That(base.Settings.ClaimTypes.Remove(ctConfig), Is.True, $"Delete the ClaimTypeConfig by calling method ClaimTypeConfigCollection.Remove(ClaimTypeConfig) should succeed");
        }
    }
}
