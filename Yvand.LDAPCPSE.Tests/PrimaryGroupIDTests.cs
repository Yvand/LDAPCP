using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Yvand.LdapClaimsProvider.Configuration;
using WIF4_5 = System.Security.Claims;

namespace Yvand.LdapClaimsProvider.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    internal class PrimaryGroupIDTests : EntityTestsBase
    {
        public override void InitializeSettings(bool applyChanges)
        {
            base.InitializeSettings(false);

            // Extra initialization for current test class
            ClaimTypeConfig ctConfigPgidAttribute = new ClaimTypeConfig
            {
                ClaimType = TestContext.Parameters["MultiPurposeCustomClaimType"], // WIF4_5.ClaimTypes.PrimaryGroupSid
                ClaimTypeDisplayName = "primaryGroupID",
                DirectoryObjectType = DirectoryObjectType.User,
                SPEntityType = ClaimsProviderConstants.GroupClaimEntityType,
                DirectoryObjectClass = "user",
                DirectoryObjectAttribute = "primaryGroupID",
                DirectoryObjectAttributeSupportsWildcard = false,
            };
            Settings.ClaimTypes.Add(ctConfigPgidAttribute);
            if (applyChanges)
            {
                TestSettingsAndApplyThemIfValid();
            }
        }

        [TestCase(@"513", 1, @"513")]
        [TestCase(@"51", 0, @"")]
        [TestCase(@"5133", 0, @"")]
        public override void SearchEntities(string inputValue, int expectedResultCount, string expectedEntityClaimValue)
        {
            base.SearchEntities(inputValue, expectedResultCount, expectedEntityClaimValue);
        }
    }
}
