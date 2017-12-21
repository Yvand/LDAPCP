/// <reference path="../node_modules/@types/jquery/index.d.ts" />
// npm install --save @types/jquery

/*function parseGitHubStatisticsResponse (p1) {
    console.log('Got callback.');    
}*/

namespace GitHubStatistics {    
    export interface RepoStatsJSON {
        DateStatCreatedUTC: string;
        Repository: string;
        LatestAssetUrl: string;
        LatestReleaseCreationDate: string;
        LatestReleaseTagName: string;
        LatestReleaseDownloadCount: number;
        AllReleasesDownloadCount: number;
        TotalDownloadCount: number;
    }

    export class LDAPCPStats {
        url: string = "http://ldapcp-functions.azurewebsites.net/api/GetRepoStats";
        //url: string = "http://jsfiddle.net/echo/jsonp/";
        authZKey: string = "Xs141m0QqIUrDBfecYvdhOf0cJJ8sA2LygLgkVcKmTdwIU5ELx1OCg==";
        getLatestStat() {
            console.log("Sending query to " + this.url);
            
            $.ajax({
                method: "GET",
                crossDomain: true,
                data: {code: this.authZKey},
                dataType: "jsonp",
                jsonpCallback: "parseGitHubStatisticsResponse",
                url: this.url,
                success: function(responseData, textStatus, jqXHR) {
                    console.log("Data received");                
                    var value = responseData;
                    console.log(value);
                },
                error: function (responseData, textStatus, errorThrown) {
                    console.log("Request failed: " + errorThrown);
                }
            });
        }

        static DecodeJSONResponse(json: GitHubStatistics.RepoStatsJSON) {
            var obj = Object.assign({}, json, {
                created: new Date(json.DateStatCreatedUTC)
            });
            return obj;
        }

        static ParseGitHubStatisticsResponse (data) {
            console.log("parseGitHubStatisticsResponse response received");
            var result =  GitHubStatistics.LDAPCPStats.DecodeJSONResponse(data);
            $("#TotalDownloadCount").text(result.TotalDownloadCount);
            $("#LatestReleaseDownloadCount").text(result.LatestReleaseDownloadCount);
            $("#LatestReleaseTagName").text(result.LatestReleaseTagName);
            console.log(result);
        };
    }
}

/*//window.parseGitHubStatisticsResponse = function(data) {
function parseGitHubStatisticsResponse (data) {
    console.log("parseGitHubStatisticsResponse response received");
    var result =  GitHubStatistics.LDAPCPStats.DecodeJSONResponse(data);
    $("#TotalDownloadCount").text(result.TotalDownloadCount);
    $("#LatestReleaseDownloadCount").text(result.LatestReleaseDownloadCount);
    console.log(result);
};*/

$(document).ready(function () {
    let stats = new GitHubStatistics.LDAPCPStats();
    let result = stats.getLatestStat()
    //$("#status")[0].innerHTML = message;
});

