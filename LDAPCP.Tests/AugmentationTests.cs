using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration.Claims;
using NUnit.Framework;
using System;
using System.Security.Claims;

namespace LDAPCP.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class AugmentationTests
    {
        private static SPBasePermissions GroupPermissionsToTest = SPBasePermissions.EditListItems;

        [TestCase("i:05.t|contoso.local|yvand@contoso.local")]
        [MaxTime(UnitTestsHelper.MaxTime)]
        public void AugmentEntity_Debug(string entity)
        {
            ValidationTestsData registrationData = new ValidationTestsData() { ClaimValue = entity, IsMemberOfTrustedGroup = true, ShouldValidate = true };
            AugmentEntity(registrationData);
        }

        [Test, TestCaseSource(typeof(ValidationTestsDataSource), "GetTestData")]
        [MaxTime(UnitTestsHelper.MaxTime)]
        [Repeat(UnitTestsHelper.TestRepeatCount)]
        public void AugmentEntity(ValidationTestsData registrationData)
        {
            if (!registrationData.IsMemberOfTrustedGroup) return;

            SPClaim inputClaim = new SPClaim(UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType, registrationData.ClaimValue, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));

            using (SPSite site = new SPSite(UnitTestsHelper.Context.AbsoluteUri))
            {
                // SPSite.RootWeb should not be disposed: https://blogs.msdn.microsoft.com/rogerla/2008/10/04/updated-spsite-rootweb-dispose-guidance/
                SPWeb rootWeb = site.RootWeb;
                bool entityHasPerm = rootWeb.DoesUserHavePermissions(inputClaim.ToEncodedString(), GroupPermissionsToTest);
                Assert.IsTrue(entityHasPerm);
            }
        }
    }
}
