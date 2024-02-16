using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Metadata;
using Yvand.LdapClaimsProvider.Logging;
using WIF4_5 = System.Security.Claims;

namespace Yvand.LdapClaimsProvider.Configuration
{
    /// <summary>
    /// Stores configuration associated to a claim type, and its mapping with the Azure AD newAttribute (GraphProperty)
    /// </summary>
    public class ClaimTypeConfig : SPAutoSerializingObject, IEquatable<ClaimTypeConfig>
    {
        public string ClaimType
        {
            get { return _ClaimType; }
            set { _ClaimType = value; }
        }
        [Persisted]
        private string _ClaimType;

        public DirectoryObjectType DirectoryObjectType
        {
            get
            {
                return String.IsNullOrWhiteSpace(_DirectoryObjectType) ?
                    DirectoryObjectType.User :  // Default is User
                    (DirectoryObjectType)Enum.Parse(typeof(DirectoryObjectType), _DirectoryObjectType);
            }
            set { _DirectoryObjectType = value.ToString(); }
        }
        [Persisted]
        private string _DirectoryObjectType;

        /// <summary>
        /// Class of the newAttribute in LDAP, typically 'user' or 'group'
        /// </summary>
        public string DirectoryObjectClass
        {
            get { return _DirectoryObjectClass; }
            set { _DirectoryObjectClass = value; }
        }
        [Persisted]
        private string _DirectoryObjectClass;

        /// <summary>
        /// Name of the newAttribute in LDAP
        /// </summary>
        public string DirectoryObjectAttribute
        {
            get { return _DirectoryObjectAttribute; }
            set { _DirectoryObjectAttribute = value; }
        }
        [Persisted]
        private string _DirectoryObjectAttribute;

        public bool DirectoryObjectAttributeSupportsWildcard
        {
            get { return _DirectoryObjectAttributeSupportsWildcard; }
            set { _DirectoryObjectAttributeSupportsWildcard = value; }
        }
        [Persisted]
        private bool _DirectoryObjectAttributeSupportsWildcard = true;

        /// <summary>
        /// Gets or sets this property to define if the entity created is a User (SPClaimEntityTypes.User) or a Group (SPClaimEntityTypes.FormsRole). Accepted values are "User" or "FormsRole"
        /// </summary>
        public string SPEntityType
        {
            get { return _SPEntityType; }
            set
            {
                if (String.Equals(value, "User", StringComparison.CurrentCultureIgnoreCase) || String.Equals(value, ClaimsProviderConstants.GroupClaimEntityType, StringComparison.CurrentCultureIgnoreCase))
                {
                    _SPEntityType = value;
                }
            }
        }
        [Persisted]
        private string _SPEntityType;

        /// <summary>
        /// If true, this entry contains directory object settings but no claim type
        /// </summary>
        public bool IsAdditionalLdapSearchAttribute
        {
            get { return _IsAdditionalLdapSearchAttribute; }
            set { _IsAdditionalLdapSearchAttribute = value; }
        }
        [Persisted]
        private bool _IsAdditionalLdapSearchAttribute = false;

        /// <summary>
        /// Can contain a member of class PeopleEditorEntityDataKey https://learn.microsoft.com/en-us/previous-versions/office/sharepoint-server/ms415673(v=office.15)
        /// to populate additional metadata in permission created
        /// </summary>
        public string SPEntityDataKey
        {
            get { return _SPEntityDataKey; }
            set { _SPEntityDataKey = value; }
        }
        [Persisted]
        private string _SPEntityDataKey;

        /// <summary>
        /// Stores property SPTrustedClaimTypeInformation.DisplayName of current claim type.
        /// </summary>
        public string ClaimTypeDisplayName
        {
            get { return _ClaimTypeDisplayName; }
            set { _ClaimTypeDisplayName = value; }
        }
        [Persisted]
        private string _ClaimTypeDisplayName;

        /// <summary>
        /// Every claim value type is String by default
        /// </summary>
        public string ClaimValueType
        {
            get { return _ClaimValueType; }
            set { _ClaimValueType = value; }
        }
        [Persisted]
        private string _ClaimValueType = WIF4_5.ClaimValueTypes.String;

        /// <summary>
        /// This prefix is added to the value of the permission created. This is useful to add a domain name before a group name (for example "domain\group" instead of "group")
        /// </summary>
        public string ClaimValueLeadingToken
        {
            get { return _ClaimValueLeadingToken; }
            set { _ClaimValueLeadingToken = value; }
        }
        [Persisted]
        private string _ClaimValueLeadingToken;

        ///// <summary>
        ///// If set to true: permission created without LDAP lookup (possible if LeadingKeywordToBypassDirectory is set and user typed this keyword in the input) should not contain the prefix (set in PrefixToAddToValueReturned) in the value
        ///// </summary>
        //public bool DoNotAddClaimValuePrefixIfBypassLookup
        //{
        //    get { return _DoNotAddClaimValuePrefixIfBypassLookup; }
        //    set { _DoNotAddClaimValuePrefixIfBypassLookup = value; }
        //}
        //[Persisted]
        //private bool _DoNotAddClaimValuePrefixIfBypassLookup;

        /// <summary>
        /// Set this to tell LDAPCP to validate user input (and create the permission) without LDAP lookup if it contains this keyword at the beginning
        /// </summary>
        public string LeadingKeywordToBypassDirectory
        {
            get { return _LeadingKeywordToBypassLdapDuringSearch; }
            set { _LeadingKeywordToBypassLdapDuringSearch = value; }
        }
        [Persisted]
        private string _LeadingKeywordToBypassLdapDuringSearch = String.Empty;

        /// <summary>
        /// Set this property to customize display text of the permission with a specific LDAP newAttribute (different than LDAPAttributeName, that is the actual value of the permission)
        /// </summary>
        public string DirectoryObjectAttributeForDisplayText
        {
            get { return _DirectoryObjectAttributeForDisplayText; }
            set { _DirectoryObjectAttributeForDisplayText = value; }
        }
        [Persisted]
        private string _DirectoryObjectAttributeForDisplayText;

        /// <summary>
        /// Set this property to set a specific LDAP filter
        /// </summary>
        public string DirectoryObjectAdditionalFilter
        {
            get { return _DirectoryObjectAdditionalFilter; }
            set { _DirectoryObjectAdditionalFilter = value; }
        }
        [Persisted]
        private string _DirectoryObjectAdditionalFilter;

        /// <summary>
        /// Set to true to show the display name of claim type in parenthesis in display text of permission
        /// </summary>
        public bool ShowClaimNameInDisplayText
        {
            get { return _ShowClaimNameInDisplayText; }
            set { _ShowClaimNameInDisplayText = value; }
        }
        [Persisted]
        private bool _ShowClaimNameInDisplayText = true;

        public ClaimTypeConfig()
        {
        }

        /// <summary>
        /// Returns a copy of the current object. This copy does not have any member of the base SharePoint base class set
        /// </summary>
        /// <returns></returns>
        public ClaimTypeConfig CopyConfiguration()
        {
            ClaimTypeConfig copy = new ClaimTypeConfig();
            // Copy non-inherited private fields
            FieldInfo[] fieldsToCopy = this.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (FieldInfo field in fieldsToCopy)
            {
                field.SetValue(copy, field.GetValue(this));
            }
            return copy;
        }

        /// <summary>
        /// Apply configuration in parameter to current object. It does not copy SharePoint base class members
        /// </summary>
        /// <param name="configToApply"></param>
        internal void ApplyConfiguration(ClaimTypeConfig configToApply)
        {
            // Copy non-inherited private fields
            FieldInfo[] fieldsToCopy = this.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (FieldInfo field in fieldsToCopy)
            {
                field.SetValue(this, field.GetValue(configToApply));
            }
        }

        public bool Equals(ClaimTypeConfig other)
        {
            if (new ClaimTypeConfigSameConfig().Equals(this, other))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Implements ICollection<ClaimTypeConfig> to add validation when collection is changed
    /// </summary>
    public class ClaimTypeConfigCollection : ICollection<ClaimTypeConfig>
    {   // Follows article https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.icollection-1?view=netframework-4.7.1

        /// <summary>
        /// Internal collection serialized in persisted object
        /// </summary>
        internal Collection<ClaimTypeConfig> innerCol = new Collection<ClaimTypeConfig>();

        public int Count
        {
            get
            {
                // If innerCol is null, it means that collection is not correctly set in the persisted object, very likely because it was migrated from a previons version of AzureCP
                if (innerCol == null)
                {
                    return 0;
                }
                return innerCol.Count;
            }
        }

        public bool IsReadOnly => false;

        public ClaimTypeConfig UserIdentifierConfig
        {
            get
            {
                if (innerCol == null)
                {
                    return null;
                }
                ClaimTypeConfig ctConfig = GetIdentifierConfiguration(DirectoryObjectType.User);
                return ctConfig;
            }
        }

        public ClaimTypeConfig GroupIdentifierConfig
        {
            get
            {
                if (innerCol == null)
                {
                    return null;
                }
                ClaimTypeConfig ctConfig = GetIdentifierConfiguration(DirectoryObjectType.Group);
                return ctConfig;
            }
        }

        /// <summary>
        /// If set, more checks can be done when collection is changed
        /// </summary>
        public SPTrustedLoginProvider SPTrust { get; private set; }

        public ClaimTypeConfigCollection(SPTrustedLoginProvider spTrust)
        {
            this.SPTrust = spTrust;
        }

        internal ClaimTypeConfigCollection(ref Collection<ClaimTypeConfig> innerCol, SPTrustedLoginProvider spTrust)
        {
            this.innerCol = innerCol;
            this.SPTrust = spTrust;
        }

        public ClaimTypeConfig this[int index]
        {
            get { return (ClaimTypeConfig)innerCol[index]; }
            set { innerCol[index] = value; }
        }

        public void Add(ClaimTypeConfig item)
        {
            Add(item, true);
        }

        internal void Add(ClaimTypeConfig item, bool strictChecks)
        {
            if (String.IsNullOrEmpty(item.DirectoryObjectAttribute) || String.IsNullOrEmpty(item.DirectoryObjectClass))
            {
                throw new InvalidOperationException($"Properties DirectoryObjectAttribute and DirectoryObjectClass are required");
            }

            if (item.IsAdditionalLdapSearchAttribute && !String.IsNullOrEmpty(item.ClaimType))
            {
                throw new InvalidOperationException($"No claim type should be set if IsAdditionalLdapSearchAttribute is set to true");
            }

            if (!item.IsAdditionalLdapSearchAttribute && String.IsNullOrEmpty(item.ClaimType) && String.IsNullOrEmpty(item.SPEntityDataKey))
            {
                throw new InvalidOperationException($"SPEntityDataKey is required if ClaimType is not set and IsAdditionalLdapSearchAttribute is set to false");
            }

            if (Contains(item, new ClaimTypeConfigSamePermissionMetadata()))
            {
                throw new InvalidOperationException($"Entity metadata '{item.SPEntityDataKey}' already exists in the collection for the object type '{item.DirectoryObjectType}'");
            }

            if (Contains(item, new ClaimTypeConfigSameClaimType()))
            {
                throw new InvalidOperationException($"Claim type '{item.ClaimType}' already exists in the collection");
            }

            if (Contains(item, new ClaimTypeConfigEnsureUniquePrefixToBypassLookup()))
            {
                throw new InvalidOperationException($"Prefix '{item.LeadingKeywordToBypassDirectory}' is already set with another claim type and must be unique");
            }

            if (Contains(item, new ClaimTypeConfigSameDirectoryConfiguration()))
            {
                throw new InvalidOperationException($"An item with LDAP newAttribute '{item.DirectoryObjectAttribute}' and LDAP class '{item.DirectoryObjectClass}' already exists for the object type '{item.DirectoryObjectType}'");
            }

            if (Contains(item))
            {
                if (String.IsNullOrEmpty(item.ClaimType))
                    throw new InvalidOperationException($"This configuration with LDAP newAttribute '{item.DirectoryObjectAttribute}' and class '{item.DirectoryObjectClass}' already exists in the collection");
                else
                    throw new InvalidOperationException($"This configuration with claim type '{item.ClaimType}' already exists in the collection");
            }

            if (ClaimsProviderConstants.EnforceOnly1ClaimTypeForGroup && item.DirectoryObjectType == DirectoryObjectType.Group)
            {
                if (Contains(item, new ClaimTypeConfigEnforeOnly1ClaimTypePerObjectType()))
                {
                    throw new InvalidOperationException($"A claim type for DirectoryObjectType '{DirectoryObjectType.Group.ToString()}' already exists in the collection");
                }
            }

            if (strictChecks)
            {
                // If current item has IsAdditionalLdapSearchAttribute = true: check if another item with same DirectoryObjectType AND a claim type defined
                // Another valid item may be added later, and even if not, code should handle that scenario
                if (item.IsAdditionalLdapSearchAttribute && innerCol.FirstOrDefault(x => x.DirectoryObjectType == item.DirectoryObjectType && !String.IsNullOrEmpty(x.ClaimType)) == null)
                {
                    throw new InvalidOperationException($"Cannot add this item (with IsAdditionalLdapSearchAttribute set to true) because collecton does not contain an item with same DirectoryObjectType '{item.DirectoryObjectType.ToString()}' AND a ClaimType set");
                }
            }

            // If SPTrustedLoginProvider is set, additional checks can be done
            if (SPTrust != null)
            {
                // If current claim type is identity claim type: DirectoryObjectType must be User
                if (String.Equals(item.ClaimType, SPTrust.IdentityClaimTypeInformation.MappedClaimType, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (item.DirectoryObjectType != DirectoryObjectType.User)
                    {
                        throw new InvalidOperationException($"Identity claim type must be configured with DirectoryObjectType 'User'");
                    }
                }
            }
            innerCol.Add(item);
        }

        /// <summary>
        /// Only ClaimTypeConfig with property ClaimType already set can be updated
        /// </summary>
        /// <param name="oldClaimType">Claim type of ClaimTypeConfig object to update</param>
        /// <param name="newItem">New version of ClaimTypeConfig object</param>
        public void Update(string oldClaimType, ClaimTypeConfig newItem)
        {
            if (String.IsNullOrEmpty(oldClaimType)) { throw new ArgumentNullException(nameof(oldClaimType)); }
            if (newItem == null) { throw new ArgumentNullException(nameof(newItem)); }

            // If SPTrustedLoginProvider is set, additional checks can be done
            if (SPTrust != null)
            {
                // Specific checks if current claim type is identity claim type
                if (String.Equals(oldClaimType, SPTrust.IdentityClaimTypeInformation.MappedClaimType, StringComparison.InvariantCultureIgnoreCase))
                {
                    // We don't allow to change claim type
                    if (!String.Equals(newItem.ClaimType, oldClaimType, StringComparison.InvariantCultureIgnoreCase))
                    {
                        throw new InvalidOperationException($"Claim type cannot be changed because current item is the configuration of the identity claim type");
                    }

                    // DirectoryObjectType must be User
                    if (newItem.DirectoryObjectType != DirectoryObjectType.User)
                    {
                        throw new InvalidOperationException($"Identity claim type must be configured with DirectoryObjectType 'User'");
                    }
                }
            }

            // Create a temp collection that is a copy of current collection
            ClaimTypeConfigCollection testUpdateCollection = new ClaimTypeConfigCollection(this.SPTrust);
            foreach (ClaimTypeConfig curCTConfig in innerCol)
            {
                testUpdateCollection.Add(curCTConfig.CopyConfiguration(), false);
            }

            // Update ClaimTypeConfig in testUpdateCollection
            ClaimTypeConfig ctConfigToUpdate = testUpdateCollection.First(x => String.Equals(x.ClaimType, oldClaimType, StringComparison.InvariantCultureIgnoreCase));
            ctConfigToUpdate.ApplyConfiguration(newItem);

            // Test change in testUpdateCollection by adding all items in a new temp collection
            ClaimTypeConfigCollection testNewItemCollection = new ClaimTypeConfigCollection(this.SPTrust);
            foreach (ClaimTypeConfig curCTConfig in testUpdateCollection)
            {
                // ClaimTypeConfigCollection.Add() may thrown an exception if newItem is not valid for any reason
                testNewItemCollection.Add(curCTConfig, false);
            }

            // No error, current collection can safely be updated
            innerCol.First(x => String.Equals(x.ClaimType, oldClaimType, StringComparison.InvariantCultureIgnoreCase)).ApplyConfiguration(newItem);
        }

        /// <summary>
        /// Updates the properties <see cref="ClaimTypeConfig.DirectoryObjectClass"/> and <see cref="ClaimTypeConfig.DirectoryObjectAttribute"/> of the user identifier.
        /// If new values duplicate an existing item, it will be removed from the collection
        /// </summary>
        /// <param name="newDirectoryObjectClass">new DirectoryObjectClass</param>
        /// <param name="newDirectoryObjectAttribute">new newDirectoryObjectAttribute</param>
        /// <returns>True if the identity ClaimTypeConfig was successfully updated</returns>
        public bool UpdateUserIdentifier(string newDirectoryObjectClass, string newDirectoryObjectAttribute)
        {
            if (String.IsNullOrEmpty(newDirectoryObjectClass)) throw new ArgumentNullException(nameof(newDirectoryObjectClass));
            if (String.IsNullOrEmpty(newDirectoryObjectAttribute)) throw new ArgumentNullException(nameof(newDirectoryObjectAttribute));

            bool identifierConfigUpdated = false;
            if (SPTrust == null) { return identifierConfigUpdated; }

            ClaimTypeConfig userIdentifierConfig = UserIdentifierConfig;
            if (userIdentifierConfig == null)
            {
                return identifierConfigUpdated;
            }

            if (String.Equals(userIdentifierConfig.DirectoryObjectClass, newDirectoryObjectClass, StringComparison.InvariantCultureIgnoreCase) &&
                String.Equals(userIdentifierConfig.DirectoryObjectAttribute, newDirectoryObjectAttribute, StringComparison.InvariantCultureIgnoreCase))
            { return identifierConfigUpdated; }

            // Check if the new DirectoryObjectAttribute / DirectoryObjectClass duplicates an existing item, and delete it if so
            for (int i = 0; i < innerCol.Count; i++)
            {
                ClaimTypeConfig curCT = (ClaimTypeConfig)innerCol[i];
                if (curCT.DirectoryObjectType == DirectoryObjectType.User &&
                    String.Equals(curCT.DirectoryObjectAttribute, newDirectoryObjectAttribute, StringComparison.InvariantCultureIgnoreCase) &&
                    String.Equals(curCT.DirectoryObjectClass, newDirectoryObjectClass, StringComparison.InvariantCultureIgnoreCase))
                {
                    innerCol.RemoveAt(i);
                    break;  // There can be only 1 potential duplicate
                }
            }

            userIdentifierConfig.DirectoryObjectClass = newDirectoryObjectClass;
            userIdentifierConfig.DirectoryObjectAttribute = newDirectoryObjectAttribute;
            identifierConfigUpdated = true;
            return identifierConfigUpdated;
        }

        public bool UpdateGroupIdentifier(string newDirectoryObjectClass, string newDirectoryObjectAttribute)
        {
            if (String.IsNullOrEmpty(newDirectoryObjectClass)) throw new ArgumentNullException(nameof(newDirectoryObjectClass));
            if (String.IsNullOrEmpty(newDirectoryObjectAttribute)) throw new ArgumentNullException(nameof(newDirectoryObjectAttribute));

            bool identifierConfigUpdated = false;
            if (SPTrust == null) { return identifierConfigUpdated; }

            ClaimTypeConfig groupIdentifierConfig = GetIdentifierConfiguration(DirectoryObjectType.Group);
            if (groupIdentifierConfig == null)
            {
                return identifierConfigUpdated;
            }

            if (String.Equals(groupIdentifierConfig.DirectoryObjectClass, newDirectoryObjectClass, StringComparison.InvariantCultureIgnoreCase) &&
                String.Equals(groupIdentifierConfig.DirectoryObjectAttribute, newDirectoryObjectAttribute, StringComparison.InvariantCultureIgnoreCase))
            { return identifierConfigUpdated; }

            // Check if the new DirectoryObjectAttribute / DirectoryObjectClass duplicates an existing item, and delete it if so
            for (int i = 0; i < innerCol.Count; i++)
            {
                ClaimTypeConfig curCT = (ClaimTypeConfig)innerCol[i];
                if (curCT.DirectoryObjectType == DirectoryObjectType.Group &&
                    String.Equals(curCT.DirectoryObjectAttribute, newDirectoryObjectAttribute, StringComparison.InvariantCultureIgnoreCase) &&
                    String.Equals(curCT.DirectoryObjectClass, newDirectoryObjectClass, StringComparison.InvariantCultureIgnoreCase))
                {
                    innerCol.RemoveAt(i);
                    break;  // There can be only 1 potential duplicate
                }
            }

            groupIdentifierConfig.DirectoryObjectClass = newDirectoryObjectClass;
            groupIdentifierConfig.DirectoryObjectAttribute = newDirectoryObjectAttribute;
            identifierConfigUpdated = true;
            return identifierConfigUpdated;
        }

        public void Clear()
        {
            innerCol.Clear();
        }

        /// <summary>
        /// Test equality based on ClaimTypeConfigSameConfig (default implementation of IEquitable<T> in ClaimTypeConfig)
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Contains(ClaimTypeConfig item)
        {
            bool found = false;
            foreach (ClaimTypeConfig ct in innerCol)
            {
                if (ct.Equals(item))
                {
                    found = true;
                }
            }
            return found;
        }

        public bool Contains(ClaimTypeConfig item, EqualityComparer<ClaimTypeConfig> comp)
        {
            bool found = false;
            foreach (ClaimTypeConfig ct in innerCol)
            {
                if (comp.Equals(ct, item))
                {
                    found = true;
                }
            }
            return found;
        }

        public void CopyTo(ClaimTypeConfig[] array, int arrayIndex)
        {
            if (array == null) { throw new ArgumentNullException(nameof(array)); }
            if (arrayIndex < 0) { throw new ArgumentOutOfRangeException("The starting array index cannot be negative."); }
            if (Count > array.Length - arrayIndex + 1) { throw new ArgumentException("The destination array has fewer elements than the collection."); }

            for (int i = 0; i < innerCol.Count; i++)
            {
                array[i + arrayIndex] = innerCol[i];
            }
        }

        public void RemoveAll(IEnumerable<ClaimTypeConfig> elementsToRemove)
        {
            // ToList() creates a copy and avoids exception "Collection was modified; enumeration operation may not execute"
            foreach (ClaimTypeConfig ct in elementsToRemove.ToList())
            {
                Remove(ct);
            }
        }

        public bool Remove(ClaimTypeConfig item)
        {
            if (SPTrust != null && String.Equals(item.ClaimType, SPTrust.IdentityClaimTypeInformation.MappedClaimType, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new InvalidOperationException($"Cannot delete claim type \"{item.ClaimType}\" because it is the identity claim type of \"{SPTrust.Name}\"");
            }

            bool result = false;
            for (int i = 0; i < innerCol.Count; i++)
            {
                ClaimTypeConfig curCT = (ClaimTypeConfig)innerCol[i];
                if (curCT.Equals(item))
                {
                    innerCol.RemoveAt(i);
                    result = true;
                    break;
                }
            }
            return result;
        }

        public bool Remove(string claimType)
        {
            if (String.IsNullOrEmpty(claimType))
            {
                throw new ArgumentNullException(nameof(claimType));
            }
            if (SPTrust != null && String.Equals(claimType, SPTrust.IdentityClaimTypeInformation.MappedClaimType, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new InvalidOperationException($"Cannot delete claim type \"{claimType}\" because it is the identity claim type of \"{SPTrust.Name}\"");
            }

            bool result = false;
            for (int i = 0; i < innerCol.Count; i++)
            {
                ClaimTypeConfig curCT = (ClaimTypeConfig)innerCol[i];
                if (String.Equals(claimType, curCT.ClaimType, StringComparison.InvariantCultureIgnoreCase))
                {
                    innerCol.RemoveAt(i);
                    result = true;
                    break;
                }
            }
            return result;
        }

        public IEnumerator<ClaimTypeConfig> GetEnumerator()
        {
            return new ClaimTypeConfigEnumerator(this);
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return new ClaimTypeConfigEnumerator(this);
        }

        /// <summary>
        /// Returns the configuration for the given <paramref name="objectType"/>
        /// </summary>
        /// <param name="objectType"></param>
        /// <returns></returns>
        public ClaimTypeConfig GetIdentifierConfiguration(DirectoryObjectType objectType)
        {
            if (objectType == DirectoryObjectType.User)
            {
                // If user, add a test on the identity claim type
                return innerCol
                .FirstOrDefault(x =>
                    x.DirectoryObjectType == DirectoryObjectType.User &&
                    String.Equals(x.ClaimType, SPTrust.IdentityClaimTypeInformation.MappedClaimType, StringComparison.OrdinalIgnoreCase) &&
                    x.IsAdditionalLdapSearchAttribute == false);
            }
            else
            {
                // There can be only 1 DirectoryObjectType "Group"
                return innerCol
                .FirstOrDefault(x =>
                    x.DirectoryObjectType == DirectoryObjectType.Group &&
                    x.IsAdditionalLdapSearchAttribute == false);
            }
        }

        /// <summary>
        /// Returns the configuration for the given <paramref name="claimType"/>
        /// </summary>
        /// <param name="claimType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public ClaimTypeConfig GetByClaimType(string claimType)
        {
            if (String.IsNullOrEmpty(claimType)) { throw new ArgumentNullException(nameof(claimType)); }
            ClaimTypeConfig ctConfig = innerCol.FirstOrDefault(x => String.Equals(claimType, x.ClaimType, StringComparison.InvariantCultureIgnoreCase));
            return ctConfig;
        }

        /// <summary>
        /// Returns all configuration objects, excluding the identifier configuration, corresponding to the given <paramref name="entityType"/>
        /// </summary>
        /// <param name="entityType"></param>
        /// <returns></returns>
        public IEnumerable<ClaimTypeConfig> GetAdditionalConfigurationsForEntity(DirectoryObjectType entityType)
        {
            return innerCol
                .Where(x =>
                    x.DirectoryObjectType == entityType &&
                    x.IsAdditionalLdapSearchAttribute == true);
        }

        /// <summary>
        /// Returns the list of LDAP attributes used to search LDAP objects corresponding to the given <paramref name="entityType"/>
        /// </summary>
        /// <param name="entityType"></param>
        /// <returns></returns>
        public IEnumerable<string> GetSearchAttributesForEntity(DirectoryObjectType entityType)
        {
            var existingAdditionalUserConfigs = GetAdditionalConfigurationsForEntity(entityType);
            return existingAdditionalUserConfigs.Select(x => x.DirectoryObjectAttribute);
        }

        /// <summary>
        /// Updates the list of LDAP attributes used to search LDAP objects corresponding to the given <paramref name="entityType"/>
        /// </summary>
        /// <param name="newSearchAttributesCsv">The new list of LDAP attributes</param>
        /// <param name="ldapClass">The new LDAP class</param>
        /// <param name="entityType">The entity type for which this update applies</param>
        public void SetSearchAttributesForEntity(string newSearchAttributesCsv, string ldapClass, DirectoryObjectType entityType)
        {
            string[] newSearchAttributes = String.IsNullOrWhiteSpace(newSearchAttributesCsv) ? new string[] { } : newSearchAttributesCsv.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> newSearchAttributesList = newSearchAttributes.ToList();
            ClaimTypeConfig mainConfig = GetIdentifierConfiguration(entityType);
            var existingSearchAttributes = GetSearchAttributesForEntity(entityType);

            // Step 1: Add new search attributes
            foreach (string newAttribute in newSearchAttributes)
            {
                if (String.Equals(newAttribute, mainConfig.DirectoryObjectAttribute, StringComparison.InvariantCultureIgnoreCase))
                {
                    newSearchAttributesList.Remove(newAttribute);
                    continue;
                }
                if (existingSearchAttributes.Contains(newAttribute) == false)
                {
                    var newSearchAttributeConfig = new ClaimTypeConfig
                    {
                        ClaimType = String.Empty,
                        IsAdditionalLdapSearchAttribute = true,
                        DirectoryObjectType = entityType,
                        DirectoryObjectAttribute = newAttribute,
                        DirectoryObjectClass = ldapClass,
                        SPEntityDataKey = ClaimsProviderConstants.EntityMetadataPerLdapAttributes.ContainsKey(newAttribute) ? ClaimsProviderConstants.EntityMetadataPerLdapAttributes[newAttribute] : String.Empty,
                    };
                    try
                    {
                        Add(newSearchAttributeConfig);
                    }
                    catch (InvalidOperationException ex) 
                    {
                        // A InvalidOperationException is thrown if the LDAP attribute already exists as metadata
                        Logger.LogException(String.Empty, $"while trying to set the LDAP attribute {newAttribute} for entity type {entityType} as a search attribute", TraceCategory.Core, ex);
                    }
                }
            }

            // Step 2: Remove existing search attributes that are missing in newAdditionalLdapFilter
            var existingSearchAttributesToRemove = innerCol
                .Where(x =>
                    x.DirectoryObjectType == entityType &&
                    x.IsAdditionalLdapSearchAttribute == true &&
                    newSearchAttributesList.Contains(x.DirectoryObjectAttribute) == false);
            RemoveAll(existingSearchAttributesToRemove);
        }

        /// <summary>
        /// Updates the additional LDAP filter used to search LDAP objects corresponding to the given <paramref name="entityType"/>
        /// </summary>
        /// <param name="newAdditionalLdapFilter"></param>
        /// <param name="entityType"></param>
        public void SetAdditionalLdapFilterForEntity(string newAdditionalLdapFilter, DirectoryObjectType entityType)
        {
            ClaimTypeConfig mainConfig = GetIdentifierConfiguration(entityType);
            mainConfig.DirectoryObjectAdditionalFilter = newAdditionalLdapFilter;
            IEnumerable<ClaimTypeConfig> additionalConfigurations = GetAdditionalConfigurationsForEntity(entityType);
            foreach (ClaimTypeConfig additionalConfiguration in additionalConfigurations)
            {
                additionalConfiguration.DirectoryObjectAdditionalFilter = newAdditionalLdapFilter;
            }
        }
    }

    public class ClaimTypeConfigEnumerator : IEnumerator<ClaimTypeConfig>
    {
        private ClaimTypeConfigCollection _collection;
        private int curIndex;
        private ClaimTypeConfig curBox;


        public ClaimTypeConfigEnumerator(ClaimTypeConfigCollection collection)
        {
            _collection = collection;
            curIndex = -1;
            curBox = default(ClaimTypeConfig);

        }

        public bool MoveNext()
        {
            //Avoids going beyond the end of the collection.
            if (++curIndex >= _collection.Count)
            {
                return false;
            }
            else
            {
                // Set current box to next item in collection.
                curBox = _collection[curIndex];
            }
            return true;
        }

        public void Reset() { curIndex = -1; }

        void IDisposable.Dispose()
        {
            // Not implemented
        }

        public ClaimTypeConfig Current
        {
            get { return curBox; }
        }

        object IEnumerator.Current
        {
            get { return Current; }
        }
    }

    /// <summary>
    /// Ensure that properties ClaimType, DirectoryObjectProperty and DirectoryObjectType are unique
    /// </summary>
    public class ClaimTypeConfigSameConfig : EqualityComparer<ClaimTypeConfig>
    {
        public override bool Equals(ClaimTypeConfig existingCTConfig, ClaimTypeConfig newCTConfig)
        {
            if (String.Equals(existingCTConfig.ClaimType, newCTConfig.ClaimType, StringComparison.InvariantCultureIgnoreCase) &&
                String.Equals(existingCTConfig.DirectoryObjectAttribute, newCTConfig.DirectoryObjectAttribute, StringComparison.InvariantCultureIgnoreCase) &&
                String.Equals(existingCTConfig.DirectoryObjectClass, newCTConfig.DirectoryObjectClass, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode(ClaimTypeConfig ct)
        {
            string hCode = ct.ClaimType + ct.DirectoryObjectAttribute + ct.DirectoryObjectClass;
            return hCode.GetHashCode();
        }
    }

    /// <summary>
    /// Ensure that property ClaimType is unique
    /// </summary>
    public class ClaimTypeConfigSameClaimType : EqualityComparer<ClaimTypeConfig>
    {
        public override bool Equals(ClaimTypeConfig existingCTConfig, ClaimTypeConfig newCTConfig)
        {
            if (String.Equals(existingCTConfig.ClaimType, newCTConfig.ClaimType, StringComparison.InvariantCultureIgnoreCase) &&
                !String.IsNullOrEmpty(newCTConfig.ClaimType))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode(ClaimTypeConfig ct)
        {
            string hCode = ct.ClaimType + ct.DirectoryObjectType + ct.DirectoryObjectAttribute + ct.DirectoryObjectClass;
            return hCode.GetHashCode();
        }
    }

    /// <summary>
    /// Ensure that property SPEntityDataKey is unique for the DirectoryObjectType
    /// </summary>
    public class ClaimTypeConfigSamePermissionMetadata : EqualityComparer<ClaimTypeConfig>
    {
        public override bool Equals(ClaimTypeConfig existingCTConfig, ClaimTypeConfig newCTConfig)
        {
            if (!String.IsNullOrEmpty(newCTConfig.SPEntityDataKey) &&
                String.Equals(existingCTConfig.SPEntityDataKey, newCTConfig.SPEntityDataKey, StringComparison.InvariantCultureIgnoreCase) &&
                existingCTConfig.DirectoryObjectType == newCTConfig.DirectoryObjectType)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode(ClaimTypeConfig ct)
        {
            string hCode = ct.ClaimType + ct.DirectoryObjectType;
            return hCode.GetHashCode();
        }
    }

    /// <summary>
    /// Ensure that there is no duplicate of "LeadingKeywordToBypassDirectory" property
    /// </summary>
    internal class ClaimTypeConfigEnsureUniquePrefixToBypassLookup : EqualityComparer<ClaimTypeConfig>
    {
        public override bool Equals(ClaimTypeConfig existingCTConfig, ClaimTypeConfig newCTConfig)
        {
            if (!String.IsNullOrEmpty(newCTConfig.LeadingKeywordToBypassDirectory) &&
                String.Equals(newCTConfig.LeadingKeywordToBypassDirectory, existingCTConfig.LeadingKeywordToBypassDirectory, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode(ClaimTypeConfig ct)
        {
            string hCode = ct.LeadingKeywordToBypassDirectory;
            return hCode.GetHashCode();
        }
    }

    /// <summary>
    /// Should be used only to ensure that only 1 claim type is set per DirectoryObjectType
    /// </summary>
    internal class ClaimTypeConfigEnforeOnly1ClaimTypePerObjectType : EqualityComparer<ClaimTypeConfig>
    {
        public override bool Equals(ClaimTypeConfig existingCTConfig, ClaimTypeConfig newCTConfig)
        {
            if ((!String.IsNullOrEmpty(newCTConfig.ClaimType) && !String.IsNullOrEmpty(existingCTConfig.ClaimType)) &&
                existingCTConfig.DirectoryObjectType == newCTConfig.DirectoryObjectType &&
                existingCTConfig.IsAdditionalLdapSearchAttribute == newCTConfig.IsAdditionalLdapSearchAttribute &&
                newCTConfig.IsAdditionalLdapSearchAttribute == false)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode(ClaimTypeConfig ct)
        {
            string hCode = ct.ClaimType + ct.DirectoryObjectType + ct.IsAdditionalLdapSearchAttribute.ToString();
            return hCode.GetHashCode();
        }
    }

    /// <summary>
    /// Check if a given object type (user or group) has 2 ClaimTypeConfig with the same DirectoryObjectAttribute and DirectoryObjectClass
    /// </summary>
    public class ClaimTypeConfigSameDirectoryConfiguration : EqualityComparer<ClaimTypeConfig>
    {
        public override bool Equals(ClaimTypeConfig existingCTConfig, ClaimTypeConfig newCTConfig)
        {
            if (String.Equals(existingCTConfig.DirectoryObjectAttribute, newCTConfig.DirectoryObjectAttribute, StringComparison.InvariantCultureIgnoreCase) &&
                String.Equals(existingCTConfig.DirectoryObjectClass, newCTConfig.DirectoryObjectClass, StringComparison.InvariantCultureIgnoreCase) &&
                existingCTConfig.DirectoryObjectType == newCTConfig.DirectoryObjectType)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode(ClaimTypeConfig ct)
        {
            string hCode = ct.DirectoryObjectAttribute + ct.DirectoryObjectClass + ct.DirectoryObjectType;
            return hCode.GetHashCode();
        }
    }
}
