using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
using System;
using System.Collections.ObjectModel;
using System.Reflection;

namespace Yvand.Config
{
    public interface IEntityProviderSettings
    {
        /// <summary>
        /// Gets the version of the settings
        /// </summary>
        long Version { get; }
        
        /// <summary>
        /// Gets the claim types and their mapping with a DirectoryObject property
        /// </summary>
        ClaimTypeConfigCollection ClaimTypes { get; }
        
        /// <summary>
        /// Gets or sets whether to skip Azure AD lookup and consider any input as valid.
        /// This can be useful to keep people picker working even if connectivity with the Azure tenant is lost.
        /// </summary>
        bool AlwaysResolveUserInput { get; }

        /// <summary>
        /// Gets or sets whether to return only results that match exactly the user input (case-insensitive).
        /// </summary>
        bool FilterExactMatchOnly { get; }

        /// <summary>
        /// Gets or sets whether to return the Azure AD groups that the user is a member of.
        /// </summary>
        bool EnableAugmentation { get; }

        /// <summary>
        /// Gets or sets a string that will appear as a prefix of the text of each result, in the people picker.
        /// </summary>
        string EntityDisplayTextPrefix { get; }

        /// <summary>
        /// Gets or sets the timeout before giving up the query to Azure AD.
        /// </summary>
        int Timeout { get; }

        /// <summary>
        /// This property is not used by AzureCP and is available to developers for their own needs
        /// </summary>
        string CustomData { get; }
    }

    public class EntityProviderSettings : IEntityProviderSettings
    {
        public long Version { get; set; }
        public ClaimTypeConfigCollection ClaimTypes { get; set; }
        public bool AlwaysResolveUserInput { get; set; } = false;
        public bool FilterExactMatchOnly { get; set; } = false;
        public bool EnableAugmentation { get; set; } = true;
        public string EntityDisplayTextPrefix { get; set; }
        public int Timeout { get; set; } = ClaimsProviderConstants.DEFAULT_TIMEOUT;
        public string CustomData { get; set; }
        public EntityProviderSettings() { }
    }

    public class EntityProviderConfig<TSettings> : SPPersistedObject
        where TSettings : IEntityProviderSettings
    {
        /// <summary>
        /// Gets the settings, based on the configuration stored in this persisted object
        /// </summary>
        public TSettings Settings {
            get
            {
                if (_Settings == null)
                {
                    _Settings = GenerateSettingsFromCurrentConfiguration();
                }
                return _Settings;
            }
        }
        private TSettings _Settings;

        #region "Settings implemented from the interface"
        protected ClaimTypeConfigCollection ClaimTypes
        {
            get
            {
                if (_ClaimTypes == null)
                {
                    _ClaimTypes = new ClaimTypeConfigCollection(ref this._ClaimTypesCollection);
                }
                return _ClaimTypes;
            }
            set
            {
                _ClaimTypes = value;
                _ClaimTypesCollection = value == null ? null : value.innerCol;
            }
        }
        [Persisted]
        private Collection<ClaimTypeConfig> _ClaimTypesCollection;
        private ClaimTypeConfigCollection _ClaimTypes;

        protected bool AlwaysResolveUserInput
        {
            get => _AlwaysResolveUserInput;
            set => _AlwaysResolveUserInput = value;
        }
        [Persisted]
        private bool _AlwaysResolveUserInput;

        protected bool FilterExactMatchOnly
        {
            get => _FilterExactMatchOnly;
            set => _FilterExactMatchOnly = value;
        }
        [Persisted]
        private bool _FilterExactMatchOnly;

        protected bool EnableAugmentation
        {
            get => _EnableAugmentation;
            set => _EnableAugmentation = value;
        }
        [Persisted]
        private bool _EnableAugmentation = true;

        protected string EntityDisplayTextPrefix
        {
            get => _EntityDisplayTextPrefix;
            set => _EntityDisplayTextPrefix = value;
        }
        [Persisted]
        private string _EntityDisplayTextPrefix;

        protected int Timeout
        {
            get
            {
#if DEBUG
                return _Timeout * 100;
#endif
                return _Timeout;
            }
            set => _Timeout = value;
        }
        [Persisted]
        private int _Timeout = ClaimsProviderConstants.DEFAULT_TIMEOUT;

        protected string CustomData
        {
            get => _CustomData;
            set => _CustomData = value;
        }
        [Persisted]
        private string _CustomData;
        #endregion

        #region "Other properties"
        /// <summary>
        /// Gets or sets the name of the claims provider using this settings
        /// </summary>
        public string ClaimsProviderName
        {
            get => _ClaimsProviderName;
            set => _ClaimsProviderName = value;
        }
        [Persisted]
        private string _ClaimsProviderName;

        [Persisted]
        private string ClaimsProviderVersion;

        private SPTrustedLoginProvider _SPTrust;
        protected SPTrustedLoginProvider SPTrust
        {
            get
            {
                if (this._SPTrust == null)
                {
                    this._SPTrust = Utils.GetSPTrustAssociatedWithClaimsProvider(this.ClaimsProviderName);
                }
                return this._SPTrust;
            }
        }
        #endregion       

        public EntityProviderConfig() { }
        public EntityProviderConfig(string persistedObjectName, SPPersistedObject parent, string claimsProviderName) : base(persistedObjectName, parent)
        {
            this.ClaimsProviderName = claimsProviderName;
            this.Initialize();
        }

        private void Initialize()
        {
            this.InitializeDefaultSettings();
        }

        public virtual bool InitializeDefaultSettings()
        {
            this.ClaimTypes = ReturnDefaultClaimTypesConfig();
            return true;
        }

        /// <summary>
        /// Returns a TSettings from the properties of the current persisted object
        /// </summary>
        /// <returns></returns>
        protected virtual TSettings GenerateSettingsFromCurrentConfiguration()
        {
            IEntityProviderSettings entityProviderSettings = new EntityProviderSettings()
            {
                AlwaysResolveUserInput = this.AlwaysResolveUserInput,
                ClaimTypes = this.ClaimTypes,
                CustomData = this.CustomData,
                EnableAugmentation = this.EnableAugmentation,
                EntityDisplayTextPrefix = this.EntityDisplayTextPrefix,
                FilterExactMatchOnly = this.FilterExactMatchOnly,
                Timeout = this.Timeout,
                Version = this.Version,
            };
            return (TSettings)entityProviderSettings;
        }

        /// <summary>
        /// If it is valid, commits the current settings to the SharePoint settings database
        /// </summary>
        public override void Update()
        {
            this.ValidateConfiguration();
            base.Update();
            Logger.Log($"Successfully updated configuration '{this.Name}' with Id {this.Id}", TraceSeverity.High, EventSeverity.Information, TraceCategory.Core);
        }

        /// <summary>
        /// If it is valid, commits the current settings to the SharePoint settings database
        /// </summary>
        /// <param name="ensure">If true, the call will not throw if the object already exists.</param>
        public override void Update(bool ensure)
        {
            this.ValidateConfiguration();
            base.Update(ensure);
            Logger.Log($"Successfully updated configuration '{this.Name}' with Id {this.Id}", TraceSeverity.High, EventSeverity.Information, TraceCategory.Core);
        }

        /// <summary>
        /// Ensures that the current configuration is valid and can be safely persisted in the configuration database
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public virtual void ValidateConfiguration()
        {
            // In case ClaimTypes collection was modified, test if it is still valid
            if (this.ClaimTypes == null)
            {
                throw new InvalidOperationException("Configuration is not valid because property ClaimTypes is null");
            }
            try
            {
                ClaimTypeConfigCollection testUpdateCollection = new ClaimTypeConfigCollection(this.SPTrust);
                foreach (ClaimTypeConfig curCTConfig in this.ClaimTypes)
                {
                    testUpdateCollection.Add(curCTConfig, false);
                }
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException("Some changes made to list ClaimTypes are invalid and cannot be committed to configuration database. Inspect inner exception for more details about the error.", ex);
            }
        }

        /// <summary>
        /// Removes the current persisted object from the SharePoint configuration database
        /// </summary>
        public override void Delete()
        {
            base.Delete();
            Logger.Log($"Successfully deleted configuration '{this.Name}' with Id {this.Id}", TraceSeverity.High, EventSeverity.Information, TraceCategory.Core);
        }

        /// <summary>
        /// Override this method to allow more users to update the object. True specifies that more users can update the object; otherwise, false. The default value is false.
        /// </summary>
        /// <returns></returns>
        protected override bool HasAdditionalUpdateAccess()
        {
            return false;
        }

        /// <summary>
        /// Applies the settings passed in parameter to the current settings
        /// </summary>
        /// <param name="settings"></param>
        public virtual void ApplySettings(TSettings settings, bool commitIfValid)
        {
            if(settings == null)
            {
                return;
            }
            if (settings.ClaimTypes == null)
            {
                this.ClaimTypes = null;
            }
            else
            {
                this.ClaimTypes = new ClaimTypeConfigCollection(this.SPTrust);
                foreach (ClaimTypeConfig claimTypeConfig in settings.ClaimTypes)
                {
                    this.ClaimTypes.Add(claimTypeConfig.CopyConfiguration(), false);
                }
            }
            this.AlwaysResolveUserInput = settings.AlwaysResolveUserInput;
            this.FilterExactMatchOnly = settings.FilterExactMatchOnly;
            this.EnableAugmentation = settings.EnableAugmentation;
            this.EntityDisplayTextPrefix = settings.EntityDisplayTextPrefix;
            this.Timeout = settings.Timeout;
            this.CustomData = settings.CustomData;

            if(commitIfValid)
            {
                this.Update();
            }
        }

        public virtual TSettings GetDefaultSettings()
        {
            IEntityProviderSettings entityProviderSettings = new EntityProviderSettings();
            return (TSettings)entityProviderSettings;
        }

        public virtual ClaimTypeConfigCollection ReturnDefaultClaimTypesConfig()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns the global configuration, stored as a persisted object in the SharePoint configuration database
        /// </summary>
        /// <param name="configurationId">The ID of the configuration</param>
        /// <param name="initializeLocalSettings">Set to true to initialize the property <see cref="Settings"/></param>
        /// <returns></returns>
        public static EntityProviderConfig<TSettings> GetGlobalConfiguration(Guid configurationId, bool initializeLocalSettings = false)
        {
            SPFarm parent = SPFarm.Local;
            try
            {
                //IEntityProviderSettings settings = (IEntityProviderSettings)parent.GetObject(configurationName, parent.Id, typeof(EntityProviderConfiguration));
                //Conf<TSettings> settings = (Conf<TSettings>)parent.GetObject(configurationName, parent.Id, T);
                //Conf<TSettings> settings = (Conf<TSettings>)parent.GetObject(configurationName, parent.Id, typeof(Conf<TSettings>));
                EntityProviderConfig<TSettings> configuration = (EntityProviderConfig<TSettings>)parent.GetObject(configurationId);
                //if (configuration != null && initializeLocalSettings == true)
                //{
                //    configuration.RefreshSettingsIfNeeded();
                //}
                return configuration;
            }
            catch (Exception ex)
            {
                Logger.LogException(String.Empty, $"while retrieving configuration ID '{configurationId}'", TraceCategory.Configuration, ex);
            }
            return null;
        }

        public static void DeleteGlobalConfiguration(Guid configurationId)
        {
            EntityProviderConfig<TSettings> configuration = (EntityProviderConfig<TSettings>)GetGlobalConfiguration(configurationId);
            if (configuration == null)
            {
                Logger.Log($"Configuration ID '{configurationId}' was not found in configuration database", TraceSeverity.Medium, EventSeverity.Error, TraceCategory.Core);
                return;
            }
            configuration.Delete();
            Logger.Log($"Configuration ID '{configurationId}' was successfully deleted from configuration database", TraceSeverity.High, EventSeverity.Information, TraceCategory.Core);
        }

        /// <summary>
        /// Creates a configuration. This will delete any existing configuration which may already exist
        /// </summary>
        /// <param name="configurationID">ID of the new configuration</param>
        /// <param name="configurationName">Name of the new configuration</param>
        /// <param name="claimsProviderName">Clais provider associated with this new configuration</param>
        /// <param name="T">Type of the new configuration</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static EntityProviderConfig<TSettings> CreateGlobalConfiguration(Guid configurationID, string configurationName, string claimsProviderName, Type T)
        {
            if (String.IsNullOrWhiteSpace(claimsProviderName))
            {
                throw new ArgumentNullException(nameof(claimsProviderName));
            }

            if(Utils.GetSPTrustAssociatedWithClaimsProvider(claimsProviderName) == null)
            {
                return null;
            }

            // Ensure it doesn't already exists and delete it if so
            EntityProviderConfig<TSettings> existingConfig = GetGlobalConfiguration(configurationID);
            if (existingConfig != null)
            {
                DeleteGlobalConfiguration(configurationID);
            }

            Logger.Log($"Creating configuration '{configurationName}' with Id {configurationID}...", TraceSeverity.VerboseEx, EventSeverity.Error, TraceCategory.Core);
            ConstructorInfo ctorWithParameters = T.GetConstructor(new[] { typeof(string), typeof(SPFarm), typeof(string) });
            EntityProviderConfig<TSettings> globalConfiguration = (EntityProviderConfig<TSettings>)ctorWithParameters.Invoke(new object[] { configurationName, SPFarm.Local, claimsProviderName });
            TSettings defaultSettings = globalConfiguration.GetDefaultSettings();
            globalConfiguration.ApplySettings(defaultSettings, false);
            globalConfiguration.Id = configurationID;
            globalConfiguration.Update(true);
            Logger.Log($"Created configuration '{configurationName}' with Id {globalConfiguration.Id}", TraceSeverity.High, EventSeverity.Information, TraceCategory.Core);
            return globalConfiguration;
        }
    }
}
