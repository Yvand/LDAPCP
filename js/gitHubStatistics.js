/// <reference path="../node_modules/@types/jquery/index.d.ts" />
// npm install --save @types/jquery
/*function parseGitHubStatisticsResponse (p1) {
    console.log('Got callback.');
}*/
var GitHubStatistics;
(function (GitHubStatistics) {
    class LDAPCPStats {
        constructor() {
            //url: string = "http://ldapcp-functions.azurewebsites.net/api/GetRepoStats";
            this.url = "http://jsfiddle.net/echo/jsonp/";
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
    }
    GitHubStatistics.LDAPCPStats = LDAPCPStats;
})(GitHubStatistics || (GitHubStatistics = {}));
$(document).ready(function () {
    let stats = new GitHubStatistics.LDAPCPStats();
    let result = stats.getLatestStat();
    //$("#status")[0].innerHTML = message;
});
