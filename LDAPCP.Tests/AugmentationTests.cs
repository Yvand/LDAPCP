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
        [TestCase("yvand@contoso.local", true)]
        [TestCase("IDoNotExist", false)]
        [MaxTime(UnitTestsHelper.MaxTime)]
        [Repeat(2)]
        public void ValidateClaim(string claimValue, bool shouldValidate)
        {
            SPClaimProviderOperationOptions mode = SPClaimProviderOperationOptions.AllZones | SPClaimProviderOperationOptions.OverrideVisibleConfiguration;
            string[] providerNames = null;
            string[] entityTypes = new string[] { "User" };
            SPClaim inputClaim = new SPClaim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", claimValue, "http://www.w3.org/2001/XMLSchema#string", SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));

            PickerEntity[] entities = SPClaimProviderOperations.Resolve(UnitTestsHelper.Context, mode, providerNames, entityTypes, inputClaim);
            Assert.AreEqual(entities != null && entities.Length == 1, shouldValidate);
            if (shouldValidate)
            {
                StringAssert.AreEqualIgnoringCase(claimValue, entities[0].Claim.Value);
            }
            //Assert.Inconclusive();
        }

        [TestCase("i:05.t|contoso.local|yvand@contoso.local")]
        [MaxTime(UnitTestsHelper.MaxTime)]
        public void AugmentEntity(string entity)
        {
            SPBasePermissions perms = SPBasePermissions.EditListItems;

            using (SPSite site = new SPSite(UnitTestsHelper.Context.AbsoluteUri))
            {
                // SPSite.RootWeb should not be disposed: https://blogs.msdn.microsoft.com/rogerla/2008/10/04/updated-spsite-rootweb-dispose-guidance/
                SPWeb rootWeb = site.RootWeb;
                bool entityHasPerm = rootWeb.DoesUserHavePermissions(entity, perms);
                Assert.IsTrue(entityHasPerm);
            }
        }
    }
}
