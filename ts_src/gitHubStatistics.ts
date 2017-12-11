/// <reference path="../node_modules/@types/jquery/index.d.ts" />
// npm install --save @types/jquery
namespace GitHubStatistics {
    export class LDAPCPStats {
        url: string = "http://ldapcp-functions.azurewebsites.net/api/GetRepoStats";
        authZKey: string = "Xs141m0QqIUrDBfecYvdhOf0cJJ8sA2LygLgkVcKmTdwIU5ELx1OCg==";
        getLatestStat() {
            console.log('Hello World');
            
            $.ajax({
                method: "GET",
                crossDomain: true,
                contentType: "application/json; charset=utf-8",
                dataType: "jsonp",
                url: this.url + "?code=" + this.authZKey + "&callback=parseGitHubStatisticsResponse",
                success: function(responseData, textStatus, jqXHR) {
                    console.log("Data received");                
                    var value = responseData;
                    console.log(value);
                },
                error: function (responseData, textStatus, errorThrown) {
                    console.log('Request failed.');
                }
            });
        }
    }
}

$(document).ready(function () {
    let stats = new GitHubStatistics.LDAPCPStats();
    let result = stats.getLatestStat()
    //$("#status")[0].innerHTML = message;
});

function parseGitHubStatisticsResponse (p1) {
    console.log('Got callback.');    
}