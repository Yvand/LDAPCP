using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration.Claims;
using Microsoft.SharePoint.WebControls;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LDAPCP.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class AugmentationTests
    {
        [TestCase("i:05.t|contoso.local|yvand@contoso.local")]
        [MaxTime(UnitTestsHelper.MaxTime)]
        public void AugmentEntity_Debug(string entity)
        {
            ValidationTestsData registrationData = new ValidationTestsData() { ClaimValue = entity, IsMemberOfTrustedGroup = true, ShouldValidate = true };
            AugmentEntity(registrationData);
        }

        [Test, TestCaseSource(typeof(ValidationTestsDataSource), "GetTestData")]
        [MaxTime(UnitTestsHelper.MaxTime)]
        [Repeat(100)]
        public void AugmentEntity(ValidationTestsData registrationData)
        {
            if (!registrationData.IsMemberOfTrustedGroup) return;

            SPClaim inputClaim = new SPClaim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", registrationData.ClaimValue, "http://www.w3.org/2001/XMLSchema#string", SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));

            // If user is member of the trusted group, he should also have permissions granted to that group
            SPBasePermissions perms = SPBasePermissions.EditListItems;

            using (SPSite site = new SPSite(UnitTestsHelper.Context.AbsoluteUri))
            {
                // SPSite.RootWeb should not be disposed: https://blogs.msdn.microsoft.com/rogerla/2008/10/04/updated-spsite-rootweb-dispose-guidance/
                SPWeb rootWeb = site.RootWeb;
                bool entityHasPerm = rootWeb.DoesUserHavePermissions(inputClaim.ToEncodedString(), perms);
                Assert.IsTrue(entityHasPerm);
            }
        }
    }
}
