using NUnit.Framework;

namespace LDAPCP.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class AugmentationTests
    {
        [TestCase("i:05.t|contoso.local|yvand@contoso.local", true)]
        [TestCase("i:05.t|contoso.local|zzzyvand@contoso.local", false)]
        public void AugmentEntity_Debug(string claimValue, bool shouldHavePermissions)
        {
            UnitTestsHelper.DoAugmentationOperationAndVerifyResult(UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType, claimValue, shouldHavePermissions);
        }

        [Test, TestCaseSource(typeof(ValidationTestsDataSource), "GetTestData")]
        [MaxTime(UnitTestsHelper.MaxTime)]
        [Repeat(UnitTestsHelper.TestRepeatCount)]
        public void AugmentEntity(ValidationTestsData registrationData)
        {
            UnitTestsHelper.DoAugmentationOperationAndVerifyResult(UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType, registrationData.ClaimValue, registrationData.IsMemberOfTrustedGroup);
        }
    }
}
