using Microsoft.SharePoint.Administration.Claims;
using Microsoft.SharePoint.WebControls;
using NUnit.Framework;
using System.Security.Claims;

namespace LDAPCP.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class PeoplePickerTests
    {
        [Test, TestCaseSource(typeof(SearchTestsDataSource), "GetTestData")]
        [MaxTime(UnitTestsHelper.MaxTime)]
        [Repeat(UnitTestsHelper.TestRepeatCount)]
        public void SearchEntities(SearchTestsData registrationData)
        {
            SPProviderHierarchyTree[] providerResults = UnitTestsHelper.DoSearchOperation(registrationData.Input);
            UnitTestsHelper.VerifySearchResult(providerResults, registrationData.ExpectedResultCount, registrationData.ExpectedEntityClaimValue);
        }

        [Test, TestCaseSource(typeof(ValidationTestsDataSource), "GetTestData")]
        [MaxTime(UnitTestsHelper.MaxTime)]
        [Repeat(UnitTestsHelper.TestRepeatCount)]
        public void ValidateClaim(ValidationTestsData registrationData)
        {
            SPClaim inputClaim = new SPClaim(UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType, registrationData.ClaimValue, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));
            PickerEntity[] entities = UnitTestsHelper.DoValidationOperation(inputClaim);
            UnitTestsHelper.VerifyValidationResult(entities, registrationData.ShouldValidate, registrationData.ClaimValue);
        }

        //[TestCase(@"group\ch", 1, @"contoso.local\group\chartest")]
        //[TestCase(@"group\ch", 1, @"group\chartest")]
        public void DEBUG_SearchEntities(string inputValue, int expectedResultCount, string expectedEntityClaimValue)
        {
            SPProviderHierarchyTree[] providerResults = UnitTestsHelper.DoSearchOperation(inputValue);
            UnitTestsHelper.VerifySearchResult(providerResults, expectedResultCount, expectedEntityClaimValue);
        }

        //[TestCase("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", @"contoso.local\group\chartest", true)]
        //[TestCase("http://yvand.com/customType1", @"group\chartest", true)]
        public void DEBUG_ValidateClaim(string claimType, string claimValue, bool shouldValidate)
        {
            SPClaim inputClaim = new SPClaim(claimType, claimValue, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));
            PickerEntity[] entities = UnitTestsHelper.DoValidationOperation(inputClaim);
            UnitTestsHelper.VerifyValidationResult(entities, shouldValidate, claimValue);
        }
    }
}
