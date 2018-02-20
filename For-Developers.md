# For developers

Project has evolved a lot since it started, and now most of the customizations can be made with LDAPCP administration pages, but some still require custom development:

- Use LDAPCP with multiple SPTrustedIdentityTokenIssuer.
- Depeply customize the display text or the value of claims created by LDAPCP.

For that, you can create a custom class that inherits LDAPCP class. [Each release](https://github.com/Yvand/LDAPCP/releases) of LDAPCP comes with its own version of LDAPCP.Developers.zip, which contains a Visual Studio project with sample classes that demonstrates various customizations.
Each class inheriting LDAPCP is a unique claims provider, and only 1 can be installed at a time by the feature event receiver.
Common mistakes to avoid:

- To avoid confusion, consider to completely uninstall standard LDAPCP.wsp solution before you deploy your sample.
- **Always deactivate the farm feature before retracting the solution** (see "how to remove" above to understand why).
- If you create your own SharePoint solution, **DO NOT forget to include the ldapcp.dll assembly in the wsp package**.

If you did any of the mistakes above, you will likely experience issues when you try to redeploy the solution because the feature was already installed. [Check this page](Fix-setup-issues.html) to resolve problems.

In any case, do not directly edit LDAPCP class, it has been designed to be inherited so that you can customize it to fit your needs. If a scenario that you need is not covered, please submit it in the discussions tab.
