using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SharePoint.Administration.Claims;
using Microsoft.SharePoint;

namespace ldapcp
{
    /// <summary>
    /// This class handles events raised during feature activation, deactivation, installation, uninstallation, and upgrade.
    /// </summary>
    /// <remarks>
    /// The GUID attached to this class may be used during packaging and should not be modified.
    /// </remarks>

    [Guid("a2d43c17-4f01-4768-a190-8481023c5189")]
    public class LDAPClaimProviderFeatureReceiver : SPClaimProviderFeatureReceiver
    {
        public override string ClaimProviderAssembly
        {
            get { return typeof(LDAPClaimProvider).Assembly.FullName; }
        }

        public override string ClaimProviderDescription
        {
            get { return LDAPClaimProvider._ProviderDisplayName; }
        }

        public override string ClaimProviderDisplayName
        {
            get { return LDAPClaimProvider._ProviderDisplayName; }
        }

        public override string ClaimProviderType
        {
            get { return typeof(LDAPClaimProvider).FullName; }
        }

        private void ExecBaseFeatureActivated(Microsoft.SharePoint.SPFeatureReceiverProperties properties)
        {
            // Wrapper function for base FeatureActivated. 
            // Used because base keywork can lead to unverifiable code inside lambda expression
            base.FeatureActivated(properties);
        }

        private void RemovePersistedObject()
        {
            var PersistedObject = LDAPClaimProviderConfig.GetFromConfigDB;
            if (PersistedObject != null)
                PersistedObject.Delete();
        }

        public override void FeatureActivated(SPFeatureReceiverProperties properties)
        {
            ExecBaseFeatureActivated(properties);
        }

        public override void FeatureUninstalling(SPFeatureReceiverProperties properties)
        {
            SPSecurity.RunWithElevatedPrivileges(delegate()
            {
                this.RemovePersistedObject();
            });
        }

        public override void FeatureDeactivating(SPFeatureReceiverProperties properties)
        {
            SPSecurity.RunWithElevatedPrivileges(delegate()
            {
                base.RemoveClaimProvider(LDAPClaimProvider._ProviderInternalName);
                //var trust = LDAPClaimProvider.GetSPTrustAssociatedWithCP(LDAPClaimProvider._ProviderInternalName);
                //if (trust != null)
                //{
                //    trust.ClaimProviderName = null;
                //    trust.Update();
                //}
                this.RemovePersistedObject();
            });
        }

        public override void FeatureInstalled(SPFeatureReceiverProperties properties)
        {
            SPSecurity.RunWithElevatedPrivileges(delegate()
            {
                this.RemovePersistedObject();
            });
        }

        public override void FeatureUpgrading(SPFeatureReceiverProperties properties, string upgradeActionName, IDictionary<string, string> parameters)
        {
            SPSecurity.RunWithElevatedPrivileges(delegate()
            {
                this.RemovePersistedObject();
            });
        }
    }
}