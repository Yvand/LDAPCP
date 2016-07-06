# LDAPCP
This claims provider queries Active Directory and LDAP servers to enhance people picker with a great search experience in trusted authentication (typically ADFS).

# Features
- Works with SharePoint 2013 and SharePoint 2016. 
- Easy to configure with administration pages added in Central administration > Security. 
- Queries multiple servers in parallel (multi-threaded connections). 
- Populates properties (e.g. email, SIP, display name) upon permission creation. 
- Supports rehydration for provider-hosted add-ins. 
- Supports dynamics tokens "{domain}" and "{fqdn}" to add domain information on permissions to create. 
- Implements SharePoint logging infrastructure and logs messages in Area/Product "LDAPCP". 
- Ensures thread safety.
- Implements augmentation to populate SAML token of users with group membership upon authentication.

# Installation
The source code is now hosted in this repository but please visit the [Codeplex project](http://ldapcp.codeplex.com/) to download the SharePoint solution package.
