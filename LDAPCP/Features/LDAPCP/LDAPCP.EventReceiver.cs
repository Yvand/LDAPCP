using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration.Claims;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ldapcp
{
    /// <summary>
    /// This class handles events raised during feature activation, deactivation, installation, uninstallation, and upgrade.
    /// </summary>
    /// <remarks>
    /// The GUID attached to this class may be used during packaging and should not be modified.
    /// </remarks>

    [Guid("91e8e631-b3be-4d05-84c4-8653bddac278")]
    public class LDAPCPEventReceiver : SPClaimProviderFeatureReceiver
    {
        public override string ClaimProviderAssembly => typeof(LDAPCP).Assembly.FullName;
        
        public override string ClaimProviderDescription => LDAPCP._ProviderInternalName;

        public override string ClaimProviderDisplayName => LDAPCP._ProviderInternalName;

        public override string ClaimProviderType => typeof(LDAPCP).FullName;

        public override void FeatureActivated(SPFeatureReceiverProperties properties)
        {
            ExecBaseFeatureActivated(properties);
        }

        private void ExecBaseFeatureActivated(SPFeatureReceiverProperties properties)
        {
            // Wrapper function for base FeatureActivated. 
            // Used because base keywork can lead to unverifiable code inside lambda expression
            base.FeatureActivated(properties);
            SPSecurity.RunWithElevatedPrivileges(delegate()
            {
                LdapcpLogging svc = LdapcpLogging.Local;
            });
        }        

        public override void FeatureUninstalling(SPFeatureReceiverProperties properties)
        {
            SPSecurity.RunWithElevatedPrivileges(delegate()
            {
                //using (StreamWriter outfile = new StreamWriter(@"c:\temp\featurereceiver.txt", true))
                //{
                //    outfile.WriteLine(DateTime.Now.ToString() + " - FeatureUninstalling called");
                //}
                LdapcpLogging.Unregister();
            });
        }

        public override void FeatureDeactivating(SPFeatureReceiverProperties properties)
        {
            SPSecurity.RunWithElevatedPrivileges(delegate()
            {
                //using (StreamWriter outfile = new StreamWriter(@"c:\temp\featurereceiver.txt", true))
                //{
                //    outfile.WriteLine(DateTime.Now.ToString() + " - FeatureDeactivating called");
                //}
                base.RemoveClaimProvider(LDAPCP._ProviderInternalName);
                LDAPCPConfig.DeleteLDAPCPConfig(ClaimsProviderConstants.LDAPCPCONFIG_NAME);
            });
        }

        public override void FeatureUpgrading(SPFeatureReceiverProperties properties, string upgradeActionName, IDictionary<string, string> parameters)
        {
            SPSecurity.RunWithElevatedPrivileges(delegate ()
            {
                LdapcpLogging svc = LdapcpLogging.Local;
            });
        }
    }
}
