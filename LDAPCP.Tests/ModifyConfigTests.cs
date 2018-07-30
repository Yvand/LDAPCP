using ldapcp;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LDAPCP.Tests
{
    [TestFixture]
    public class ModifyConfigTests
    {
        public const string ClaimsProviderConfigName = "LDAPCPConfig";
        public const string AvailableClaimType = "http://schemas.yvand.com/ws/claims/random";
        public const string AvailableLDAPAttribute = "randomAttribute";
        public const string AvailableLDAPClass = "randomClass";

        private LDAPCPConfig Config;

        [OneTimeSetUp]
        public void Init()
        {
            LDAPCPConfig configFromConfigDB = LDAPCPConfig.GetConfiguration(ClaimsProviderConfigName);
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
            ctConfig.LDAPAttribute = AvailableLDAPAttribute;
            ctConfig.LDAPClass = AvailableLDAPClass;
            Assert.Throws<InvalidOperationException>(() => Config.ClaimTypes.Add(ctConfig));

            // Adding a ClaimTypeConfig with UseMainClaimTypeOfDirectoryObject = false (default value) and LDAPAttribute / LDAPClass not set should fail
            ctConfig.ClaimType = AvailableClaimType;
            ctConfig.LDAPAttribute = String.Empty;
            ctConfig.LDAPClass = String.Empty;
            Assert.Throws<InvalidOperationException>(() => Config.ClaimTypes.Add(ctConfig));

            // Adding a ClaimTypeConfig with UseMainClaimTypeOfDirectoryObject = true and ClaimType set should fail
            ctConfig.ClaimType = AvailableClaimType;
            ctConfig.LDAPAttribute = AvailableLDAPAttribute;
            ctConfig.LDAPClass = AvailableLDAPClass;
            ctConfig.UseMainClaimTypeOfDirectoryObject = true;
            Assert.Throws<InvalidOperationException>(() => Config.ClaimTypes.Add(ctConfig));

            // Adding a valid ClaimTypeConfig with EntityType 'Group' should succeed
            ctConfig.ClaimType = AvailableClaimType;
            ctConfig.LDAPAttribute = AvailableLDAPAttribute;
            ctConfig.LDAPClass = AvailableLDAPClass;
            ctConfig.EntityType = DirectoryObjectType.Group;
            ctConfig.UseMainClaimTypeOfDirectoryObject = false;
            Config.ClaimTypes.Add(ctConfig);
            Config.ClaimTypes.Remove(AvailableClaimType);

            // Adding a valid ClaimTypeConfig should succeed
            ctConfig.ClaimType = AvailableClaimType;
            ctConfig.LDAPAttribute = AvailableLDAPAttribute;
            ctConfig.LDAPClass = AvailableLDAPClass;
            ctConfig.EntityType = DirectoryObjectType.User;
            ctConfig.UseMainClaimTypeOfDirectoryObject = false;
            Config.ClaimTypes.Add(ctConfig);
            Config.ClaimTypes.Remove(ctConfig);
        }

        [Test]
        public void DeleteIdentityClaimTypeConfig()
        {
            string identityClaimType = UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType;
            Assert.IsNotEmpty(identityClaimType);
            Assert.Throws<InvalidOperationException>(() => Config.ClaimTypes.Remove(identityClaimType));

            ClaimTypeConfig identityCTConfig = Config.ClaimTypes.FirstOrDefault(x => String.Equals(UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType, x.ClaimType, StringComparison.InvariantCultureIgnoreCase));
            Assert.IsNotNull(identityCTConfig);
            Assert.Throws<InvalidOperationException>(() => Config.ClaimTypes.Remove(identityCTConfig));
        }

        [Test]
        public void DuplicateClaimType()
        {
            var firstCTConfig = Config.ClaimTypes.FirstOrDefault(x => !String.IsNullOrEmpty(x.ClaimType));

            // Set a duplicate claim type on a new item
            ClaimTypeConfig ctConfig = new ClaimTypeConfig() { ClaimType = firstCTConfig.ClaimType, LDAPAttribute = "ldap", LDAPClass = "class" };
            Assert.Throws<InvalidOperationException>(() => Config.ClaimTypes.Add(ctConfig));

            // Set a duplicate claim type on items already existing in the list
            var anotherCTConfig = Config.ClaimTypes.FirstOrDefault(x => !String.IsNullOrEmpty(x.ClaimType) && !String.Equals(firstCTConfig.ClaimType, x.ClaimType, StringComparison.InvariantCultureIgnoreCase));
            anotherCTConfig.ClaimType = firstCTConfig.ClaimType;
            Assert.Throws<InvalidOperationException>(() => Config.Update());
        }

        [Test]
        public void DuplicatePrefixToBypassLookup()
        {
            string prefixToBypassLookup = "test:";

            // Set a duplicate PrefixToBypassLookup on 2 items already existing in the list
            Config.ClaimTypes.Where(x => !String.IsNullOrEmpty(x.ClaimType)).Take(2).Select(x => x.PrefixToBypassLookup = prefixToBypassLookup).ToList();
            Assert.Throws<InvalidOperationException>(() => Config.Update());

            // Set a PrefixToBypassLookup on an existing item and add a new item with the same PrefixToBypassLookup
            var firstCTConfig = Config.ClaimTypes.FirstOrDefault(x => !String.IsNullOrEmpty(x.ClaimType));
            firstCTConfig.PrefixToBypassLookup = prefixToBypassLookup;
            ClaimTypeConfig ctConfig = new ClaimTypeConfig() { ClaimType = AvailableClaimType, PrefixToBypassLookup = prefixToBypassLookup, LDAPAttribute = "ldap", LDAPClass = "class" };
            Assert.Throws<InvalidOperationException>(() => Config.Update());
        }

        [Test]
        public void DuplicateEntityDataKey()
        {
            string entityDataKey = "test";

            // Set a duplicate EntityDataKey on 2 items already existing in the list
            Config.ClaimTypes.Where(x => !String.IsNullOrEmpty(x.ClaimType)).Take(2).Select(x => x.EntityDataKey = entityDataKey).ToList();
            Assert.Throws<InvalidOperationException>(() => Config.Update());

            // Set a EntityDataKey on an existing item and add a new item with the same EntityDataKey
            var firstCTConfig = Config.ClaimTypes.FirstOrDefault(x => !String.IsNullOrEmpty(x.ClaimType));
            firstCTConfig.EntityDataKey = entityDataKey;
            ClaimTypeConfig ctConfig = new ClaimTypeConfig() { ClaimType = AvailableClaimType, EntityDataKey = entityDataKey, LDAPAttribute = AvailableLDAPAttribute, LDAPClass = AvailableLDAPClass };
            Assert.Throws<InvalidOperationException>(() => Config.Update());
        }

        [Test]
        public void DuplicateLDAPAttributeAndClass()
        {
            ClaimTypeConfig existingCTConfig = Config.ClaimTypes.FirstOrDefault(x => !String.IsNullOrEmpty(x.ClaimType) && x.EntityType == DirectoryObjectType.User);

            // Create a new ClaimTypeConfig with a LDAPAttribute / LDAPClass already set should fail
            ClaimTypeConfig ctConfig = new ClaimTypeConfig() { ClaimType = AvailableClaimType, EntityType = DirectoryObjectType.User, LDAPAttribute = existingCTConfig.LDAPAttribute, LDAPClass = existingCTConfig.LDAPClass };
            Assert.Throws<InvalidOperationException>(() => Config.ClaimTypes.Add(ctConfig));

            // Should be added successfully (for next test)
            ctConfig.LDAPAttribute = AvailableLDAPAttribute;
            ctConfig.LDAPClass = AvailableLDAPClass;
            Config.ClaimTypes.Add(ctConfig);
            
            // Update an existing ClaimTypeConfig with a LDAPAttribute / LDAPClass already set should fail
            ctConfig.LDAPAttribute = existingCTConfig.LDAPAttribute;
            ctConfig.LDAPClass = existingCTConfig.LDAPClass;
            Assert.Throws<InvalidOperationException>(() => Config.Update());
        }
    }
}
