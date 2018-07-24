using Microsoft.SharePoint.Administration.Claims;
using Microsoft.SharePoint.WebControls;
using NUnit.Framework;
using System;

namespace LDAPCP.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class PeoplePickerTests
    {
        [SetUp]
        public void BeforeEachTest()
        {
            Console.WriteLine($"Before {TestContext.CurrentContext.Test.Name}");
        }

        [Test, TestCaseSource(typeof(SearchTestsDataSource), "GetTestData")]
        [MaxTime(UnitTestsHelper.MaxTime)]
        [Repeat(100)]
        public void SearchReturnsEntities(SearchTestsData registrationData)
        {
            SPClaimProviderOperationOptions mode = SPClaimProviderOperationOptions.DisableHierarchyAugmentation;
            string[] providerNames = new string[] { "AllUsers", "LDAPCP", "AzureCP", "AD" };
            string[] entityTypes = new string[] { "User", "SecGroup", "SharePointGroup", "System", "FormsRole" };

            SPProviderHierarchyTree[] providerResults = SPClaimProviderOperations.Search(UnitTestsHelper.Context, mode, providerNames, entityTypes, registrationData.Input, 30);

            UnitTestsHelper.ValidateEntities(providerResults, registrationData.ExpectedResultCount, registrationData.ExpectedEntityClaimValue);
        }

        [Test, TestCaseSource(typeof(ValidationTestsDataSource), "GetTestData")]
        [MaxTime(UnitTestsHelper.MaxTime)]
        [Repeat(100)]
        public void ValidateClaim(ValidationTestsData registrationData)
        {
            SPClaimProviderOperationOptions mode = SPClaimProviderOperationOptions.AllZones | SPClaimProviderOperationOptions.OverrideVisibleConfiguration;
            string[] providerNames = null;
            string[] entityTypes = new string[] { "User" };
            SPClaim inputClaim = new SPClaim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", registrationData.ClaimValue, "http://www.w3.org/2001/XMLSchema#string", SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));

            PickerEntity[] entities = SPClaimProviderOperations.Resolve(UnitTestsHelper.Context, mode, providerNames, entityTypes, inputClaim);
            Assert.AreEqual(entities != null && entities.Length == 1, registrationData.ShouldValidate);
            if (registrationData.ShouldValidate)
            {
                StringAssert.AreEqualIgnoringCase(registrationData.ClaimValue, entities[0].Claim.Value);
            }
        }
    }
}
