using ldapcp;
using NUnit.Framework;

namespace LDAPCP.Tests
{
    /// <summary>
    /// This class creates a backup of current configuration and provides one that can be modified as needed. At the end of the test, initial configuration will be restored.
    /// </summary>
    public class ModifyConfigBase
    {
        protected LDAPCPConfig Config;
        private LDAPCPConfig BackupConfig;

        [OneTimeSetUp]
        public void Init()
        {
            Config = LDAPCPConfig.GetConfiguration(UnitTestsHelper.ClaimsProviderConfigName, UnitTestsHelper.SPTrust.Name);
            BackupConfig = Config.CopyPersistedProperties();
            InitializeConfiguration();
        }

        /// <summary>
        /// Initialize configuration
        /// </summary>
        public virtual void InitializeConfiguration()
        {
            UnitTestsHelper.InitializeConfiguration(Config);
        }

        [OneTimeTearDown]
        public void Cleanup()
        {
            Config.ApplyConfiguration(BackupConfig);
            Config.Update();
        }
    }
}
