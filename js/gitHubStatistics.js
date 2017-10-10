/// <reference path="../node_modules/@types/jquery/index.d.ts" />
// npm install --save @types/jquery
var GitHubStatistics;
(function (GitHubStatistics) {
    class LDAPCPStats {
        constructor() {
            this.url = "http://ldapcp-functions.azurewebsites.net/api/GetRepoStats";
            this.authZKey = "Xs141m0QqIUrDBfecYvdhOf0cJJ8sA2LygLgkVcKmTdwIU5ELx1OCg==";
        }
        getLatestStat() {
            console.log('Hello World');
            $.ajax({
                method: "GET",
                crossDomain: true,
                contentType: "application/json",
                dataType: "json",
                url: this.url + "?code=" + this.authZKey + "&callback=?",
                success: function (responseData, textStatus, jqXHR) {
                    console.log("Data received");
                    var value = responseData;
                    console.log(value);
                },
                error: function (responseData, textStatus, errorThrown) {
                    alert('Request failed.');
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
