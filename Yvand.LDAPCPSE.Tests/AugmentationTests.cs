using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yvand.LdapClaimsProvider.Tests
{
    public class AugmentUsingLdapQueryTestss : ClaimsProviderTestsBase
    {
        public override void InitializeSettings()
        {
            base.InitializeSettings();
            Settings.LdapConnections[0].GetGroupMembershipUsingDotNetHelpers = false;
            base.ApplySettings();
        }

        [Test]
        public override void CheckSettingsTest()
        {
            base.CheckSettingsTest();
        }
    }
}
