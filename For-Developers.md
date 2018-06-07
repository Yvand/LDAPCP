## For developers

Project has evolved a lot since it started, and now most of the customizations can be made out of the box. You may want to customize LDAPCP to:

- Use LDAPCP with multiple SPTrustedIdentityTokenIssuer.
- Have full control on the entities (permissions) created by LDAPCP.

For that, you can create a custom class that inherits LDAPCP class. [Each release](https://github.com/Yvand/LDAPCP/releases) comes with its own version of LDAPCP.Developers.zip, which contains a Visual Studio project with sample classes that demonstrates various customizations.
Each class inheriting LDAPCP is a unique claims provider, and only 1 can be installed at a time by the feature event receiver.

Common mistakes to avoid:

- To avoid confusion, consider to completely uninstall standard LDAPCP.wsp solution before you deploy your sample.
- **Always deactivate the farm feature adding the claims provider before retracting the solution**. [Check this page](Remove-LDAPCP.html) for more information.
- If you create your own SharePoint solution, **DO NOT forget to include the ldapcp.dll assembly in the wsp package**.

If something goes wrong during solution deployment, [check this page](Fix-setup-issues.html) to resolve problems.  
In any case, do not directly edit LDAPCP class, it has been designed to be inherited so that you can customize it to fit your needs. If a scenario that you need is not covered, please submit it in the discussions tab.
