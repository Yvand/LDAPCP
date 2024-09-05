using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
using System;
using System.Runtime.InteropServices;
using Yvand.LdapClaimsProvider.Logging;

namespace CustomClaimsProvider.Features
{
    /// <summary>
    /// This class handles events raised during feature activation, deactivation, installation, uninstallation, and upgrade.
    /// </summary>
    /// <remarks>
    /// The GUID attached to this class may be used during packaging and should not be modified.
    /// </remarks>

    [Guid("8a0189bf-2e90-48b0-85f7-f639b42e3ef8")]
    public class LDAPCPSECustomEventReceiver : SPClaimProviderFeatureReceiver
    {
        public override string ClaimProviderAssembly => typeof(LDAPCPSE_Custom).Assembly.FullName;

        public override string ClaimProviderDescription => LDAPCPSE_Custom.ClaimsProviderName;

        public override string ClaimProviderDisplayName => LDAPCPSE_Custom.ClaimsProviderName;

        public override string ClaimProviderType => typeof(LDAPCPSE_Custom).FullName;

        public override void FeatureActivated(SPFeatureReceiverProperties properties)
        {
            ExecBaseFeatureActivated(properties);
        }

        private void ExecBaseFeatureActivated(Microsoft.SharePoint.SPFeatureReceiverProperties properties)
        {
            // Wrapper function for base FeatureActivated. 
            // Used because base keywork can lead to unverifiable code inside lambda expression
            base.FeatureActivated(properties);
            SPSecurity.RunWithElevatedPrivileges((SPSecurity.CodeToRunElevated)delegate ()
            {
                try
                {
                    Logger svc = Logger.Local;
                    Logger.Log($"[{LDAPCPSE_Custom.ClaimsProviderName}] Activating farm-scoped feature for claims provider \"{LDAPCPSE_Custom.ClaimsProviderName}\"", TraceSeverity.High, EventSeverity.Information, TraceCategory.Configuration);
                }
                catch (Exception ex)
                {
                    Logger.LogException((string)LDAPCPSE_Custom.ClaimsProviderName, $"activating farm-scoped feature for claims provider \"{LDAPCPSE_Custom.ClaimsProviderName}\"", TraceCategory.Configuration, ex);
                }
            });
        }

        public override void FeatureUninstalling(SPFeatureReceiverProperties properties)
        {
        }

        public override void FeatureDeactivating(SPFeatureReceiverProperties properties)
        {
            SPSecurity.RunWithElevatedPrivileges((SPSecurity.CodeToRunElevated)delegate ()
            {
                try
                {
                    Logger.Log($"[{LDAPCPSE_Custom.ClaimsProviderName}] Deactivating farm-scoped feature for claims provider \"{LDAPCPSE_Custom.ClaimsProviderName}\": Removing claims provider from the farm (but not its configuration)", TraceSeverity.High, EventSeverity.Information, TraceCategory.Configuration);
                    base.RemoveClaimProvider((string)LDAPCPSE_Custom.ClaimsProviderName);
                }
                catch (Exception ex)
                {
                    Logger.LogException((string)LDAPCPSE_Custom.ClaimsProviderName, $"deactivating farm-scoped feature for claims provider \"{LDAPCPSE_Custom.ClaimsProviderName}\"", TraceCategory.Configuration, ex);
                }
            });
        }

    }
}