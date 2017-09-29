/// <reference path="typings/jquery/index.d.ts" />

namespace GitHubStatistics {
    export class LDAPCPStats {
        url: string = "http://ldapcp-functions.azurewebsites.net/api/GetRepoStats";
        authZKey: string = "Xs141m0QqIUrDBfecYvdhOf0cJJ8sA2LygLgkVcKmTdwIU5ELx1OCg=="
        getLatestStat() {
            console.log('Hello World');
            
            $.ajax({
                method: "GET",
                crossDomain: true,
                contentType: "application/json; charset=utf-8",
                dataType: "json",
                url: this.url + "code?" + this.authZKey,
            })
            .done(function( data ) {
                console.log( "Sample of data: ", data.slice( 0, 100 ) );
            });
        }
    }
}

$(document).ready(function () {
    let stats = new GitHubStatistics.LDAPCPStats();
    let result = stats.getLatestStat()
    //$("#status")[0].innerHTML = message;
});
