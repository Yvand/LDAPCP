using Microsoft.SharePoint.Administration.Claims;
using Microsoft.SharePoint.WebControls;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Yvand.LdapClaimsProvider.Configuration;

namespace Yvand.LdapClaimsProvider.Tests
{
    public class EntityTestsBase
    {
        /// <summary>
        /// Configures whether to run entity search tests.
        /// </summary>
        public virtual bool TestSearch => true;

        /// <summary>
        /// Configures whether to run entity validation tests.
        /// </summary>
        public virtual bool TestValidation => true;

        /// <summary>
        /// Configures whether to run entity augmentation tests.
        /// </summary>
        public virtual bool TestAugmentation => true;

        /// <summary>
        /// Configures whether to exclude AAD Guest users from search and validation. This does not impact augmentation.
        /// </summary>
        public virtual bool ExcludeGuestUsers => false;

        /// <summary>
        /// Configures whether to exclude AAD Member users from search and validation. This does not impact augmentation.
        /// </summary>
        public virtual bool ExcludeMemberUsers => false;

        /// <summary>
        /// Configures whether the configuration applied is valid, and whether the claims provider should be able to use it
        /// </summary>
        public virtual bool ConfigurationIsValid => true;

        protected LdapProviderConfiguration GlobalConfiguration;
        protected LdapProviderSettings Settings = new LdapProviderSettings();
        private static ILdapProviderSettings OriginalSettings;

        [OneTimeSetUp]
        public void Init()
        {
            GlobalConfiguration = LDAPCPSE.GetConfiguration(true);
            if (GlobalConfiguration == null)
            {
                GlobalConfiguration = LDAPCPSE.CreateConfiguration();
            }
            else
            {
                OriginalSettings = GlobalConfiguration.Settings;
                Settings = (LdapProviderSettings)GlobalConfiguration.Settings;
                Trace.TraceInformation($"{DateTime.Now:s} Took a backup of the original settings");
            }
            InitializeConfiguration(true);
        }

        /// <summary>
        /// Initialize configuration
        /// </summary>
        public virtual void InitializeConfiguration(bool applyChanges)
        {
            Settings = new LdapProviderSettings();
            Settings.ClaimTypes = LdapProviderSettings.ReturnDefaultClaimTypesConfig(UnitTestsHelper.ClaimsProvider.Name);

#if DEBUG
            Settings.Timeout = 99999;
#endif

            string json = File.ReadAllText(UnitTestsHelper.AzureTenantsJsonFile);
            List<LdapConnection> azureTenants = JsonConvert.DeserializeObject<List<LdapConnection>>(json);
            Settings.LdapConnections = azureTenants;            

            if (applyChanges)
            {
                GlobalConfiguration.ApplySettings(Settings, true);
                Trace.TraceInformation($"{DateTime.Now:s} [EntityTestsBase] Updated configuration: {JsonConvert.SerializeObject(Settings, Formatting.None)}");
            }
        }

        [OneTimeTearDown]
        public void Cleanup()
        {
            try
            {
                if (OriginalSettings != null)
                {
                    GlobalConfiguration.ApplySettings(OriginalSettings, true);
                    Trace.TraceInformation($"{DateTime.Now:s} Restored original settings of LDAPCPSE configuration");
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError($"{DateTime.Now:s} Unexpected error while restoring the original settings of LDAPCPSE configuration: {ex.Message}");
            }
        }

        public virtual void SearchEntities(SearchEntityData registrationData)
        {
            if (!TestSearch)
            {
                return;
            }

            // If current entry does not return only users AND either guests or members are excluded, ExpectedResultCount cannot be determined so test cannot run
            if (registrationData.SearchResultEntityTypes != ResultEntityType.User &&
                (ExcludeGuestUsers || ExcludeMemberUsers))
            {
                return;
            }

            int expectedResultCount = registrationData.SearchResultCount;
            if (Settings.FilterExactMatchOnly == true)
            {
                expectedResultCount = registrationData.ExactMatch ? 1 : 0;
            }

            TestSearchOperation(registrationData.Input, expectedResultCount, registrationData.SearchResultSingleEntityClaimValue);
        }

        public virtual void SearchEntities(string inputValue, int expectedResultCount, string expectedEntityClaimValue)
        {
            if (!TestSearch) { return; }

            TestSearchOperation(inputValue, expectedResultCount, expectedEntityClaimValue);
        }

        public virtual void ValidateClaim(ValidateEntityData registrationData)
        {
            if (!TestValidation) { return; }

            bool shouldValidate = registrationData.ShouldValidate;
            string claimType = registrationData.EntityType == ResultEntityType.User ?
                UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType :
                UnitTestsHelper.TrustedGroupToAdd_ClaimType;

            SPClaim inputClaim = new SPClaim(claimType, registrationData.ClaimValue, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));
            TestValidationOperation(inputClaim, shouldValidate, registrationData.ClaimValue);
        }

        public virtual void ValidateClaim(string claimType, string claimValue, bool shouldValidate)
        {
            if (!TestValidation) { return; }

            SPClaim inputClaim = new SPClaim(claimType, claimValue, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));
            TestValidationOperation(inputClaim, shouldValidate, claimValue);
        }

        public virtual void AugmentEntity(ValidateEntityData registrationData)
        {
            if (!TestAugmentation) { return; }

            TestAugmentationOperation(UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType, registrationData.ClaimValue, registrationData.IsMemberOfTrustedGroup);
        }

        public virtual void AugmentEntity(string claimValue, bool shouldHavePermissions)
        {
            if (!TestAugmentation) { return; }

            TestAugmentationOperation(UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType, claimValue, shouldHavePermissions);
        }

        [Test]
        public virtual void ValidateInitialization()
        {
            if (ConfigurationIsValid)
            {
                Assert.That(UnitTestsHelper.ClaimsProvider.ValidateSettings(null), Is.True, "ValidateLocalConfiguration should return true because the configuration is valid");
            }
            else
            {
                Assert.That(UnitTestsHelper.ClaimsProvider.ValidateSettings(null), Is.False, "ValidateLocalConfiguration should return false because the configuration is not valid");
            }
        }

        /// <summary>
        /// Start search operation on a specific claims provider
        /// </summary>
        /// <param name="inputValue"></param>
        /// <param name="expectedCount">How many entities are expected to be returned. Set to Int32.MaxValue if exact number is unknown but greater than 0</param>
        /// <param name="expectedClaimValue"></param>
        public static void TestSearchOperation(string inputValue, int expectedCount, string expectedClaimValue)
        {
            try
            {
                Stopwatch timer = new Stopwatch();
                timer.Start();
                var entityTypes = new[] { "User", "SecGroup", "SharePointGroup", "System", "FormsRole" };

                SPProviderHierarchyTree providerResults = UnitTestsHelper.ClaimsProvider.Search(UnitTestsHelper.TestSiteCollUri, entityTypes, inputValue, null, 30);
                List<PickerEntity> entities = new List<PickerEntity>();
                foreach (var children in providerResults.Children)
                {
                    entities.AddRange(children.EntityData);
                }
                VerifySearchTest(entities, inputValue, expectedCount, expectedClaimValue);

                entities = UnitTestsHelper.ClaimsProvider.Resolve(UnitTestsHelper.TestSiteCollUri, entityTypes, inputValue).ToList();
                VerifySearchTest(entities, inputValue, expectedCount, expectedClaimValue);
                timer.Stop();
                Trace.TraceInformation($"{DateTime.Now:s} TestSearchOperation finished in {timer.ElapsedMilliseconds} ms. Parameters: inputValue: '{inputValue}', expectedCount: '{expectedCount}', expectedClaimValue: '{expectedClaimValue}'.");
            }
            catch (Exception ex)
            {
                Trace.TraceError($"{DateTime.Now:s} TestSearchOperation failed with exception '{ex.GetType()}', message '{ex.Message}'. Parameters: inputValue: '{inputValue}', expectedCount: '{expectedCount}', expectedClaimValue: '{expectedClaimValue}'.");
            }
        }

        public static void VerifySearchTest(List<PickerEntity> entities, string input, int expectedCount, string expectedClaimValue)
        {
            bool entityValueFound = false;
            StringBuilder detailedLog = new StringBuilder($"It returned {entities.Count} entities: ");
            string entityLogPattern = "entity \"{0}\", claim type: \"{1}\"; ";
            foreach (PickerEntity entity in entities)
            {
                detailedLog.AppendLine(String.Format(entityLogPattern, entity.Claim.Value, entity.Claim.ClaimType));
                if (String.Equals(expectedClaimValue, entity.Claim.Value, StringComparison.InvariantCultureIgnoreCase))
                {
                    entityValueFound = true;
                }
            }

            if (!String.IsNullOrWhiteSpace(expectedClaimValue) && !entityValueFound && expectedCount > 0)
            {
                Assert.Fail($"Input \"{input}\" returned no entity with claim value \"{expectedClaimValue}\". {detailedLog}");
            }

            if (expectedCount == Int32.MaxValue)
            {
                expectedCount = entities.Count;
            }

            Assert.That(entities.Count, Is.EqualTo(expectedCount), $"Input \"{input}\" should have returned {expectedCount} entities, but it returned {entities.Count} instead. {detailedLog}");
        }

        public static void TestValidationOperation(SPClaim inputClaim, bool shouldValidate, string expectedClaimValue)
        {
            try
            {
                Stopwatch timer = new Stopwatch();
                timer.Start();
                var entityTypes = new[] { "User" };

                PickerEntity[] entities = UnitTestsHelper.ClaimsProvider.Resolve(UnitTestsHelper.TestSiteCollUri, entityTypes, inputClaim);

                int expectedCount = shouldValidate ? 1 : 0;
                Assert.That(entities.Length, Is.EqualTo(expectedCount), $"Validation of entity \"{inputClaim.Value}\" should have returned {expectedCount} entity, but it returned {entities.Length} instead.");
                if (shouldValidate)
                {
                    Assert.That(entities[0].Claim.Value, Is.EqualTo(expectedClaimValue).IgnoreCase, $"Validation of entity \"{inputClaim.Value}\" should have returned value \"{expectedClaimValue}\", but it returned \"{entities[0].Claim.Value}\" instead.");
                }
                timer.Stop();
                Trace.TraceInformation($"{DateTime.Now:s} TestValidationOperation finished in {timer.ElapsedMilliseconds} ms. Parameters: inputClaim.Value: '{inputClaim.Value}', shouldValidate: '{shouldValidate}', expectedClaimValue: '{expectedClaimValue}'.");
            }
            catch (Exception ex)
            {
                Trace.TraceError($"{DateTime.Now:s} TestValidationOperation failed with exception '{ex.GetType()}', message '{ex.Message}'. Parameters: inputClaim.Value: '{inputClaim.Value}', shouldValidate: '{shouldValidate}', expectedClaimValue: '{expectedClaimValue}'.");
            }
        }

        public static void TestAugmentationOperation(string claimType, string claimValue, bool isMemberOfTrustedGroup)
        {
            try
            {
                Stopwatch timer = new Stopwatch();
                timer.Start();
                SPClaim inputClaim = new SPClaim(claimType, claimValue, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));
                Uri context = new Uri(UnitTestsHelper.TestSiteCollUri.AbsoluteUri);

                SPClaim[] groups = UnitTestsHelper.ClaimsProvider.GetClaimsForEntity(context, inputClaim);

                bool groupFound = false;
                if (groups != null && groups.Contains(UnitTestsHelper.TrustedGroup))
                {
                    groupFound = true;
                }

                if (isMemberOfTrustedGroup)
                {
                    Assert.That(groupFound, Is.True, $"Entity \"{claimValue}\" should be member of group \"{UnitTestsHelper.TrustedGroupToAdd_ClaimValue}\", but this group was not found in the claims returned by the claims provider.");
                }
                else
                {
                    Assert.That(groupFound, Is.False, $"Entity \"{claimValue}\" should NOT be member of group \"{UnitTestsHelper.TrustedGroupToAdd_ClaimValue}\", but this group was found in the claims returned by the claims provider.");
                }
                timer.Stop();
                Trace.TraceInformation($"{DateTime.Now:s} TestAugmentationOperation finished in {timer.ElapsedMilliseconds} ms. Parameters: claimType: '{claimType}', claimValue: '{claimValue}', isMemberOfTrustedGroup: '{isMemberOfTrustedGroup}'.");
            }
            catch (Exception ex)
            {
                Trace.TraceError($"{DateTime.Now:s} TestAugmentationOperation failed with exception '{ex.GetType()}', message '{ex.Message}'. Parameters: claimType: '{claimType}', claimValue: '{claimValue}', isMemberOfTrustedGroup: '{isMemberOfTrustedGroup}'.");
            }
        }
    }
}
