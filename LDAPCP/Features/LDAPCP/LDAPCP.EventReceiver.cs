using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;
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
            SPSecurity.RunWithElevatedPrivileges(delegate ()
            {
                try
                {
                    ClaimsProviderLogging.Log($"Activating farm-scoped feature for {LDAPCP._ProviderInternalName}", TraceSeverity.High, EventSeverity.Information, ClaimsProviderLogging.TraceCategory.Configuration);
                    ClaimsProviderLogging svc = ClaimsProviderLogging.Local;

                    var spTrust = LDAPCP.GetSPTrustAssociatedWithCP(LDAPCP._ProviderInternalName);
                    if (spTrust != null)
                    {
                        LDAPCPConfig config = LDAPCPConfig.CreateConfiguration(ClaimsProviderConstants.LDAPCPCONFIG_ID, ClaimsProviderConstants.LDAPCPCONFIG_NAME, spTrust.Name);
                    }
                }
                catch (Exception ex)
                {
                    ClaimsProviderLogging.LogException(LDAPCP._ProviderInternalName, $"activating farm-scoped feature for {LDAPCP._ProviderInternalName}", ClaimsProviderLogging.TraceCategory.Configuration, ex);
                }
            });
        }

        public override void FeatureUninstalling(SPFeatureReceiverProperties properties)
        {
            SPSecurity.RunWithElevatedPrivileges(delegate ()
            {
                ClaimsProviderLogging.Unregister();
            });
        }

        public override void FeatureDeactivating(SPFeatureReceiverProperties properties)
        {
            SPSecurity.RunWithElevatedPrivileges(delegate ()
            {
                try
                {
                    ClaimsProviderLogging.Log($"Deactivating farm-scoped feature for {LDAPCP._ProviderInternalName}: Removing claims provider and its configuration from the farm", TraceSeverity.High, EventSeverity.Information, ClaimsProviderLogging.TraceCategory.Configuration);
                    base.RemoveClaimProvider(LDAPCP._ProviderInternalName);
                    LDAPCPConfig.DeleteConfiguration(ClaimsProviderConstants.LDAPCPCONFIG_NAME);
                }
                catch (Exception ex)
                {
                    ClaimsProviderLogging.LogException(LDAPCP._ProviderInternalName, $"deactivating farm-scoped feature for {LDAPCP._ProviderInternalName}", ClaimsProviderLogging.TraceCategory.Configuration, ex);
                }
            });
        }

        /// <summary>
        /// Upgrade must be explicitely triggered as documented in https://www.sharepointnutsandbolts.com/2010/06/feature-upgrade-part-1-fundamentals.html
        /// In PowerShell: 
        /// $feature = [Microsoft.SharePoint.Administration.SPWebService]::AdministrationService.Features["d1817470-ca9f-4b0c-83c5-ea61f9b0660d"]
        /// $feature.Upgrade($false)
        /// Since it's not automatic, this mechanism won't be used at all
        /// </summary>
        /// <param name="properties"></param>
        /// <param name="upgradeActionName"></param>
        /// <param name="parameters"></param>
        //public override void FeatureUpgrading(SPFeatureReceiverProperties properties, string upgradeActionName, IDictionary<string, string> parameters)
        //{
        //}
    }
}
