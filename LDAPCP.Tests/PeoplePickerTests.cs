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
        public void SearchReturnsEntities(SearchTestsData registrationData)
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
    }
}
