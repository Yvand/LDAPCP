# For developers

Project has evolved a lot since it started, and now most of the customizations can be made with LDAPCP administration pages, but some still require custom development:

- Use LDAPCP with multiple SPTrustedIdentityTokenIssuer objects. 
- Fully customize the display text or the value of permissions created by LDAPCP.

For that, it is required to create a custom class that inherits LDAPCP class. "LDAPCP for Developers.zip" contains a Visual Studio project with sample classes that cover various scenarios. Only 1 inherited claim provider is installed at a time, you need to edit the feature event receiver to install the claim provider you want to test.
Common mistakes to avoid: 

- **Always deactivate the farm feature before retracting the solution** (see "how to remove" above to understand why). 
- If you create your own SharePoint solution, **DO NOT forget to include the ldapcp.dll assembly in the wsp package**.

If you did any of the mistakes above, you will likely experience issues when you try to redeploy the solution because the feature was already installed. All features in the solution must be uninstalled before it can be redeployed.

In any case, do not directly edit LDAPCP class, it has been designed to be inherited so that you can customize it to fit your needs. If a scenario that you need is not covered, please submit it in the discussions tab.
