using System;
using System.Collections;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
using Microsoft.SharePoint.WebControls;
using NUnit.Framework;

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

        [Test, TestCaseSource(typeof(PeoplePickerCSVSource), "GetTestCases")]
        //[MaxTime(MaxTime)]
        [Repeat(1)]
        public void RegisterUserTest(PeoplePickerCSVData registrationData)
        {
            SPClaimProviderOperationOptions mode = SPClaimProviderOperationOptions.DisableHierarchyAugmentation;
            string[] providerNames = new string[] { "AllUsers", "LDAPCP", "AzureCP", "AD" };
            string[] entityTypes = new string[] { "User", "SecGroup", "SharePointGroup", "System", "FormsRole" };

            SPProviderHierarchyTree[] providerResults = SPClaimProviderOperations.Search(UnitTestsHelper.Context, mode, providerNames, entityTypes, registrationData.Input, 30);

            UnitTestsHelper.ValidateEntities(providerResults, Convert.ToInt32(registrationData.ExpectedResultCount), registrationData.ExpectedEntityClaimValue);
        }
    }    
}
