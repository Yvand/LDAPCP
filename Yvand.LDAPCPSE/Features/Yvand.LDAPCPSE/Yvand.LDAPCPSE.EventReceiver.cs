using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
using System;
using System.Runtime.InteropServices;
using Yvand.LdapClaimsProvider.Logging;

namespace Yvand.LdapClaimsProvider
{
    /// <summary>
    /// This class handles events raised during feature activation, deactivation, installation, uninstallation, and upgrade.
    /// </summary>
    /// <remarks>
    /// The GUID attached to this class may be used during packaging and should not be modified.
    /// </remarks>
    [Guid("91e8e631-b3be-4d05-84c4-8653bddac278")]
    public class LDAPCPSEEventReceiver : SPClaimProviderFeatureReceiver
    {
        public override string ClaimProviderAssembly => typeof(LDAPCPSE).Assembly.FullName;

        public override string ClaimProviderDescription => LDAPCPSE.ClaimsProviderName;

        public override string ClaimProviderDisplayName => LDAPCPSE.ClaimsProviderName;

        public override string ClaimProviderType => typeof(LDAPCPSE).FullName;

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
                    Logger svc = Logger.Local;
                    Logger.Log($"[{LDAPCPSE.ClaimsProviderName}] Activating farm-scoped feature for claims provider \"{LDAPCPSE.ClaimsProviderName}\"", TraceSeverity.High, EventSeverity.Information, TraceCategory.Configuration);
                    //LDAPCPConfig existingConfig = LDAPCPConfig.GetConfiguration(ClaimsProviderConstants.CONFIG_NAME);
                    //if (existingConfig == null)
                    //{
                    //    LDAPCPConfig.CreateDefaultConfiguration();
                    //}
                    //else
                    //{
                    //    Logger.Log($"[{LDAPCPSE.ClaimsProviderName}] Use configuration \"{existingConfig.Name}\" found in the configuration database", TraceSeverity.High, EventSeverity.Information, TraceCategory.Configuration);
                    //}
                }
                catch (Exception ex)
                {
                    Logger.LogException((string)LDAPCPSE.ClaimsProviderName, $"activating farm-scoped feature for claims provider \"{LDAPCPSE.ClaimsProviderName}\"", TraceCategory.Configuration, ex);
                }
            });
        }

        public override void FeatureUninstalling(SPFeatureReceiverProperties properties)
        {
            SPSecurity.RunWithElevatedPrivileges(delegate ()
            {
                try
                {
                    Logger.Log($"[{LDAPCPSE.ClaimsProviderName}] Uninstalling farm-scoped feature for claims provider \"{LDAPCPSE.ClaimsProviderName}\": Deleting configuration from the farm", TraceSeverity.High, EventSeverity.Information, TraceCategory.Configuration);
                    //LDAPCPConfig.DeleteConfiguration(ClaimsProviderConstants.CONFIG_NAME);
                    Logger.Unregister();
                }
                catch (Exception ex)
                {
                    Logger.LogException(LDAPCPSE.ClaimsProviderName, $"uninstalling farm-scoped feature for claims provider \"{LDAPCPSE.ClaimsProviderName}\"", TraceCategory.Configuration, ex);
                }
            });
        }

        public override void FeatureDeactivating(SPFeatureReceiverProperties properties)
        {
            SPSecurity.RunWithElevatedPrivileges(delegate ()
            {
                try
                {
                    Logger.Log($"[{LDAPCPSE.ClaimsProviderName}] Deactivating farm-scoped feature for claims provider \"{LDAPCPSE.ClaimsProviderName}\": Removing claims provider from the farm (but not its configuration)", TraceSeverity.High, EventSeverity.Information, TraceCategory.Configuration);
                    base.RemoveClaimProvider(LDAPCPSE.ClaimsProviderName);
                }
                catch (Exception ex)
                {
                    Logger.LogException(LDAPCPSE.ClaimsProviderName, $"deactivating farm-scoped feature for claims provider \"{LDAPCPSE.ClaimsProviderName}\"", TraceCategory.Configuration, ex);
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
