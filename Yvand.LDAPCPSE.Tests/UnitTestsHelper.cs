using DataAccess;
using Microsoft.Office.Audit.Schema;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using Yvand.LdapClaimsProvider.Configuration;

namespace Yvand.LdapClaimsProvider.Tests
{
    [SetUpFixture]
    public class UnitTestsHelper
    {
        public static readonly LDAPCPSE ClaimsProvider = new LDAPCPSE(TestContext.Parameters["ClaimsProviderName"]);
        public static SPTrustedLoginProvider SPTrust => SPSecurityTokenServiceManager.Local.TrustedLoginProviders.FirstOrDefault(x => String.Equals(x.ClaimProviderName, TestContext.Parameters["ClaimsProviderName"], StringComparison.InvariantCultureIgnoreCase));
        public static Uri TestSiteCollUri;
        public static string TestSiteRelativePath => $"/sites/{TestContext.Parameters["TestSiteCollectionName"]}";
        public const int MaxTime = 50000;
        public static string FarmAdmin => TestContext.Parameters["FarmAdmin"];
        public static string DomainFqdn => TestContext.Parameters["DomainFqdn"];
        public static string Domain => TestContext.Parameters["Domain"];
#if DEBUG
        public const int TestRepeatCount = 1;
#else
    public const int TestRepeatCount = 20;
#endif

        public static string RandomClaimType => "http://schemas.yvand.net/ws/claims/random";
        public static string RandomClaimValue => "IDoNotExist";
        public static string RandomDirectoryObjectClass => "randomClass";
        public static string RandomDirectoryObjectAttribute => "randomAttribute";

        public static string ValidGroupClaimType => TestContext.Parameters["ValidGroupClaimType"];
        public static string ValidGroupName => TestContext.Parameters["ValidGroupName"];
        public const string ValidGroupSid = "S-1-5-21-2647467245-1611586658-188888215-11601"; //=> TestContext.Parameters["ValidTrustedGroupName"]; // CONTOSO\group1
        public const string ValidUserSid = "S-1-5-21-2647467245-1611586658-188888215-107206"; // => TestContext.Parameters["ValidUserSid"]; // CONTOSO\testLdapcpseUser_001

        public static string AzureTenantsJsonFile => TestContext.Parameters["LdapConnections"];
        public static string DataFile_AllAccounts_Search => TestContext.Parameters["DataFile_AllAccounts_Search"];
        public static string DataFile_AllAccounts_Validate => TestContext.Parameters["DataFile_AllAccounts_Validate"];
        public static string DataFile_TestUsers => TestContext.Parameters["DataFile_TestUsers"];
        public static string DataFile_TestGroups => TestContext.Parameters["DataFile_TestGroups"];
        static TextWriterTraceListener Logger { get; set; }
        public static LdapProviderConfiguration PersistedConfiguration;
        private static ILdapProviderSettings OriginalSettings;


        [OneTimeSetUp]
        public static void InitializeSiteCollection()
        {
            Logger = new TextWriterTraceListener(TestContext.Parameters["TestLogFileName"]);
            Trace.Listeners.Add(Logger);
            Trace.AutoFlush = true;
            Trace.TraceInformation($"{DateTime.Now.ToString("s")} [SETUP] Start integration tests of {ClaimsProvider.Name} {FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(LDAPCPSE)).Location).FileVersion}.");
            Trace.TraceInformation($"{DateTime.Now.ToString("s")} [SETUP] DataFile_AllAccounts_Search: {DataFile_AllAccounts_Search}");
            Trace.TraceInformation($"{DateTime.Now.ToString("s")} [SETUP] DataFile_AllAccounts_Validate: {DataFile_AllAccounts_Validate}");
            Trace.TraceInformation($"{DateTime.Now.ToString("s")} [SETUP] TestSiteCollectionName: {TestContext.Parameters["TestSiteCollectionName"]}");

            if (SPTrust == null)
            {
                Trace.TraceError($"{DateTime.Now.ToString("s")} [SETUP] SPTrust: is null");
            }
            else
            {
                Trace.TraceInformation($"{DateTime.Now.ToString("s")} [SETUP] SPTrust: {SPTrust.Name}");
            }

            PersistedConfiguration = LDAPCPSE.GetConfiguration(true);
            if (PersistedConfiguration != null)
            {
                OriginalSettings = PersistedConfiguration.Settings;
                Trace.TraceInformation($"{DateTime.Now:s} [SETUP] Took a backup of the original settings");
            }
            else
            {
                PersistedConfiguration = LDAPCPSE.CreateConfiguration();
                Trace.TraceInformation($"{DateTime.Now:s} [SETUP] Persisted configuration not found, created it");
            }

#if DEBUG
            TestSiteCollUri = new Uri($"http://spsites{TestSiteRelativePath}");
            return; // Uncommented when debugging from unit tests
#endif

            var service = SPFarm.Local.Services.GetValue<SPWebService>(String.Empty);
            SPWebApplication wa = service.WebApplications.FirstOrDefault(x =>
            {
                foreach (var iisSetting in x.IisSettings.Values)
                {
                    foreach (SPAuthenticationProvider authenticationProviders in iisSetting.ClaimsAuthenticationProviders)
                    {
                        if (String.Equals(authenticationProviders.ClaimProviderName, LDAPCPSE.ClaimsProviderName, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
                return false;
            });
            if (wa == null)
            {
                Trace.TraceError($"{DateTime.Now.ToString("s")} [SETUP] Web application was NOT found.");
                return;
            }

            Trace.TraceInformation($"{DateTime.Now.ToString("s")} [SETUP] Web application {wa.Name} found.");
            Uri waRootAuthority = wa.AlternateUrls[0].Uri;
            TestSiteCollUri = new Uri($"{waRootAuthority.GetLeftPart(UriPartial.Authority)}{TestSiteRelativePath}");
            SPClaimProviderManager claimMgr = SPClaimProviderManager.Local;
            string encodedGroupClaim = claimMgr.EncodeClaim(new SPClaim(ValidGroupClaimType, ValidGroupName, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name)));
            SPUserInfo groupInfo = new SPUserInfo { LoginName = encodedGroupClaim, Name = ValidGroupName };

            FileVersionInfo spAssemblyVersion = FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(SPSite)).Location);
            string spSiteTemplate = "STS#3"; // modern team site template
            if (spAssemblyVersion.FileBuildPart < 10000)
            {
                // If SharePoint 2016, must use the classic team site template
                spSiteTemplate = "STS#0";
            }

            // The root site may not exist, but it must be present for tests to run
            if (!SPSite.Exists(waRootAuthority))
            {
                Trace.TraceInformation($"{DateTime.Now.ToString("s")} [SETUP] Creating root site collection {waRootAuthority.AbsoluteUri}...");
                SPSite spSite = wa.Sites.Add(waRootAuthority.AbsoluteUri, "root", "root", 1033, spSiteTemplate, FarmAdmin, String.Empty, String.Empty);
                spSite.RootWeb.CreateDefaultAssociatedGroups(FarmAdmin, FarmAdmin, spSite.RootWeb.Title);

                SPGroup membersGroup = spSite.RootWeb.AssociatedMemberGroup;
                membersGroup.AddUser(groupInfo.LoginName, groupInfo.Email, groupInfo.Name, groupInfo.Notes);
                spSite.Dispose();
            }

            if (!SPSite.Exists(TestSiteCollUri))
            {
                Trace.TraceInformation($"{DateTime.Now.ToString("s")} [SETUP] Creating site collection {TestSiteCollUri.AbsoluteUri} with template '{spSiteTemplate}'...");
                SPSite spSite = wa.Sites.Add(TestSiteCollUri.AbsoluteUri, LDAPCPSE.ClaimsProviderName, LDAPCPSE.ClaimsProviderName, 1033, spSiteTemplate, FarmAdmin, String.Empty, String.Empty);
                spSite.RootWeb.CreateDefaultAssociatedGroups(FarmAdmin, FarmAdmin, spSite.RootWeb.Title);

                SPGroup membersGroup = spSite.RootWeb.AssociatedMemberGroup;
                membersGroup.AddUser(groupInfo.LoginName, groupInfo.Email, groupInfo.Name, groupInfo.Notes);
                spSite.Dispose();
            }
            else
            {
                using (SPSite spSite = new SPSite(TestSiteCollUri.AbsoluteUri))
                {
                    SPGroup membersGroup = spSite.RootWeb.AssociatedMemberGroup;
                    membersGroup.AddUser(groupInfo.LoginName, groupInfo.Email, groupInfo.Name, groupInfo.Notes);
                }
            }
        }

        [OneTimeTearDown]
        public static void Cleanup()
        {
            Trace.TraceInformation($"{DateTime.Now:s} [SETUP] Cleanup.");
            try
            {
                if (PersistedConfiguration != null && OriginalSettings != null)
                {
                    PersistedConfiguration.ApplySettings(OriginalSettings, true);
                    Trace.TraceInformation($"{DateTime.Now:s} [SETUP] Restored original settings of LDAPCPSE configuration");
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError($"{DateTime.Now:s} [SETUP] Unexpected error while restoring the original settings of LDAPCPSE configuration: {ex.Message}");
            }

            Trace.TraceInformation($"{DateTime.Now.ToString("s")} [SETUP] Integration tests of {LDAPCPSE.ClaimsProviderName} {FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(LDAPCPSE)).Location).FileVersion} finished.");
            Trace.Flush();
            if (Logger != null)
            {
                Logger.Dispose();
            }
        }
    }

    public enum ResultEntityType
    {
        None,
        Mixed,
        User,
        Group,
    }

    public abstract class TestEntity : ICloneable
    {
        public object Clone()
        {
            return this.MemberwiseClone();
        }

        public abstract void SetEntityFromDataSourceRow(Row row);
    }

    public class TestUser : TestEntity
    {
        public string UserPrincipalName;
        public string SamAccountName;
        public string Mail;
        public string GivenName;
        public string DisplayName;
        public string DistinguishedName;
        public string SID;
        public bool IsMemberOfAllGroups;

        public override void SetEntityFromDataSourceRow(Row row)
        {
            UserPrincipalName = row["userPrincipalName"];
            SamAccountName = row["SamAccountName"];
            Mail = row["mail"];
            GivenName = row["givenName"];
            DisplayName = row["displayName"];
            DistinguishedName = row["DistinguishedName"];
            SID = row["SID"];
            IsMemberOfAllGroups = Convert.ToBoolean(row["IsMemberOfAllGroups"]);
        }
    }

    public class TestGroup : TestEntity
    {
        public string SamAccountName;
        public string DistinguishedName;
        public string SID;
        public bool EveryoneIsMember;
        public string AccountNameFqdn;

        public override void SetEntityFromDataSourceRow(Row row)
        {
            SamAccountName = row["SamAccountName"];
            DistinguishedName = row["DistinguishedName"];
            SID = row["SID"];
            EveryoneIsMember = Convert.ToBoolean(row["EveryoneIsMember"]);
            AccountNameFqdn = row["AccountNameFqdn"];
        }
    }

    public class SearchEntityScenario : TestEntity
    {
        public string Input;
        public int SearchResultCount;
        public string SearchResultSingleEntityClaimValue;
        public ResultEntityType SearchResultEntityTypes;
        public bool ExactMatch;

        public override void SetEntityFromDataSourceRow(Row row)
        {
            Input = row["Input"];
            SearchResultCount = Convert.ToInt32(row["SearchResultCount"]);
            SearchResultSingleEntityClaimValue = row["SearchResultSingleEntityClaimValue"];
            SearchResultEntityTypes = (ResultEntityType)Enum.Parse(typeof(ResultEntityType), row["SearchResultEntityTypes"]);
            ExactMatch = Convert.ToBoolean(row["ExactMatch"]);
        }
    }

    public class ValidateEntityScenario : TestEntity
    {
        public string ClaimValue;
        public bool ShouldValidate;
        public bool IsMemberOfTrustedGroup;
        public ResultEntityType EntityType;

        public override void SetEntityFromDataSourceRow(Row row)
        {
            ClaimValue = row["ClaimValue"];
            ShouldValidate = Convert.ToBoolean(row["ShouldValidate"]);
            IsMemberOfTrustedGroup = Convert.ToBoolean(row["IsMemberOfTrustedGroup"]);
            EntityType = (ResultEntityType)Enum.Parse(typeof(ResultEntityType), row["EntityType"]);
        }
    }

    public class TestEntitySource<T> where T : TestEntity, new()
    {
        private object _LockInitEntitiesList = new object();
        private List<T> _Entities;
        public List<T> Entities
        {
            get
            {
                if (_Entities != null) { return _Entities; }
                lock (_LockInitEntitiesList)
                {
                    if (_Entities != null) { return _Entities; }
                    _Entities = new List<T>();
                    foreach (T entity in ReadDataSource())
                    {
                        _Entities.Add(entity);
                    }
                    Trace.TraceInformation($"{DateTime.Now:s} [{typeof(T).Name}] Initialized List of {nameof(Entities)} with {Entities.Count} items.");
                    return _Entities;
                }
            }
        }

        private Random RandomNumber = new Random();
        private string DataSourceFilePath;

        public TestEntitySource(string dataSourceFilePath)
        {
            DataSourceFilePath = dataSourceFilePath;
        }

        private IEnumerable<T> ReadDataSource()
        {
            DataTable dt = DataTable.New.ReadCsv(DataSourceFilePath);
            foreach (Row row in dt.Rows)
            {
                T entity = new T();
                entity.SetEntityFromDataSourceRow(row);
                yield return entity;
            }
        }

        public IEnumerable<T> GetSomeEntities(int count, Func<T, bool> filter = null)
        {
            if (count > Entities.Count) { count = Entities.Count; }
            IEnumerable<T> entitiesFiltered = Entities.Where(filter ?? (x => true));
            int randomNumberMaxValue = entitiesFiltered.Count() - 1;
            int randomIdx = RandomNumber.Next(0, randomNumberMaxValue);
            yield return entitiesFiltered.ElementAt(randomIdx).Clone() as T;
        }
    }

    public class TestEntitySourceManager
    {
        private static TestEntitySource<SearchEntityScenario> SearchTestsSource = new TestEntitySource<SearchEntityScenario>(UnitTestsHelper.DataFile_AllAccounts_Search);
        public static List<SearchEntityScenario> AllSearchEntities
        {
            get => SearchTestsSource.Entities;
        }
        private static TestEntitySource<ValidateEntityScenario> ValidationTestsSource = new TestEntitySource<ValidateEntityScenario>(UnitTestsHelper.DataFile_AllAccounts_Validate);
        public static List<ValidateEntityScenario> AllValidationEntities
        {
            get => ValidationTestsSource.Entities;
        }
        private static TestEntitySource<TestUser> TestUsersSource = new TestEntitySource<TestUser>(UnitTestsHelper.DataFile_TestUsers);
        public static List<TestUser> AllTestUsers
        {
            get => TestUsersSource.Entities;
        }
        private static TestEntitySource<TestGroup> TestGroupsSource = new TestEntitySource<TestGroup>(UnitTestsHelper.DataFile_TestGroups);
        public static List<TestGroup> AllTestGroups
        {
            get => TestGroupsSource.Entities;
        }
        public const int MaxNumberOfUsersToTest = 100;
        public const int MaxNumberOfGroupsToTest = 100;

        public static IEnumerable<TestUser> GetSomeUsers(int count)
        {
            return TestUsersSource.GetSomeEntities(count, null);
        }

        public static IEnumerable<TestUser> GetUsersMembersOfAllGroups()
        {
            Func<TestUser, bool> onlyMembersOfAllGroupsFilter = x => x.IsMemberOfAllGroups == true;
            return TestUsersSource.GetSomeEntities(Int16.MaxValue, onlyMembersOfAllGroupsFilter);
        }

        public static TestUser FindUser(string upnPrefix)
        {
            return TestUsersSource.Entities.First(x => x.UserPrincipalName.StartsWith(upnPrefix)).Clone() as TestUser;
        }

        public static IEnumerable<TestGroup> GetSomeGroups(int count)
        {
            return TestGroupsSource.GetSomeEntities(count);
        }

        public static TestGroup GetOneGroup()
        {
            return TestGroupsSource.GetSomeEntities(1, null).First();
        }

        //public static TestGroup GetOneGroup(bool securityEnabledOnly)
        //{
        //    Func<TestGroup, bool> securityEnabledOnlyFilter = x => x.SecurityEnabled == securityEnabledOnly;
        //    return TestGroupsSource.GetSomeEntities(1, securityEnabledOnlyFilter).First();
        //}
    }
}