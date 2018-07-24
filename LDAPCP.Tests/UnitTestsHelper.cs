using DataAccess;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
using Microsoft.SharePoint.WebControls;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[SetUpFixture]
public class UnitTestsHelper
{
    public const string ClaimsProviderName = "LDAPCP";
    public static Uri Context = new Uri("http://spsites/sites/LDAPCP.UnitTests");
    public const int MaxTime = 30000;
    public const string FarmAdmin = @"i:0#.w|contoso\yvand";

    public const string TrustedGroupToAdd_Id = @"contoso.local\group1";
    public const string TrustedGroupToAdd_ClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

    public const string DataFile_PeoplePickerTests = @"F:\Data\Dev\LDAPCP_PeoplePickerTestsData.csv";
    public const string DataFile_AugmentationTests = @"F:\Data\Dev\LDAPCP_AugmentationTestsData.csv";

    public static SPTrustedLoginProvider SPTrust
    {
        get => SPSecurityTokenServiceManager.Local.TrustedLoginProviders.FirstOrDefault(x => String.Equals(x.ClaimProviderName, UnitTestsHelper.ClaimsProviderName, StringComparison.InvariantCultureIgnoreCase));
    }

    [OneTimeSetUp]
    public static void CheckSiteCollection()
    {
        SPWebApplication wa = SPWebApplication.Lookup(Context);
        if (wa != null)
        {
            Console.WriteLine($"Web app {wa.Name} found.");
            SPClaimProviderManager claimMgr = SPClaimProviderManager.Local;
            SPClaim claim = new SPClaim(TrustedGroupToAdd_ClaimType, TrustedGroupToAdd_Id, "http://www.w3.org/2001/XMLSchema#string", SPOriginalIssuers.Format(SPOriginalIssuerType.TrustedProvider, SPTrust.Name));
            string encodedClaim = claimMgr.EncodeClaim(claim);
            SPUserInfo userInfo = new SPUserInfo { LoginName = encodedClaim, Name = TrustedGroupToAdd_Id };

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

    public static void ValidateEntities(SPProviderHierarchyTree[] providerResults, int expectedCount, string expectedClaimValue)
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
}
public class PeoplePickerCSVSource
{
    public static IEnumerable<TestCaseData> GetTestCases()
    {
        DataTable dt = DataTable.New.ReadCsv(UnitTestsHelper.DataFile_PeoplePickerTests);

        foreach (Row row in dt.Rows)
        {
            var registrationData = new PeoplePickerCSVData();
            registrationData.Input = row["Input"];
            registrationData.ExpectedResultCount = row["ExpectedResultCount"];
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

public class PeoplePickerCSVData
{
    public string Input;
    public string ExpectedResultCount;
    public string ExpectedEntityClaimValue;
}
