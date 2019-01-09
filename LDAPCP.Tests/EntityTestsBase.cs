using ldapcp;
using Microsoft.SharePoint.Administration.Claims;
using NUnit.Framework;
using System;
using System.Security.Claims;

namespace LDAPCP.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class EntityTestsBase : ModifyConfigBase
    {
        /// <summary>
        /// Configure whether to run entity search tests.
        /// </summary>
        public virtual bool TestSearch => true;

        /// <summary>
        /// Configure whether to run entity validation tests.
        /// </summary>
        public virtual bool TestValidation => true;

        /// <summary>
        /// Configure whether to run entity augmentation tests. By default, augmentation is disabled on LDAPCP.
        /// </summary>
        public virtual bool TestAugmentation => false;

        public override void InitializeConfiguration()
        {
            base.InitializeConfiguration();
            Config.EnableAugmentation = true;
        }

        [Test, TestCaseSource(typeof(SearchEntityDataSource), "GetTestData")]
        [MaxTime(UnitTestsHelper.MaxTime)]
        [Repeat(UnitTestsHelper.TestRepeatCount)]
        public virtual void SearchEntities(SearchEntityData registrationData)
        {
            if (!TestSearch) return;

            UnitTestsHelper.TestSearchOperation(registrationData.Input, registrationData.ExpectedResultCount, registrationData.ExpectedEntityClaimValue);
        }

        [Test, TestCaseSource(typeof(ValidateEntityDataSource), "GetTestData")]
        [MaxTime(UnitTestsHelper.MaxTime)]
        [Repeat(UnitTestsHelper.TestRepeatCount)]
        public virtual void ValidateClaim(ValidateEntityData registrationData)
        {
            if (!TestValidation) return;

            SPClaim inputClaim = new SPClaim(UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType, registrationData.ClaimValue, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));
            UnitTestsHelper.TestValidationOperation(inputClaim, registrationData.ShouldValidate, registrationData.ClaimValue);
        }

        [Test, TestCaseSource(typeof(ValidateEntityDataSource), "GetTestData")]
        [MaxTime(UnitTestsHelper.MaxTime)]
        [Repeat(UnitTestsHelper.TestRepeatCount)]
        public virtual void AugmentEntity(ValidateEntityData registrationData)
        {
            if (!TestAugmentation) return;

            UnitTestsHelper.TestAugmentationOperation(UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType, registrationData.ClaimValue, registrationData.IsMemberOfTrustedGroup);
        }

#if DEBUG
        //[TestCaseSource(typeof(SearchEntityDataSourceCollection))]
        public virtual void DEBUG_SearchEntitiesFromCollection(string inputValue, string expectedCount, string expectedClaimValue)
        {
            UnitTestsHelper.TestSearchOperation(inputValue, Convert.ToInt32(expectedCount), expectedClaimValue);
        }

        //[TestCase(@"group\ch", 1, @"contoso.local\group\chartest")]
        //[TestCase(@"test)", 2, @"test)char@contoso.local")]
        //[TestCase(@"group\ch", 1, @"group\chartest")]
        [TestCase(@"user1", 2, @"user1@yvand.net")]
        public virtual void DEBUG_SearchEntities(string inputValue, int expectedResultCount, string expectedEntityClaimValue)
        {
            if (!TestSearch) return;

            LDAPConnection coco = new LDAPConnection();
            coco.AugmentationEnabled = true;
            coco.GetGroupMembershipAsADDomainProp = false;
            coco.UserServerDirectoryEntry = false;
            coco.Path = "LDAP://test";
            coco.Username = "userTest";
            Config.LDAPConnectionsProp.Add(coco);
            Config.Update();

            UnitTestsHelper.TestSearchOperation(inputValue, expectedResultCount, expectedEntityClaimValue);
        }

        //[TestCase("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", @"contoso.local\group\chartest", true)]
        //[TestCase("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", @"test)char@contoso.local", true)]
        //[TestCase("http://yvand.com/customType1", @"group\chartest", true)]
        [TestCase("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", @"yvan", false)]
        public virtual void DEBUG_ValidateClaim(string claimType, string claimValue, bool shouldValidate)
        {
            if (!TestValidation) return;

            SPClaim inputClaim = new SPClaim(claimType, claimValue, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));
            UnitTestsHelper.TestValidationOperation(inputClaim, shouldValidate, claimValue);
        }

        [TestCase("FakeAccount", false)]
        [TestCase("yvand@contoso.local", true)]
        public virtual void DEBUG_AugmentEntity(string claimValue, bool shouldHavePermissions)
        {
            if (!TestAugmentation) return;

            UnitTestsHelper.TestAugmentationOperation(UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType, claimValue, shouldHavePermissions);
        }
#endif
    }
}
