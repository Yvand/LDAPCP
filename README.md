# LDAPCP
This claims provider for SharePoint queries Active Directory and LDAP servers to enhance people picker with a great search experience in trusted authentication (typically ADFS).

# Features
- Works with SharePoint 2013 and SharePoint 2016. 
- Easy to configure with administration pages added in Central administration > Security. 
- Queries multiple servers in parallel (multi-threaded connections). 
- Populates properties (e.g. email, SIP, display name) upon permission creation. 
- Supports rehydration for provider-hosted add-ins. 
- Supports dynamics tokens "{domain}" and "{fqdn}" to add domain information on permissions to create. 
- Implements SharePoint logging infrastructure and logs messages in Area/Product "LDAPCP". 
- Ensures thread safety. 
- Implements augmentation to add group membership to security tokens.
- Resolves NetBiosName for LDAP connection

## Customization capabilities
- Customize list of claim types, and their mapping with LDAP objects. 
- Enable/disable augmentation globally or per LDAP connection. 
- Customize display of permissions. 
- Customize LDAP filter per claim type, e.g. to only return users member of a specific security group. 
- Set a keyword to bypass LDAP lookup. e.g. input "extuser:partner@contoso.com" directly creates permission "partner@contoso.com" on claim type set for this. 
- Set a prefix to add to LDAP results, e.g. add "domain\" to groups returned by LDAP. 
- Hide disabled users and distribution lists. 
- Developers can easily do a lot more by inheriting base class.

## Installation
This repo hosts the source code, please visit [project site on Codeplex](https://ldapcp.codeplex.com/) to download latest SharePoint solution package, find documentation and seek for assistance.
