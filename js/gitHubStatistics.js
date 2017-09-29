/// <reference path="typings/jquery/index.d.ts" />
var GitHubStatistics;
(function (GitHubStatistics) {
    var LDAPCPStats = /** @class */ (function () {
        function LDAPCPStats() {
            this.url = "http://ldapcp-functions.azurewebsites.net/api/GetRepoStats";
            this.authZKey = "Xs141m0QqIUrDBfecYvdhOf0cJJ8sA2LygLgkVcKmTdwIU5ELx1OCg==";
        }
        LDAPCPStats.prototype.getLatestStat = function () {
            console.log('Hello World');
            $.ajax({
                method: "GET",
                crossDomain: true,
                contentType: "application/json; charset=utf-8",
                dataType: "json",
                url: this.url + "code?" + this.authZKey,
            })
                .done(function (data) {
                console.log("Sample of data: ", data.slice(0, 100));
            });
        };
        return LDAPCPStats;
    }());
    GitHubStatistics.LDAPCPStats = LDAPCPStats;
})(GitHubStatistics || (GitHubStatistics = {}));
$(document).ready(function () {
    var stats = new GitHubStatistics.LDAPCPStats();
    var result = stats.getLatestStat();
    //$("#status")[0].innerHTML = message;
});
//# sourceMappingURL=gitHubStatistics.js.map