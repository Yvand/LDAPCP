using ldapcp;
using Newtonsoft.Json;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;

namespace LDAPCP.Tests
{
    [TestFixture]
    public class AugmentWithCustomLDAPConnectionsAsADDomainTests : EntityTestsBase
    {
        public override bool TestSearch => false;
        public override bool TestValidation => false;
        public override bool TestAugmentation => true;

        public override void InitializeConfiguration()
        {
            base.InitializeConfiguration();
            Config.EnableAugmentation = true;
            Config.MainGroupClaimType = ClaimsProviderConstants.DefaultMainGroupClaimType;

            string json = File.ReadAllText(UnitTestsHelper.CustomLDAPConnections);
            List<LDAPConnection> ldapConnections = JsonConvert.DeserializeObject<List<LDAPConnection>>(json);
            Config.LDAPConnectionsProp = ldapConnections;
            foreach (LDAPConnection coco in Config.LDAPConnectionsProp)
            {
                coco.UserServerDirectoryEntry = false;
                coco.AugmentationEnabled = true;
                coco.GetGroupMembershipAsADDomain = true;
            }
            Config.Update();
        }
    }

    public class AugmentWithCustomLDAPConnectionsAsLDAPServerTests : EntityTestsBase
    {
        public override bool TestSearch => false;
        public override bool TestValidation => false;
        public override bool TestAugmentation => true;

        public override void InitializeConfiguration()
        {
            base.InitializeConfiguration();
            Config.EnableAugmentation = true;
            Config.MainGroupClaimType = ClaimsProviderConstants.DefaultMainGroupClaimType;

            string json = File.ReadAllText(UnitTestsHelper.CustomLDAPConnections);
            List<LDAPConnection> ldapConnections = JsonConvert.DeserializeObject<List<LDAPConnection>>(json);
            Config.LDAPConnectionsProp = ldapConnections;
            foreach (LDAPConnection coco in Config.LDAPConnectionsProp)
            {
                coco.UserServerDirectoryEntry = false;
                coco.AugmentationEnabled = true;
                coco.GetGroupMembershipAsADDomain = false;
            }
            Config.Update();
        }
    }

    [TestFixture]
    public class AugmentatAsADDomainOnBaseConfigTests : EntityTestsBase
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
                ldapConn.GetGroupMembershipAsADDomain = true;
            }
            Config.Update();
        }
    }

    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class AugmentAsADDomaOnCustomConfigTests : CustomConfigTests
    {
        public override void InitializeConfiguration()
        {
            base.InitializeConfiguration();
            Config.EnableAugmentation = true;
            Config.MainGroupClaimType = ClaimsProviderConstants.DefaultMainGroupClaimType;
            foreach (LDAPConnection ldapConn in Config.LDAPConnectionsProp)
            {
                ldapConn.AugmentationEnabled = true;
                ldapConn.GetGroupMembershipAsADDomain = true;
            }
            Config.Update();
        }

        [Test, TestCaseSource(typeof(ValidateEntityDataSource), "GetTestData")]
        [Repeat(UnitTestsHelper.TestRepeatCount)]
        public void TestAugmentation(ValidateEntityData registrationData)
        {
            UnitTestsHelper.TestAugmentationOperation(UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType, registrationData.ClaimValue, registrationData.IsMemberOfTrustedGroup);
        }
    }

    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class AugmentAsLDAPServersOnBaseConfigTests : EntityTestsBase
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
                ldapConn.GetGroupMembershipAsADDomain = false;
            }
            Config.Update();
        }

#if DEBUG
        [TestCase("yvand@contoso.local", true)]
        [TestCase("zzzyvand@contoso.local", false)]
        public override void DEBUG_AugmentEntity(string claimValue, bool isMemberOfTrustedGroup)
        {
            //LDAPConnection coco = new LDAPConnection();
            //coco.AugmentationEnabled = true;
            //coco.GetGroupMembershipAsADDomain = false;
            //coco.UserServerDirectoryEntry = false;
            //coco.Path = "LDAP://test";
            //coco.Username = "userTest";
            //Config.LDAPConnectionsProp.Add(coco);
            //Config.Update();
            UnitTestsHelper.TestAugmentationOperation(UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType, claimValue, isMemberOfTrustedGroup);
        }
#endif
    }

    [TestFixture]
    public class AugmentAsLDAPServersOnCustomConfigTests : CustomConfigTests
    {
        public override void InitializeConfiguration()
        {
            base.InitializeConfiguration();
            Config.EnableAugmentation = true;
            Config.MainGroupClaimType = ClaimsProviderConstants.DefaultMainGroupClaimType;
            foreach (LDAPConnection ldapConn in Config.LDAPConnectionsProp)
            {
                ldapConn.AugmentationEnabled = true;
                ldapConn.GetGroupMembershipAsADDomain = false;
                ldapConn.GroupMembershipAttributes = new string[] { "memberOf", "uniquememberof" };
            }
            Config.Update();
        }

        [Test, TestCaseSource(typeof(ValidateEntityDataSource), "GetTestData")]
        [Repeat(UnitTestsHelper.TestRepeatCount)]
        public void TestAugmentation(ValidateEntityData registrationData)
        {
            UnitTestsHelper.TestAugmentationOperation(UnitTestsHelper.SPTrust.IdentityClaimTypeInformation.MappedClaimType, registrationData.ClaimValue, registrationData.IsMemberOfTrustedGroup);
        }
    }
}
