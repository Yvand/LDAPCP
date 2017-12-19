/// <reference path="../node_modules/@types/jquery/index.d.ts" />
// npm install --save @types/jquery
/*function parseGitHubStatisticsResponse (p1) {
    console.log('Got callback.');
}*/
var GitHubStatistics;
(function (GitHubStatistics) {
    class LDAPCPStats {
        constructor() {
            this.url = "http://ldapcp-functions.azurewebsites.net/api/GetRepoStats";
            //url: string = "http://jsfiddle.net/echo/jsonp/";
            this.authZKey = "Xs141m0QqIUrDBfecYvdhOf0cJJ8sA2LygLgkVcKmTdwIU5ELx1OCg==";
        }
        getLatestStat() {
            console.log("Sending query to " + this.url);
            $.ajax({
                method: "GET",
                crossDomain: true,
                data: { code: this.authZKey },
                dataType: "jsonp",
                jsonpCallback: "parseGitHubStatisticsResponse",
                url: this.url,
                success: function (responseData, textStatus, jqXHR) {
                    console.log("Data received");
                    var value = responseData;
                    console.log(value);
                },
                error: function (responseData, textStatus, errorThrown) {
                    console.log("Request failed: " + errorThrown);
                }
            });
        }
        static DecodeJSONResponse(json) {
            var obj = Object.assign({}, json, {
                created: new Date(json.DateStatCreatedUTC)
            });
            return obj;
        }
    }
    GitHubStatistics.LDAPCPStats = LDAPCPStats;
})(GitHubStatistics || (GitHubStatistics = {}));
//window.parseGitHubStatisticsResponse = function(data) {
function parseGitHubStatisticsResponse(data) {
    console.log("parseGitHubStatisticsResponse response received");
    var result = GitHubStatistics.LDAPCPStats.DecodeJSONResponse(data);
    $("#TotalDownloadCount").text(result.AllReleasesDownloadCount);
    $("#LatestReleaseDownloadCount").text(result.LatestReleaseDownloadCount);
    // $("#stats").text("<p>test</p>");
    // $("#stats").text(result.LatestReleaseDownloadCount);
    // $("#stats").text("<p>test</p>");
    console.log(result.LatestReleaseDownloadCount);
}
;
$(document).ready(function () {
    let stats = new GitHubStatistics.LDAPCPStats();
    let result = stats.getLatestStat();
    //$("#status")[0].innerHTML = message;
});
