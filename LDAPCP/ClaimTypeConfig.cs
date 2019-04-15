using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using WIF4_5 = System.Security.Claims;

namespace ldapcp
{
    /// <summary>
    /// Defines an attribute / claim type configuration
    /// </summary>
    public class ClaimTypeConfig : SPAutoSerializingObject, IEquatable<ClaimTypeConfig>
    {
        /// <summary>
        /// Name of the attribute in LDAP
        /// </summary>
        public string LDAPAttribute
        {
            get { return _LDAPAttribute; }
            set { _LDAPAttribute = value; }
        }
        [Persisted]
        private string _LDAPAttribute;

        /// <summary>
        /// Class of the attribute in LDAP, typically 'user' or 'group'
        /// </summary>
        public string LDAPClass
        {
            get { return _LDAPClass; }
            set { _LDAPClass = value; }
        }
        [Persisted]
        private string _LDAPClass;

        public DirectoryObjectType EntityType
        {
            get { return (DirectoryObjectType)Enum.ToObject(typeof(DirectoryObjectType), _DirectoryObjectType); }
            set { _DirectoryObjectType = (int)value; }
        }
        [Persisted]
        private int _DirectoryObjectType;

        public string ClaimType
        {
            get { return _ClaimType; }
            set { _ClaimType = value; }
        }
        [Persisted]
        private string _ClaimType;

        public bool SupportsWildcard
        {
            get { return _SupportsWildcard; }
            set { _SupportsWildcard = value; }
        }
        [Persisted]
        private bool _SupportsWildcard = true;

        /// <summary>
        /// If set to true, property ClaimType should not be set
        /// </summary>
        public bool UseMainClaimTypeOfDirectoryObject
        {
            get { return _UseMainClaimTypeOfDirectoryObject; }
            set { _UseMainClaimTypeOfDirectoryObject = value; }
        }
        [Persisted]
        private bool _UseMainClaimTypeOfDirectoryObject = false;

        /// <summary>
        /// When creating a PickerEntry, it's possible to populate entry with additional attributes stored in EntityData hash table
        /// </summary>
        public string EntityDataKey
        {
            get { return _EntityDataKey; }
            set { _EntityDataKey = value; }
        }
        [Persisted]
        private string _EntityDataKey;

        /// <summary>
        /// Stores property SPTrustedClaimTypeInformation.DisplayName of current claim type.
        /// </summary>
        internal string ClaimTypeDisplayName
        {
            get { return _ClaimTypeMappingName; }
            set { _ClaimTypeMappingName = value; }
        }
        [Persisted]
        private string _ClaimTypeMappingName;

        /// <summary>
        /// Every claim value type is a string by default
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
        public string ClaimValuePrefix
        {
            get { return _ClaimValuePrefix; }
            set { _ClaimValuePrefix = value; }
        }
        [Persisted]
        private string _ClaimValuePrefix;

        /// <summary>
        /// If set to true: permission created without LDAP lookup (possible if PrefixToBypassLookup is set and user typed this keyword in the input) should not contain the prefix (set in PrefixToAddToValueReturned) in the value
        /// </summary>
        public bool DoNotAddClaimValuePrefixIfBypassLookup
        {
            get { return _DoNotAddClaimValuePrefixIfBypassLookup; }
            set { _DoNotAddClaimValuePrefixIfBypassLookup = value; }
        }
        [Persisted]
        private bool _DoNotAddClaimValuePrefixIfBypassLookup;

        /// <summary>
        /// Set this to tell LDAPCP to validate user input (and create the permission) without LDAP lookup if it contains this keyword at the beginning
        /// </summary>
        public string PrefixToBypassLookup
        {
            get { return _PrefixToBypassLookup; }
            set { _PrefixToBypassLookup = value; }
        }
        [Persisted]
        private string _PrefixToBypassLookup;

        /// <summary>
        /// Set this property to customize display text of the permission with a specific LDAP attribute (different than LDAPAttributeName, that is the actual value of the permission)
        /// </summary>
        public string LDAPAttributeToShowAsDisplayText
        {
            get { return _LDAPAttributeToShowAsDisplayText; }
            set { _LDAPAttributeToShowAsDisplayText = value; }
        }
        [Persisted]
        private string _LDAPAttributeToShowAsDisplayText;

        /// <summary>
        /// Set to only return values that exactly match the user input
        /// </summary>
        public bool FilterExactMatchOnly
        {
            get { return _FilterExactMatchOnly; }
            set { _FilterExactMatchOnly = value; }
        }
        [Persisted]
        private bool _FilterExactMatchOnly = false;

        /// <summary>
        /// Set this property to set a specific LDAP filter
        /// </summary>
        public string AdditionalLDAPFilter
        {
            get { return _AdditionalLDAPFilter; }
            set { _AdditionalLDAPFilter = value; }
        }
        [Persisted]
        private string _AdditionalLDAPFilter;

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
            // Copy non-inherited private members
            FieldInfo[] fieldsToCopy = this.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (FieldInfo field in fieldsToCopy)
            {
                field.SetValue(copy, field.GetValue(this));
            }
            return copy;
        }

        /// <summary>
        /// Apply configuration in parameter to current object. It does not copy SharePoint base class properties
        /// </summary>
        /// <param name="configToApply"></param>
        internal void ApplyConfiguration(ClaimTypeConfig configToApply)
        {
            // Copy non-inherited private members
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

        public int Count => innerCol.Count;

        public bool IsReadOnly => false;

        /// <summary>
        /// If set, more checks can be done when collection is changed
        /// </summary>
        public SPTrustedLoginProvider SPTrust;

        public ClaimTypeConfigCollection()
        {
        }

        internal ClaimTypeConfigCollection(ref Collection<ClaimTypeConfig> innerCol)
        {
            this.innerCol = innerCol;
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
            if (String.IsNullOrEmpty(item.LDAPAttribute) || String.IsNullOrEmpty(item.LDAPClass))
            {
                throw new InvalidOperationException($"Properties LDAPAttribute and LDAPClass are required");
            }

            if (item.UseMainClaimTypeOfDirectoryObject && !String.IsNullOrEmpty(item.ClaimType))
            {
                throw new InvalidOperationException($"No claim type should be set if UseMainClaimTypeOfDirectoryObject is set to true");
            }

            if (!item.UseMainClaimTypeOfDirectoryObject && String.IsNullOrEmpty(item.ClaimType) && String.IsNullOrEmpty(item.EntityDataKey))
            {
                throw new InvalidOperationException($"EntityDataKey is required if ClaimType is not set and UseMainClaimTypeOfDirectoryObject is set to false");
            }

            if (Contains(item, new ClaimTypeConfigSamePermissionMetadata()))
            {
                throw new InvalidOperationException($"Entity metadata '{item.EntityDataKey}' already exists in the collection for the object type '{item.EntityType}'");
            }

            if (Contains(item, new ClaimTypeConfigSameClaimType()))
            {
                throw new InvalidOperationException($"Claim type '{item.ClaimType}' already exists in the collection");
            }

            if (Contains(item, new ClaimTypeConfigEnsureUniquePrefixToBypassLookup()))
            {
                throw new InvalidOperationException($"Prefix '{item.PrefixToBypassLookup}' is already set with another claim type and must be unique");
            }

            if (Contains(item, new ClaimTypeConfigSameDirectoryConfiguration()))
            {
                throw new InvalidOperationException($"An item with LDAP attribute '{item.LDAPAttribute}' and LDAP class '{item.LDAPClass}' already exists for the object type '{item.EntityType}'");
            }

            if (Contains(item))
            {
                if (String.IsNullOrEmpty(item.ClaimType))
                    throw new InvalidOperationException($"This configuration with LDAP attribute '{item.LDAPAttribute}' and class '{item.LDAPClass}' already exists in the collection");
                else
                    throw new InvalidOperationException($"This configuration with claim type '{item.ClaimType}' already exists in the collection");
            }

            if (ClaimsProviderConstants.EnforceOnly1ClaimTypeForGroup && item.EntityType == DirectoryObjectType.Group)
            {
                if (Contains(item, new ClaimTypeConfigEnforeOnly1ClaimTypePerObjectType()))
                {
                    throw new InvalidOperationException($"A claim type for EntityType '{DirectoryObjectType.Group.ToString()}' already exists in the collection");
                }
            }

            if (strictChecks)
            {
                // If current item has UseMainClaimTypeOfDirectoryObject = true: check if another item with same EntityType AND a claim type defined
                // Another valid item may be added later, and even if not, code should handle that scenario
                if (item.UseMainClaimTypeOfDirectoryObject && innerCol.FirstOrDefault(x => x.EntityType == item.EntityType && !String.IsNullOrEmpty(x.ClaimType)) == null)
                {
                    throw new InvalidOperationException($"Cannot add this item (with UseMainClaimTypeOfDirectoryObject set to true) because collecton does not contain an item with same EntityType '{item.EntityType.ToString()}' AND a ClaimType set");
                }
            }

            // If SPTrustedLoginProvider is set, additional checks can be done
            if (SPTrust != null)
            {
                // If current claim type is identity claim type: EntityType must be User
                if (String.Equals(item.ClaimType, SPTrust.IdentityClaimTypeInformation.MappedClaimType, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (item.EntityType != DirectoryObjectType.User)
                    {
                        throw new InvalidOperationException($"Identity claim type must be configured with EntityType 'User'");
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
            if (String.IsNullOrEmpty(oldClaimType)) throw new ArgumentNullException("oldClaimType");
            if (newItem == null) throw new ArgumentNullException("newItem");

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

                    // EntityType must be User
                    if (newItem.EntityType != DirectoryObjectType.User)
                    {
                        throw new InvalidOperationException($"Identity claim type must be configured with EntityType 'User'");
                    }
                }
            }

            // Create a temp collection that is a copy of current collection
            ClaimTypeConfigCollection testUpdateCollection = new ClaimTypeConfigCollection();
            foreach (ClaimTypeConfig curCTConfig in innerCol)
            {
                testUpdateCollection.Add(curCTConfig.CopyConfiguration(), false);
            }

            // Update ClaimTypeConfig in testUpdateCollection
            ClaimTypeConfig ctConfigToUpdate = testUpdateCollection.First(x => String.Equals(x.ClaimType, oldClaimType, StringComparison.InvariantCultureIgnoreCase));
            ctConfigToUpdate.ApplyConfiguration(newItem);

            // Test change in testUpdateCollection by adding all items in a new temp collection
            ClaimTypeConfigCollection testNewItemCollection = new ClaimTypeConfigCollection();
            foreach (ClaimTypeConfig curCTConfig in testUpdateCollection)
            {
                // ClaimTypeConfigCollection.Add() may thrown an exception if newItem is not valid for any reason
                testNewItemCollection.Add(curCTConfig, false);
            }

            // No error, current collection can safely be updated
            innerCol.First(x => String.Equals(x.ClaimType, oldClaimType, StringComparison.InvariantCultureIgnoreCase)).ApplyConfiguration(newItem);
        }

        /// <summary>
        /// Update the LDAPClass and LDAPAttribute of the identity ClaimTypeConfig. If new values duplicate an existing item, it will be removed from the collection
        /// </summary>
        /// <param name="newLDAPClass">new LDAPClass</param>
        /// <param name="newLDAPAttribute">new newLDAPAttribute</param>
        /// <returns>True if the identity ClaimTypeConfig was successfully updated</returns>
        public bool UpdateUserIdentifier(string newLDAPClass, string newLDAPAttribute)
        {
            if (String.IsNullOrEmpty(newLDAPClass)) throw new ArgumentNullException("newLDAPClass");
            if (String.IsNullOrEmpty(newLDAPAttribute)) throw new ArgumentNullException("newLDAPAttribute");

            bool identifierUpdated = false;
            if (SPTrust == null)
                return identifierUpdated;

            ClaimTypeConfig identityClaimType = innerCol.FirstOrDefault(x => String.Equals(x.ClaimType, SPTrust.IdentityClaimTypeInformation.MappedClaimType, StringComparison.InvariantCultureIgnoreCase));
            if (identityClaimType == null)
                return identifierUpdated;

            if (String.Equals(identityClaimType.LDAPClass, newLDAPClass, StringComparison.InvariantCultureIgnoreCase) &&
                String.Equals(identityClaimType.LDAPAttribute, newLDAPAttribute, StringComparison.InvariantCultureIgnoreCase))
                return identifierUpdated;

            // Check if the new LDAPAttribute / LDAPClass duplicates an existing item, and delete it if so
            for (int i = 0; i < innerCol.Count; i++)
            {
                ClaimTypeConfig curCT = (ClaimTypeConfig)innerCol[i];
                if (curCT.EntityType == DirectoryObjectType.User &&
                    String.Equals(curCT.LDAPAttribute, newLDAPAttribute, StringComparison.InvariantCultureIgnoreCase) &&
                    String.Equals(curCT.LDAPClass, newLDAPClass, StringComparison.InvariantCultureIgnoreCase))
                {
                    innerCol.RemoveAt(i);
                    break;  // There can be only 1 potential duplicate
                }
            }            

            identityClaimType.LDAPClass = newLDAPClass;
            identityClaimType.LDAPAttribute = newLDAPAttribute;
            identifierUpdated = true;
            return identifierUpdated;
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
            if (array == null)
                throw new ArgumentNullException("The array cannot be null.");
            if (arrayIndex < 0)
                throw new ArgumentOutOfRangeException("The starting array index cannot be negative.");
            if (Count > array.Length - arrayIndex + 1)
                throw new ArgumentException("The destination array has fewer elements than the collection.");

            for (int i = 0; i < innerCol.Count; i++)
            {
                array[i + arrayIndex] = innerCol[i];
            }
        }

        public bool Remove(ClaimTypeConfig item)
        {
            if (SPTrust != null && String.Equals(item.ClaimType, SPTrust.IdentityClaimTypeInformation.MappedClaimType, StringComparison.InvariantCultureIgnoreCase)) throw new InvalidOperationException($"Cannot delete claim type \"{item.ClaimType}\" because it is the identity claim type of \"{SPTrust.Name}\"");

            bool result = false;
            for (int i = 0; i < innerCol.Count; i++)
            {
                ClaimTypeConfig curCT = (ClaimTypeConfig)innerCol[i];
                if (new ClaimTypeConfigSameConfig().Equals(curCT, item))
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
            if (String.IsNullOrEmpty(claimType)) throw new ArgumentNullException("claimType");
            if (SPTrust != null && String.Equals(claimType, SPTrust.IdentityClaimTypeInformation.MappedClaimType, StringComparison.InvariantCultureIgnoreCase)) throw new InvalidOperationException($"Cannot delete claim type \"{claimType}\" because it is the identity claim type of \"{SPTrust.Name}\"");

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

        public ClaimTypeConfig GetByClaimType(string claimType)
        {
            if (String.IsNullOrEmpty(claimType)) throw new ArgumentNullException("claimType");
            ClaimTypeConfig ctConfig = innerCol.FirstOrDefault(x => String.Equals(claimType, x.ClaimType, StringComparison.InvariantCultureIgnoreCase));
            return ctConfig;
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

        void IDisposable.Dispose() { }

        public ClaimTypeConfig Current
        {
            get { return curBox; }
        }


        object IEnumerator.Current
        {
            get { return Current; }
        }

    }

    public class ClaimTypeConfigSameConfig : EqualityComparer<ClaimTypeConfig>
    {
        public override bool Equals(ClaimTypeConfig existingCTConfig, ClaimTypeConfig newCTConfig)
        {
            if (String.Equals(existingCTConfig.ClaimType, newCTConfig.ClaimType, StringComparison.InvariantCultureIgnoreCase) &&
                String.Equals(existingCTConfig.LDAPAttribute, newCTConfig.LDAPAttribute, StringComparison.InvariantCultureIgnoreCase) &&
                String.Equals(existingCTConfig.LDAPClass, newCTConfig.LDAPClass, StringComparison.InvariantCultureIgnoreCase))
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
            string hCode = ct.ClaimType + ct.LDAPAttribute + ct.LDAPClass;
            return hCode.GetHashCode();
        }
    }

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
            string hCode = ct.ClaimType + ct.EntityType + ct.LDAPAttribute + ct.LDAPClass;
            return hCode.GetHashCode();
        }
    }

    public class ClaimTypeConfigSamePermissionMetadata : EqualityComparer<ClaimTypeConfig>
    {
        public override bool Equals(ClaimTypeConfig existingCTConfig, ClaimTypeConfig newCTConfig)
        {
            if (!String.IsNullOrEmpty(newCTConfig.EntityDataKey) &&
                String.Equals(existingCTConfig.EntityDataKey, newCTConfig.EntityDataKey, StringComparison.InvariantCultureIgnoreCase) &&
                existingCTConfig.EntityType == newCTConfig.EntityType)
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
            string hCode = ct.ClaimType + ct.EntityType;
            return hCode.GetHashCode();
        }
    }

    /// <summary>
    /// Ensure that there is no duplicate of "PrefixToBypassLookup" property
    /// </summary>
    internal class ClaimTypeConfigEnsureUniquePrefixToBypassLookup : EqualityComparer<ClaimTypeConfig>
    {
        public override bool Equals(ClaimTypeConfig existingCTConfig, ClaimTypeConfig newCTConfig)
        {
            if (!String.IsNullOrEmpty(newCTConfig.PrefixToBypassLookup) &&
                String.Equals(newCTConfig.PrefixToBypassLookup, existingCTConfig.PrefixToBypassLookup, StringComparison.InvariantCultureIgnoreCase))
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
            string hCode = ct.PrefixToBypassLookup;
            return hCode.GetHashCode();
        }
    }

    /// <summary>
    /// Should be used only to ensure that only 1 claim type is set per EntityType
    /// </summary>
    internal class ClaimTypeConfigEnforeOnly1ClaimTypePerObjectType : EqualityComparer<ClaimTypeConfig>
    {
        public override bool Equals(ClaimTypeConfig existingCTConfig, ClaimTypeConfig newCTConfig)
        {
            if ((!String.IsNullOrEmpty(newCTConfig.ClaimType) && !String.IsNullOrEmpty(existingCTConfig.ClaimType)) &&
                existingCTConfig.EntityType == newCTConfig.EntityType &&
                existingCTConfig.UseMainClaimTypeOfDirectoryObject == newCTConfig.UseMainClaimTypeOfDirectoryObject &&
                newCTConfig.UseMainClaimTypeOfDirectoryObject == false)
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
            string hCode = ct.ClaimType + ct.EntityType + ct.UseMainClaimTypeOfDirectoryObject.ToString();
            return hCode.GetHashCode();
        }
    }

    /// <summary>
    /// Check if a given object type (user or group) has 2 ClaimTypeConfig with the same LDAPAttribute and LDAPClass
    /// </summary>
    public class ClaimTypeConfigSameDirectoryConfiguration : EqualityComparer<ClaimTypeConfig>
    {
        public override bool Equals(ClaimTypeConfig existingCTConfig, ClaimTypeConfig newCTConfig)
        {
            if (String.Equals(existingCTConfig.LDAPAttribute, newCTConfig.LDAPAttribute, StringComparison.InvariantCultureIgnoreCase) &&
                String.Equals(existingCTConfig.LDAPClass, newCTConfig.LDAPClass, StringComparison.InvariantCultureIgnoreCase) &&
                existingCTConfig.EntityType == newCTConfig.EntityType)
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
            string hCode = ct.LDAPAttribute + ct.LDAPClass + ct.EntityType;
            return hCode.GetHashCode();
        }
    }
}
