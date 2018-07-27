using DataAccess;
using ldapcp;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
using Microsoft.SharePoint.WebControls;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

[SetUpFixture]
public class UnitTestsHelper
{
    public const string ClaimsProviderName = "LDAPCP";
    public static Uri Context = new Uri("http://spsites/sites/LDAPCP.UnitTests");
    public const int MaxTime = 50000;
    public const int TestRepeatCount = 50;
    public const string FarmAdmin = @"i:0#.w|contoso\yvand";
    public const string NonExistentClaimValue = "IDoNotExist";

    public const string TrustedGroupToAdd_ClaimValue = @"contoso.local\group1";
    public const string TrustedGroupToAdd_ClaimType = ClaimsProviderConstants.DefaultMainGroupClaimType;
    private static SPBasePermissions TrustedGroupToAdd_PermissionsAssigned = SPBasePermissions.EditListItems;

    public const string DataFile_SearchTests = @"F:\Data\Dev\LDAPCP_SearchTests_Data.csv";
    public const string DataFile_ValidationTests = @"F:\Data\Dev\LDAPCP_ValidationTests_Data.csv";

    public static SPTrustedLoginProvider SPTrust
    {
        get => SPSecurityTokenServiceManager.Local.TrustedLoginProviders.FirstOrDefault(x => String.Equals(x.ClaimProviderName, UnitTestsHelper.ClaimsProviderName, StringComparison.InvariantCultureIgnoreCase));
    }

    [OneTimeSetUp]
    public static void InitSiteCollection()
    {
        return; // Uncommented when debugging LDAPCP code from unit tests
        SPWebApplication wa = SPWebApplication.Lookup(Context);
        if (wa != null)
        {
            Console.WriteLine($"Web app {wa.Name} found.");
            SPClaimProviderManager claimMgr = SPClaimProviderManager.Local;
            SPClaim claim = new SPClaim(TrustedGroupToAdd_ClaimType, TrustedGroupToAdd_ClaimValue, "http://www.w3.org/2001/XMLSchema#string", SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, SPTrust.Name));
            string encodedClaim = claimMgr.EncodeClaim(claim);
            SPUserInfo userInfo = new SPUserInfo { LoginName = encodedClaim, Name = TrustedGroupToAdd_ClaimValue };

            if (!SPSite.Exists(Context))
            {
                Console.WriteLine($"Creating site collection {Context.AbsoluteUri}...");
                SPSite spSite = wa.Sites.Add(Context.AbsoluteUri, ClaimsProviderName, ClaimsProviderName, 1033, "STS#0", FarmAdmin, String.Empty, String.Empty);
                spSite.RootWeb.CreateDefaultAssociatedGroups(FarmAdmin, FarmAdmin, spSite.RootWeb.Title);

                SPGroup membersGroup = spSite.RootWeb.AssociatedMemberGroup;
                membersGroup.AddUser(userInfo.LoginName, userInfo.Email, userInfo.Name, userInfo.Notes);
                spSite.Dispose();
            }
            else
            {
                using (SPSite spSite = new SPSite(Context.AbsoluteUri))
                {
                    SPGroup membersGroup = spSite.RootWeb.AssociatedMemberGroup;
                    membersGroup.AddUser(userInfo.LoginName, userInfo.Email, userInfo.Name, userInfo.Notes);
                }
            }
        }
        else
        {
            Console.WriteLine($"Web app {Context} was NOT found.");
        }
    }

    public static SPProviderHierarchyTree[] DoSearchOperation(string inputValue)
    {
        SPClaimProviderOperationOptions mode = SPClaimProviderOperationOptions.DisableHierarchyAugmentation;
        string[] providerNames = new string[] { "AllUsers", "LDAPCP", "AzureCP", "AD" };
        string[] entityTypes = new string[] { "User", "SecGroup", "SharePointGroup", "System", "FormsRole" };

        SPProviderHierarchyTree[] providerResults = SPClaimProviderOperations.Search(UnitTestsHelper.Context, mode, providerNames, entityTypes, inputValue, 30);
        return providerResults;
    }

    public static PickerEntity[] DoValidationOperation(SPClaim inputClaim)
    {
        SPClaimProviderOperationOptions mode = SPClaimProviderOperationOptions.AllZones | SPClaimProviderOperationOptions.OverrideVisibleConfiguration;
        string[] providerNames = null;
        string[] entityTypes = new string[] { "User" };

        PickerEntity[] entities = SPClaimProviderOperations.Resolve(UnitTestsHelper.Context, mode, providerNames, entityTypes, inputClaim);
        return entities;
    }

    public static void DoAugmentationOperationAndVerifyResult(string claimType, string claimValue, bool shouldHavePermissions)
    {
        SPClaim inputClaim = new SPClaim(claimType, claimValue, ClaimValueTypes.String, SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, UnitTestsHelper.SPTrust.Name));

        using (SPSite site = new SPSite(UnitTestsHelper.Context.AbsoluteUri))
        {
            // SPSite.RootWeb should not be disposed: https://blogs.msdn.microsoft.com/rogerla/2008/10/04/updated-spsite-rootweb-dispose-guidance/
            SPWeb rootWeb = site.RootWeb;
            bool entityHasPerms = rootWeb.DoesUserHavePermissions(inputClaim.ToEncodedString(), TrustedGroupToAdd_PermissionsAssigned);
            if (shouldHavePermissions) Assert.IsTrue(entityHasPerms);
        }
    }

    public static void VerifySearchResult(SPProviderHierarchyTree[] providerResults, int expectedCount, string expectedClaimValue)
    {
        foreach (SPProviderHierarchyTree providerResult in providerResults)
        {
            if (providerResult.ProviderName == UnitTestsHelper.ClaimsProviderName)
            {
                Assert.AreEqual(expectedCount, providerResult.Count);

                if (expectedCount == 0)
                {
                    return;
                }
                else if (expectedCount == 1)
                {
                    PickerEntity entity = providerResult.Children[0].EntityData[0];
                    StringAssert.AreEqualIgnoringCase(expectedClaimValue, entity.Claim.Value);
                    return;
                }
                else
                {
                    foreach (var children in providerResult.Children)
                    {
                        foreach (var entity in children.EntityData)
                        {
                            if (String.Equals(expectedClaimValue, entity.Claim.Value))
                            {
                                StringAssert.AreEqualIgnoringCase(expectedClaimValue, entity.Claim.Value);
                                return;
                            }
                        }
                    }
                    Assert.Fail($"No entity with claim value {expectedClaimValue} was found in the entities returned by {UnitTestsHelper.ClaimsProviderName}");
                }
            }
        }
        Assert.AreEqual(expectedCount, 0);
    }

    public static void VerifyValidationResult(PickerEntity[] entities, bool shouldValidate, string expectedClaimValue)
    {
        int expectedCount = shouldValidate ? 1 : 0;
        Assert.AreEqual(expectedCount, entities.Length);
        if (shouldValidate) StringAssert.AreEqualIgnoringCase(expectedClaimValue, entities[0].Claim.Value);
    }
}

public class SearchEntityDataSourceCollection : IEnumerable
{
    public IEnumerator GetEnumerator()
    {
        yield return new[] { "yvand", "2", "yvand@contoso.local" };
        yield return new[] { "IDoNotExist", "0", "" };
        yield return new[] { "group1", "1", @"contoso.local\group1" };
    }
}

public class SearchEntityDataSource
{
    public static IEnumerable<TestCaseData> GetTestData()
    {
        DataTable dt = DataTable.New.ReadCsv(UnitTestsHelper.DataFile_SearchTests);
        foreach (Row row in dt.Rows)
        {
            var registrationData = new SearchEntityData();
            registrationData.Input = row["Input"];
            registrationData.ExpectedResultCount = Convert.ToInt32(row["ExpectedResultCount"]);
            registrationData.ExpectedEntityClaimValue = row["ExpectedEntityClaimValue"];
            yield return new TestCaseData(new object[] { registrationData });
        }
    }

    //public class ReadCSV
    //{
    //    public void GetValue()
    //    {
    //        TextReader tr1 = new StreamReader(@"c:\pathtofile\filename", true);

    //        var Data = tr1.ReadToEnd().Split('\n')
    //        .Where(l => l.Length > 0)  //nonempty strings
    //        .Skip(1)               // skip header 
    //        .Select(s => s.Trim())   // delete whitespace
    //        .Select(l => l.Split(',')) // get arrays of values
    //        .Select(l => new { Field1 = l[0], Field2 = l[1], Field3 = l[2] });
    //    }
    //}
}

public class SearchEntityData
{
    public string Input;
    public int ExpectedResultCount;
    public string ExpectedEntityClaimValue;
}

public class ValidateEntityDataSource
{
    public static IEnumerable<TestCaseData> GetTestData()
    {
        DataTable dt = DataTable.New.ReadCsv(UnitTestsHelper.DataFile_ValidationTests);
        foreach (Row row in dt.Rows)
        {
            var registrationData = new ValidateEntityData();
            registrationData.ClaimValue = row["ClaimValue"];
            registrationData.ShouldValidate = Convert.ToBoolean(row["ShouldValidate"]);
            registrationData.IsMemberOfTrustedGroup = Convert.ToBoolean(row["IsMemberOfTrustedGroup"]);
            yield return new TestCaseData(new object[] { registrationData });
        }
    }
}

public class ValidateEntityData
{
    public string ClaimValue;
    public bool ShouldValidate;
    public bool IsMemberOfTrustedGroup;
}
