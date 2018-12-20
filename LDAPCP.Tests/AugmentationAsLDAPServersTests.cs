using ldapcp;
using NUnit.Framework;

namespace LDAPCP.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class AugmentationAsLDAPServersTests : EntityTestsBase
    {
        public override bool TestSearch => false;
        public override bool TestValidation => false;
        public override bool TestAugmentation => true;

        public override void InitializeConfiguration()
        {
            base.InitializeConfiguration();
            Config.EnableAugmentation = true;
            Config.MainGroupClaimType = ClaimsProviderConstants.DefaultMainGroupClaimType;
            foreach (LDAPConnection ldapConn in Config.LDAPConnectionsProp)
            {
                ldapConn.AugmentationEnabled = true;
                ldapConn.GetGroupMembershipAsADDomainProp = false;
                ldapConn.GroupMembershipAttributes = new string[] { "memberOf", "uniquememberof" };
            }
            Config.Update();
        }

        [TestCase("yvand@contoso.local", true)]
        [TestCase("zzzyvand@contoso.local", false)]
        public override void DEBUG_AugmentEntity(string claimValue, bool isMemberOfTrustedGroup)
        {
            //LDAPConnection coco = new LDAPConnection();
            //coco.AugmentationEnabled = true;
            //coco.GetGroupMembershipAsADDomainProp = false;
            //coco.UserServerDirectoryEntry = false;
            //coco.Path = "LDAP://test";
            //coco.Username = "userTest";
            //Config.LDAPConnectionsProp.Add(coco);
            //Config.Update();
            UnitTestsHelper.TestAugmentationOperation(UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType, claimValue, isMemberOfTrustedGroup);
        }
    }
}
