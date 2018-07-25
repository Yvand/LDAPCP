using ldapcp;
using NUnit.Framework;
using System;
using System.Linq;

namespace LDAPCP.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class ModifyConfigTests
    {
        public const string ClaimsProviderConfigName = "LDAPCPConfig";
        public const string NonExistingClaimType = "http://schemas.yvand.com/ws/claims/random";

        public static LDAPCPConfig ReturnDefaultConfig()
        {
            LDAPCPConfig configFromConfigDB = LDAPCPConfig.GetConfiguration(ClaimsProviderConfigName);
            // Create a local copy, otherwise changes will impact the whole process (even without calling Update method)
            LDAPCPConfig localConfig = configFromConfigDB.CopyPersistedProperties();
            // Reset configuration to test its default for the tests
            localConfig.ResetCurrentConfiguration();
            return localConfig;
        }

        [Test]
        public void AddClaimTypeConfig()
        {
            LDAPCPConfig config = ReturnDefaultConfig();

            ClaimTypeConfig ctConfig = new ClaimTypeConfig();

            // Identity claim type already exists and a claim type cannot be added twice
            ctConfig.ClaimType = UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType;
            Assert.Throws<InvalidOperationException>(() => config.ClaimTypes.Add(ctConfig));

            // Properties LDAPAttribute and LDAPClass should be set
            ctConfig.ClaimType = NonExistingClaimType;
            Assert.Throws<InvalidOperationException>(() => config.ClaimTypes.Add(ctConfig));

            // Property ClaimType should be empty if UseMainClaimTypeOfDirectoryObject is true
            ctConfig.UseMainClaimTypeOfDirectoryObject = true;
            Assert.Throws<InvalidOperationException>(() => config.ClaimTypes.Add(ctConfig));

            // AzureCP allows only 1 claim type for EntityType 'Group'
            ctConfig.EntityType = DirectoryObjectType.Group;
            ctConfig.UseMainClaimTypeOfDirectoryObject = false;
            Assert.Throws<InvalidOperationException>(() => config.ClaimTypes.Add(ctConfig));

            // Valid ClaimTypeConfig
            ctConfig.UseMainClaimTypeOfDirectoryObject = false;
            ctConfig.EntityType = DirectoryObjectType.User;
            ctConfig.LDAPAttribute = "LDAPAttributeValue";
            ctConfig.LDAPClass = "LDAPClassValue";
            config.ClaimTypes.Add(ctConfig);
        }

        [Test]
        public void DeleteIdentityClaimTypeConfig()
        {
            LDAPCPConfig config = ReturnDefaultConfig();

            string identityClaimType = UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType;
            Assert.IsNotEmpty(identityClaimType);
            Assert.Throws<InvalidOperationException>(() => config.ClaimTypes.Remove(identityClaimType));

            ClaimTypeConfig identityCTConfig = config.ClaimTypes.FirstOrDefault(x => String.Equals(UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType, x.ClaimType, StringComparison.InvariantCultureIgnoreCase));
            Assert.IsNotNull(identityCTConfig);
            Assert.Throws<InvalidOperationException>(() => config.ClaimTypes.Remove(identityCTConfig));
        }
    }
}
