/// <reference path="../node_modules/@types/jquery/index.d.ts" />
// npm install --save @types/jquery
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
                url: this.url + "?code=" + this.authZKey + "&callback=?",
            })
            .done(function( data ) {
                console.log( "Sample of data: ", data);
            });
            //$.getJSON( this.url + "?code" + this.authZKey, function ( data ) { alert ( data ); } );
        }
    }
}

$(document).ready(function () {
    let stats = new GitHubStatistics.LDAPCPStats();
    let result = stats.getLatestStat()
    //$("#status")[0].innerHTML = message;
});
