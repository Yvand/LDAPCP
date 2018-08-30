using Microsoft.SharePoint.Administration.Claims;
using Microsoft.SharePoint.WebControls;
using NUnit.Framework;
using System;
using System.Security.Claims;

namespace LDAPCP.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class PeoplePickerTests
    {
        [Test, TestCaseSource(typeof(SearchEntityDataSource), "GetTestData")]
        [MaxTime(UnitTestsHelper.MaxTime)]
        [Repeat(UnitTestsHelper.TestRepeatCount)]
        public void SearchEntities(SearchEntityData registrationData)
        {
            UnitTestsHelper.TestSearchOperation(registrationData.Input, registrationData.ExpectedResultCount, registrationData.ExpectedEntityClaimValue);
        }

        [Test, TestCaseSource(typeof(ValidateEntityDataSource), "GetTestData")]
        [MaxTime(UnitTestsHelper.MaxTime)]
        [Repeat(UnitTestsHelper.TestRepeatCount)]
        public void ValidateClaim(ValidateEntityData registrationData)
        {
            SPClaim inputClaim = new SPClaim(UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType, registrationData.ClaimValue, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));
            UnitTestsHelper.TestValidationOperation(inputClaim, registrationData.ShouldValidate, registrationData.ClaimValue);
        }

        //[TestCaseSource(typeof(SearchEntityDataSourceCollection))]
        public void DEBUG_SearchEntitiesFromCollection(string inputValue, string expectedCount, string expectedClaimValue)
        {
            UnitTestsHelper.TestSearchOperation(inputValue, Convert.ToInt32(expectedCount), expectedClaimValue);
        }

        //[TestCase(@"group\ch", 1, @"contoso.local\group\chartest")]
        //[TestCase(@"test)", 2, @"test)char@contoso.local")]
        //[TestCase(@"group\ch", 1, @"group\chartest")]
        [TestCase(@"user1", 2, @"user1@yvand.net")]
        public void DEBUG_SearchEntities(string inputValue, int expectedResultCount, string expectedEntityClaimValue)
        {
            UnitTestsHelper.TestSearchOperation(inputValue, expectedResultCount, expectedEntityClaimValue);
        }

        //[TestCase("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", @"contoso.local\group\chartest", true)]
        //[TestCase("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", @"test)char@contoso.local", true)]
        //[TestCase("http://yvand.com/customType1", @"group\chartest", true)]
        public void DEBUG_ValidateClaim(string claimType, string claimValue, bool shouldValidate)
        {
            SPClaim inputClaim = new SPClaim(claimType, claimValue, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));
            UnitTestsHelper.TestValidationOperation(inputClaim, shouldValidate, claimValue);
        }
    }
}
