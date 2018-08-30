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
                    ClaimsProviderLogging svc = ClaimsProviderLogging.Local;
                    ClaimsProviderLogging.Log($"[{LDAPCP._ProviderInternalName}] Activating farm-scoped feature for claims provider \"{LDAPCP._ProviderInternalName}\"", TraceSeverity.High, EventSeverity.Information, ClaimsProviderLogging.TraceCategory.Configuration);

                    var spTrust = LDAPCP.GetSPTrustAssociatedWithCP(LDAPCP._ProviderInternalName);
                    if (spTrust != null)
                    {
                        LDAPCPConfig existingConfig = LDAPCPConfig.GetConfiguration(ClaimsProviderConstants.CONFIG_NAME);
                        if (existingConfig == null)
                            LDAPCPConfig.CreateConfiguration(ClaimsProviderConstants.CONFIG_ID, ClaimsProviderConstants.CONFIG_NAME, spTrust.Name);
                        else
                            ClaimsProviderLogging.Log($"[{LDAPCP._ProviderInternalName}] Use configuration \"{ClaimsProviderConstants.CONFIG_NAME}\" found in the configuration database", TraceSeverity.High, EventSeverity.Information, ClaimsProviderLogging.TraceCategory.Configuration);
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
                try
                {
                    ClaimsProviderLogging.Log($"[{LDAPCP._ProviderInternalName}] Uninstalling farm-scoped feature for claims provider \"{LDAPCP._ProviderInternalName}\": Deleting configuration from the farm", TraceSeverity.High, EventSeverity.Information, ClaimsProviderLogging.TraceCategory.Configuration);
                    LDAPCPConfig.DeleteConfiguration(ClaimsProviderConstants.CONFIG_NAME);
                    ClaimsProviderLogging.Unregister();
                }
                catch (Exception ex)
                {
                    ClaimsProviderLogging.LogException(LDAPCP._ProviderInternalName, $"deactivating farm-scoped feature for claims provider \"{LDAPCP._ProviderInternalName}\"", ClaimsProviderLogging.TraceCategory.Configuration, ex);
                }
            });
        }

        public override void FeatureDeactivating(SPFeatureReceiverProperties properties)
        {
            SPSecurity.RunWithElevatedPrivileges(delegate ()
            {
                try
                {
                    ClaimsProviderLogging.Log($"[{LDAPCP._ProviderInternalName}] Deactivating farm-scoped feature for claims provider \"{LDAPCP._ProviderInternalName}\": Removing claims provider from the farm (but not its configuration)", TraceSeverity.High, EventSeverity.Information, ClaimsProviderLogging.TraceCategory.Configuration);
                    base.RemoveClaimProvider(LDAPCP._ProviderInternalName);
                }
                catch (Exception ex)
                {
                    ClaimsProviderLogging.LogException(LDAPCP._ProviderInternalName, $"deactivating farm-scoped feature for claims provider \"{LDAPCP._ProviderInternalName}\"", ClaimsProviderLogging.TraceCategory.Configuration, ex);
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
