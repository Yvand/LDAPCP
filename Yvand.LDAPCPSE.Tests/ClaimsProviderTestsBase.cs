using Microsoft.Office.Audit.Schema;
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
    public class ClaimsProviderTestsBase
    {
        /// <summary>
        /// Configures whether the configuration applied is valid, and whether the claims provider should be able to use it
        /// </summary>
        protected bool ConfigurationShouldBeValid = true;

        protected string UserIdentifierClaimType
        {
            get
            {
                return Settings.ClaimTypes.UserIdentifierConfig.ClaimType;
            }
        }

        protected string GroupIdentifierClaimType
        {
            get
            {
                return Settings.ClaimTypes.GroupIdentifierConfig.ClaimType;
            }
        }

        protected LdapProviderSettings Settings = new LdapProviderSettings();

        /// <summary>
        /// Initialize settings
        /// </summary>
        [OneTimeSetUp]
        protected virtual void InitializeSettings()
        {
            Settings = new LdapProviderSettings();
            Settings.ClaimTypes = LdapProviderSettings.ReturnDefaultClaimTypesConfig(UnitTestsHelper.ClaimsProvider.Name);
            string json = File.ReadAllText(UnitTestsHelper.AzureTenantsJsonFile);
            List<DirectoryConnection> azureTenants = JsonConvert.DeserializeObject<List<DirectoryConnection>>(json);
            Settings.LdapConnections = azureTenants;
            Settings.EnableAugmentation = true;

            Trace.TraceInformation($"{DateTime.Now:s} [{this.GetType().Name}] Initialized default settings.");
        }

        /// <summary>
        /// Override this method and decorate it with [Test] if the settings applied in the inherited class should be tested
        /// </summary>
        public virtual void CheckSettingsTest()
        {
            UnitTestsHelper.PersistedConfiguration.ApplySettings(Settings, false);
            if (ConfigurationShouldBeValid)
            {
                Assert.DoesNotThrow(() => UnitTestsHelper.PersistedConfiguration.ValidateConfiguration(), "ValidateLocalConfiguration should NOT throw a InvalidOperationException because the configuration is valid");
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => UnitTestsHelper.PersistedConfiguration.ValidateConfiguration(), "ValidateLocalConfiguration should throw a InvalidOperationException because the configuration is invalid");
                Trace.TraceInformation($"{DateTime.Now:s} [{this.GetType().Name}] Invalid configuration: {JsonConvert.SerializeObject(Settings, Formatting.None)}");
            }
        }

        /// <summary>
        /// Applies the <see cref="Settings"/> to the configuration object and save it in the configuration database
        /// </summary>
        protected void ApplySettings()
        {
            UnitTestsHelper.PersistedConfiguration.ApplySettings(Settings, true);
            Trace.TraceInformation($"{DateTime.Now:s} [{this.GetType().Name}] Updated configuration: {JsonConvert.SerializeObject(Settings, Formatting.None)}");
        }

        [OneTimeTearDown]
        protected void Cleanup()
        {
            Trace.TraceInformation($"{DateTime.Now:s} [{this.GetType().Name}] Cleanup.");
        }

        public void TestSearchAndValidateForTestUser(TestUser entity)
        {
            int expectedCount = 1;
            string inputValue = entity.UserPrincipalName;
            string claimValue = entity.UserPrincipalName;
            bool shouldValidate = true;

            if (Settings.AlwaysResolveUserInput)
            {
                inputValue = entity.UserPrincipalName;
                claimValue = entity.UserPrincipalName;
                expectedCount = Settings.ClaimTypes.GetConfigsMappedToClaimType().Count();
            }

            // If shouldValidate is false, user should not be found anyway so no need to do additional checks
            if (shouldValidate)
            {
                //claimValue = String.Equals(Settings.ClaimTypes.UserIdentifierConfig.DirectoryObjectAttribute, "userPrincipalName", StringComparison.OrdinalIgnoreCase) ?
                //    entity.UserPrincipalName :
                //    entity.SID;
                expectedCount = 1;
            }
            TestSearchOperation(inputValue, expectedCount, claimValue);
            TestValidationOperation(UserIdentifierClaimType, claimValue, shouldValidate);
        }

        public void TestSearchAndValidateForETestGroup(TestGroup entity)
        {
            string inputValue = entity.SamAccountName;
            string claimValue = entity.AccountNameFqdn;
            int expectedCount = 1;
            bool shouldValidate = true;

            if (Settings.AlwaysResolveUserInput)
            {
                expectedCount = Settings.ClaimTypes.GetConfigsMappedToClaimType().Count();
            }

            TestSearchOperation(inputValue, expectedCount, claimValue);
            TestValidationOperation(GroupIdentifierClaimType, claimValue, shouldValidate);
        }

        /// <summary>
        /// Gold users are the test users who are members of all the test groups
        /// </summary>
        public virtual void TestAugmentationOfGoldUsersAgainstRandomGroups()
        {
            foreach (TestUser user in TestEntitySourceManager.GetUsersMembersOfAllGroups())
            {
                TestAugmentationAgainstRandomGroups(user);
            }
        }

        public void TestAugmentationAgainstRandomGroups(TestUser user)
        {
            TestGroup randomGroup = TestEntitySourceManager.GetOneGroup();
            bool userShouldBeMember = user.IsMemberOfAllGroups || randomGroup.EveryoneIsMember ? true : false;
            Trace.TraceInformation($"{DateTime.Now:s} [{this.GetType().Name}] TestAugmentationAgainstRandomGroups for user \"{user.UserPrincipalName}\", IsMemberOfAllGroupsp: {user.IsMemberOfAllGroups} against group \"{randomGroup.SamAccountName}\" EveryoneIsMember: {randomGroup.EveryoneIsMember}. userShouldBeMember: {userShouldBeMember}");
            TestAugmentationOperation(user.UserPrincipalName, userShouldBeMember, randomGroup.AccountNameFqdn);
        }

        /// <summary>
        /// Start search operation on a specific claims provider
        /// </summary>
        /// <param name="inputValue"></param>
        /// <param name="expectedCount">How many entities are expected to be returned. Set to Int32.MaxValue if exact number is unknown but greater than 0</param>
        /// <param name="expectedClaimValue"></param>
        protected void TestSearchOperation(string inputValue, int expectedCount, string expectedClaimValue)
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
                Trace.TraceInformation($"{DateTime.Now:s} [{this.GetType().Name}] TestSearchOperation finished in {timer.ElapsedMilliseconds} ms. Parameters: inputValue: '{inputValue}', expectedCount: '{expectedCount}', expectedClaimValue: '{expectedClaimValue}'.");
            }
            catch (Exception ex)
            {
                Trace.TraceError($"{DateTime.Now:s} [{this.GetType().Name}] TestSearchOperation failed with exception '{ex.GetType()}', message '{ex.Message}'. Parameters: inputValue: '{inputValue}', expectedCount: '{expectedCount}', expectedClaimValue: '{expectedClaimValue}'.");
            }
        }

        private void VerifySearchTest(List<PickerEntity> entities, string input, int expectedCount, string expectedClaimValue)
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

        protected void TestValidationOperation(ValidateEntityScenario registrationData)
        {
            bool shouldValidate = registrationData.ShouldValidate;
            string claimType = registrationData.EntityType == ResultEntityType.User ?
                UserIdentifierClaimType :
                GroupIdentifierClaimType;

            TestValidationOperation(claimType, registrationData.ClaimValue, shouldValidate);
        }

        protected void TestValidationOperation(string claimType, string claimValue, bool shouldValidate) //(SPClaim inputClaim, bool shouldValidate, string expectedClaimValue)
        {
            SPClaim inputClaim = new SPClaim(claimType, claimValue, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));
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
                    Assert.That(entities[0].Claim.Value, Is.EqualTo(claimValue).IgnoreCase, $"Validation of entity \"{inputClaim.Value}\" should have returned value \"{claimValue}\", but it returned \"{entities[0].Claim.Value}\" instead.");
                }
                timer.Stop();
                Trace.TraceInformation($"{DateTime.Now:s} [{this.GetType().Name}] TestValidationOperation finished in {timer.ElapsedMilliseconds} ms. Parameters: inputClaim.Value: '{inputClaim.Value}', shouldValidate: '{shouldValidate}', expectedClaimValue: '{claimValue}'.");
            }
            catch (Exception ex)
            {
                Trace.TraceError($"{DateTime.Now:s} [{this.GetType().Name}] TestValidationOperation failed with exception '{ex.GetType()}', message '{ex.Message}'. Parameters: inputClaim.Value: '{inputClaim.Value}', shouldValidate: '{shouldValidate}', expectedClaimValue: '{claimValue}'.");
            }
        }

        /// <summary>
        /// Tests if the augmentation works as expected.
        /// </summary>
        /// <param name="claimValue"></param>
        /// <param name="shouldBeMemberOfTheGroupTested"></param>
        //[TestCase("FakeAccount", false)]
        //[TestCase("yvand@contoso.local", true)]
        protected void TestAugmentationOperation(string claimValue, bool shouldBeMemberOfTheGroupTested, string groupNameToTestInGroupMembership)
        {
            string claimType = UserIdentifierClaimType;
            SPClaim groupClaimToTestInGroupMembership = new SPClaim(GroupIdentifierClaimType, groupNameToTestInGroupMembership, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));
            try
            {
                Stopwatch timer = new Stopwatch();
                timer.Start();
                SPClaim inputClaim = new SPClaim(claimType, claimValue, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));
                Uri context = new Uri(UnitTestsHelper.TestSiteCollUri.AbsoluteUri);

                SPClaim[] groups = UnitTestsHelper.ClaimsProvider.GetClaimsForEntity(context, inputClaim);

                bool groupFound = false;
                if (groups != null && groups.Contains(groupClaimToTestInGroupMembership))
                {
                    groupFound = true;
                }

                if (shouldBeMemberOfTheGroupTested)
                {

                    Assert.That(groupFound, Is.True, $"Entity \"{claimValue}\" should be member of group \"{groupNameToTestInGroupMembership}\", but this group was not found in the claims returned by the claims provider.");
                }
                else
                {
                    Assert.That(groupFound, Is.False, $"Entity \"{claimValue}\" should NOT be member of group \"{groupNameToTestInGroupMembership}\", but this group was found in the claims returned by the claims provider.");
                }
                timer.Stop();
                Trace.TraceInformation($"{DateTime.Now:s} [{this.GetType().Name}] TestAugmentationOperation finished in {timer.ElapsedMilliseconds} ms. Parameters: claimType: '{claimType}', claimValue: '{claimValue}', isMemberOfTrustedGroup: '{shouldBeMemberOfTheGroupTested}'.");
            }
            catch (Exception ex)
            {
                Trace.TraceError($"{DateTime.Now:s} [{this.GetType().Name}] TestAugmentationOperation failed with exception '{ex.GetType()}', message '{ex.Message}'. Parameters: claimType: '{claimType}', claimValue: '{claimValue}', isMemberOfTrustedGroup: '{shouldBeMemberOfTheGroupTested}'.");
            }
        }
    }
}
