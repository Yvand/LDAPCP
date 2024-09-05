# Sample with a hard-coded configuration and a manual reference to LDAPCPSE

This project shows how to create a claims provider that inherits LDAPCPSE. It uses a simple, hard-coded configuration to specify the tenant.

> [!WARNING]
> Do NOT deploy this solution in a SharePoint farm that already has LDAPCPSE deployed, unless both use **exactly** the same versions of NuGet dependencies. If they use different versions, that may cause errors when loading DLLs, due to mismatches with the assembly bindings in the machine.config file.

> [!IMPORTANT]  
> You need to manually add a reference to `Yvand.LDAPCPSE.dll`.
