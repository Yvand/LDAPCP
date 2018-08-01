using ldapcp;
using NUnit.Framework;
using System;
using System.Linq;

namespace LDAPCP.Tests
{
    [TestFixture]
    public class ModifyConfigTests
    {
        private LDAPCPConfig Config;

        [OneTimeSetUp]
        public void Init()
        {
            LDAPCPConfig configFromConfigDB = LDAPCPConfig.GetConfiguration(UnitTestsHelper.ClaimsProviderConfigName);
            // Create a local copy, otherwise changes will impact the whole process (even without calling Update method)
            Config = configFromConfigDB.CopyPersistedProperties();
            // Reset configuration to test its default for the tests
            Config.ResetCurrentConfiguration();
        }

        [Test]
        public void AddClaimTypeConfig()
        {
            ClaimTypeConfig ctConfig = new ClaimTypeConfig();

            // Adding a ClaimTypeConfig with a claim type already set should fail
            ctConfig.ClaimType = UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType;
            ctConfig.LDAPAttribute = UnitTestsHelper.RandomLDAPAttribute;
            ctConfig.LDAPClass = UnitTestsHelper.RandomLDAPClass;
            Assert.Throws<InvalidOperationException>(() => Config.ClaimTypes.Add(ctConfig));

            // Adding a ClaimTypeConfig with UseMainClaimTypeOfDirectoryObject = false (default value) and LDAPAttribute / LDAPClass not set should fail
            ctConfig.ClaimType = UnitTestsHelper.RandomClaimType;
            ctConfig.LDAPAttribute = String.Empty;
            ctConfig.LDAPClass = String.Empty;
            Assert.Throws<InvalidOperationException>(() => Config.ClaimTypes.Add(ctConfig));

            // Adding a ClaimTypeConfig with UseMainClaimTypeOfDirectoryObject = true and ClaimType set should fail
            ctConfig.ClaimType = UnitTestsHelper.RandomClaimType;
            ctConfig.LDAPAttribute = UnitTestsHelper.RandomLDAPAttribute;
            ctConfig.LDAPClass = UnitTestsHelper.RandomLDAPClass;
            ctConfig.UseMainClaimTypeOfDirectoryObject = true;
            Assert.Throws<InvalidOperationException>(() => Config.ClaimTypes.Add(ctConfig));

            // Adding a valid ClaimTypeConfig with EntityType 'Group' should succeed
            ctConfig.ClaimType = UnitTestsHelper.RandomClaimType;
            ctConfig.LDAPAttribute = UnitTestsHelper.RandomLDAPAttribute;
            ctConfig.LDAPClass = UnitTestsHelper.RandomLDAPClass;
            ctConfig.EntityType = DirectoryObjectType.Group;
            ctConfig.UseMainClaimTypeOfDirectoryObject = false;
            Assert.DoesNotThrow(() => Config.ClaimTypes.Add(ctConfig));
            Assert.IsTrue(Config.ClaimTypes.Remove(UnitTestsHelper.RandomClaimType));

            // Adding a valid ClaimTypeConfig should succeed
            ctConfig.ClaimType = UnitTestsHelper.RandomClaimType;
            ctConfig.LDAPAttribute = UnitTestsHelper.RandomLDAPAttribute;
            ctConfig.LDAPClass = UnitTestsHelper.RandomLDAPClass;
            ctConfig.EntityType = DirectoryObjectType.User;
            ctConfig.UseMainClaimTypeOfDirectoryObject = false;
            Assert.DoesNotThrow(() => Config.ClaimTypes.Add(ctConfig));

            // Adding a ClaimTypeConfig twice should fail
            Assert.Throws<InvalidOperationException>(() => Config.ClaimTypes.Add(ctConfig));

            // Deleting the ClaimTypeConfig should succeed
            Assert.IsTrue(Config.ClaimTypes.Remove(ctConfig));
        }

        [Test]
        public void ModifyOrDeleteIdentityClaimTypeConfig()
        {
            // Deleting identity claim type from its claim type should fail
            string identityClaimType = UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType;
            Assert.Throws<InvalidOperationException>(() => Config.ClaimTypes.Remove(identityClaimType));

            // Deleting identity claim type from its ClaimTypeConfig should fail
            ClaimTypeConfig identityCTConfig = Config.ClaimTypes.FirstOrDefault(x => String.Equals(UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType, x.ClaimType, StringComparison.InvariantCultureIgnoreCase));
            Assert.IsNotNull(identityCTConfig);
            Assert.Throws<InvalidOperationException>(() => Config.ClaimTypes.Remove(identityCTConfig));

            // Modify identity ClaimTypeConfig to set its EntityType to Group should fail
            identityCTConfig.EntityType = DirectoryObjectType.Group;
            Assert.Throws<InvalidOperationException>(() => Config.Update());
        }

        [Test]
        public void DuplicateClaimType()
        {
            var firstCTConfig = Config.ClaimTypes.FirstOrDefault(x => !String.IsNullOrEmpty(x.ClaimType));

            // Setting a duplicate claim type on a new item should fail
            ClaimTypeConfig ctConfig = new ClaimTypeConfig() { ClaimType = firstCTConfig.ClaimType, LDAPAttribute = "ldap", LDAPClass = "class" };
            Assert.Throws<InvalidOperationException>(() => Config.ClaimTypes.Add(ctConfig));

            // Setting a duplicate claim type on items already existing in the list should fail
            var anotherCTConfig = Config.ClaimTypes.FirstOrDefault(x => !String.IsNullOrEmpty(x.ClaimType) && !String.Equals(firstCTConfig.ClaimType, x.ClaimType, StringComparison.InvariantCultureIgnoreCase));
            anotherCTConfig.ClaimType = firstCTConfig.ClaimType;
            Assert.Throws<InvalidOperationException>(() => Config.Update());
        }

        [Test]
        public void DuplicatePrefixToBypassLookup()
        {
            string prefixToBypassLookup = "test:";

            // Setting a duplicate PrefixToBypassLookup on 2 items already existing in the list should fail
            Config.ClaimTypes.Where(x => !String.IsNullOrEmpty(x.ClaimType)).Take(2).Select(x => x.PrefixToBypassLookup = prefixToBypassLookup).ToList();
            Assert.Throws<InvalidOperationException>(() => Config.Update());

            // Setting a PrefixToBypassLookup on an existing item and add a new item with the same PrefixToBypassLookup should fail
            var firstCTConfig = Config.ClaimTypes.FirstOrDefault(x => !String.IsNullOrEmpty(x.ClaimType));
            firstCTConfig.PrefixToBypassLookup = prefixToBypassLookup;
            ClaimTypeConfig ctConfig = new ClaimTypeConfig() { ClaimType = UnitTestsHelper.RandomClaimType, PrefixToBypassLookup = prefixToBypassLookup, LDAPAttribute = "ldap", LDAPClass = "class" };
            Assert.Throws<InvalidOperationException>(() => Config.Update());
        }

        [Test]
        public void DuplicateEntityDataKey()
        {
            string entityDataKey = "test";

            // Setting a duplicate EntityDataKey on 2 items already existing in the list should fail
            Config.ClaimTypes.Where(x => !String.IsNullOrEmpty(x.ClaimType)).Take(2).Select(x => x.EntityDataKey = entityDataKey).ToList();
            Assert.Throws<InvalidOperationException>(() => Config.Update());

            // Setting a EntityDataKey on an existing item and add a new item with the same EntityDataKey should fail
            var firstCTConfig = Config.ClaimTypes.FirstOrDefault(x => !String.IsNullOrEmpty(x.ClaimType));
            firstCTConfig.EntityDataKey = entityDataKey;
            ClaimTypeConfig ctConfig = new ClaimTypeConfig() { ClaimType = UnitTestsHelper.RandomClaimType, EntityDataKey = entityDataKey, LDAPAttribute = UnitTestsHelper.RandomLDAPAttribute, LDAPClass = UnitTestsHelper.RandomLDAPClass };
            Assert.Throws<InvalidOperationException>(() => Config.Update());
        }

        [Test]
        public void DuplicateLDAPAttributeAndClass()
        {
            ClaimTypeConfig existingCTConfig = Config.ClaimTypes.FirstOrDefault(x => !String.IsNullOrEmpty(x.ClaimType) && x.EntityType == DirectoryObjectType.User);

            // Create a new ClaimTypeConfig with a LDAPAttribute / LDAPClass already set should fail
            ClaimTypeConfig ctConfig = new ClaimTypeConfig() { ClaimType = UnitTestsHelper.RandomClaimType, EntityType = DirectoryObjectType.User, LDAPAttribute = existingCTConfig.LDAPAttribute, LDAPClass = existingCTConfig.LDAPClass };
            Assert.Throws<InvalidOperationException>(() => Config.ClaimTypes.Add(ctConfig));

            // Should be added successfully (for next test)
            ctConfig.LDAPAttribute = UnitTestsHelper.RandomLDAPAttribute;
            ctConfig.LDAPClass = UnitTestsHelper.RandomLDAPClass;
            Assert.DoesNotThrow(() => Config.ClaimTypes.Add(ctConfig));

            // Update an existing ClaimTypeConfig with a LDAPAttribute / LDAPClass already set should fail
            ctConfig.LDAPAttribute = existingCTConfig.LDAPAttribute;
            ctConfig.LDAPClass = existingCTConfig.LDAPClass;
            Assert.Throws<InvalidOperationException>(() => Config.Update());
            Assert.IsTrue(Config.ClaimTypes.Remove(ctConfig));
        }
    }
}
