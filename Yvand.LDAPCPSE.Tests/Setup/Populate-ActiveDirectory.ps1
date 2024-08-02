#Requires -Modules ActiveDirectory

<#
.SYNOPSIS
    Creates the users and groups in Active Directory, required to run the unit tests in LDAPCP project
.DESCRIPTION
    It creates the objects only if they do not exist (no overwrite).
    Group membership is always re-applied.
.LINK
    https://github.com/Yvand/LDAPCP/
#>


$domainFqdn = (Get-ADDomain -Current LocalComputer).DNSRoot
$domainDN = (Get-ADDomain -Current LocalComputer).DistinguishedName
$ouName = "ldapcp"
$ouDN = "OU=$($ouName),$($domainDN)"

$exportedUsersFullFilePath = "C:\YvanData\dev\LDAPCP_Tests_Users.csv"
$exportedGroupsFullFilePath = "C:\YvanData\dev\LDAPCP_Tests_Groups.csv"

try {
    Get-ADOrganizationalUnit -Identity $ouDN | Out-Null
}
catch {
    Write-Warning "Could not get the OU $ouDN : $($_.Exception.Message)"
    return
}

$userNamePrefix = "testLdapcpUser_"
$groupNamePrefix = "testLdapcpGroup_"

$confirmation = Read-Host "Connected to domain '$domainFqdn' and about to process users starting with '$userNamePrefix' and groups starting with '$groupNamePrefix'. Are you sure you want to proceed? [y/n]"
if ($confirmation -ne 'y') {
    Write-Warning -Message "Aborted."
    return
}

# Set specific attributes for some users
$usersWithSpecificSettings = @( 
    @{ UserPrincipalName = "$($userNamePrefix)001@$($domainFqdn)"; IsMemberOfAllGroups = $true }
    @{ UserPrincipalName = "$($userNamePrefix)002@$($domainFqdn)"; UserAttributes = @{ "GivenName" = "firstname_002" } }
    @{ UserPrincipalName = "$($userNamePrefix)003@$($domainFqdn)"; UserAttributes = @{ "GivenName" = "test_special)" } }
    @{ UserPrincipalName = "$($userNamePrefix)007@$($domainFqdn)"; UserAttributes = @{ "GivenName" = "James"; "displayName" = "James Bond" } }
    @{ UserPrincipalName = "$($userNamePrefix)010@$($domainFqdn)"; IsMemberOfAllGroups = $true }
    @{ UserPrincipalName = "$($userNamePrefix)011@$($domainFqdn)"; IsMemberOfAllGroups = $true }
    @{ UserPrincipalName = "$($userNamePrefix)012@$($domainFqdn)"; IsMemberOfAllGroups = $true }
    @{ UserPrincipalName = "$($userNamePrefix)013@$($domainFqdn)"; IsMemberOfAllGroups = $true }
    @{ UserPrincipalName = "$($userNamePrefix)014@$($domainFqdn)"; IsMemberOfAllGroups = $true }
    @{ UserPrincipalName = "$($userNamePrefix)015@$($domainFqdn)"; IsMemberOfAllGroups = $true }
    @{ UserPrincipalName = "$($userNamePrefix)020@$($domainFqdn)"; IsMemberOfNestedGroups = $true }
    @{ UserPrincipalName = "$($userNamePrefix)024@$($domainFqdn)"; IsMemberOfNestedGroups = $true }
    @{ UserPrincipalName = "$($userNamePrefix)028@$($domainFqdn)"; IsMemberOfNestedGroups = $true }
    @{ UserPrincipalName = "$($userNamePrefix)032@$($domainFqdn)"; IsMemberOfNestedGroups = $true }
    @{ UserPrincipalName = "$($userNamePrefix)036@$($domainFqdn)"; IsMemberOfNestedGroups = $true }
    @{ UserPrincipalName = "$($userNamePrefix)040@$($domainFqdn)"; IsMemberOfNestedGroups = $true }
    @{ UserPrincipalName = "$($userNamePrefix)042@$($domainFqdn)"; IsMemberOfNestedGroups = $true }
)

$groupsWithSpecificSettings = @(
    @{
        GroupName        = "$($groupNamePrefix)001"
        EveryoneIsMember = $true
    },
    @{
        GroupName        = "$($groupNamePrefix)005"
        EveryoneIsMember = $true
    },
    @{
        GroupName        = "$($groupNamePrefix)018"
        EveryoneIsMember = $true
    },
    @{
        GroupName     = "$($groupNamePrefix)020"
        IsNestedGroup = $true
    },
    @{
        GroupName     = "$($groupNamePrefix)021"
        IsNestedGroup = $true
    },
    @{
        GroupName     = "$($groupNamePrefix)022"
        IsNestedGroup = $true
    },
    @{
        GroupName     = "$($groupNamePrefix)023"
        IsNestedGroup = $true
    },
    @{
        GroupName              = "$($groupNamePrefix)025"
        NestedGroupsAreMembers = $true
    },
    @{
        GroupName              = "$($groupNamePrefix)026"
        NestedGroupsAreMembers = $true
    },
    @{
        GroupName        = "$($groupNamePrefix)025"
        EveryoneIsMember = $true
    },
    @{
        GroupName        = "$($groupNamePrefix)038"
        EveryoneIsMember = $true
    }
)

$temporaryPassword = @(
    (0..9 | Get-Random ),
    ('!', '@', '#', '$', '%', '^', '&', '*', '?', ';', '+' | Get-Random),
    (0..9 | Get-Random ),
    [char](65..90 | Get-Random),
    (0..9 | Get-Random ),
    [char](97..122 | Get-Random),
    [char](97..122 | Get-Random),
    (0..9 | Get-Random ),
    [char](97..122 | Get-Random)
) -Join ''

# Bulk add users if they do not already exist
$totalUsers = 100
$allUsers = @()
$allUsersAccountNames = @()
for ($i = 1; $i -le $totalUsers; $i++) {
    $accountName = "$($userNamePrefix)$("{0:D3}" -f $i)"
    $allUsersAccountNames += $accountName
    $user = $(try { Get-ADUser $accountName } catch { $null })
    if ($null -eq $user) {
        $userPrincipalName = "$($accountName)@$($domainFqdn)"
        $additionalUserAttributes = New-Object -TypeName HashTable
        $userHasSpecificAttributes = [System.Linq.Enumerable]::FirstOrDefault($usersWithSpecificSettings, [Func[object, bool]] { param($x) $x.UserPrincipalName -like $userPrincipalName })
        if ($null -ne $userHasSpecificAttributes.UserAttributes) {
            $additionalUserAttributes = $userHasSpecificAttributes.UserAttributes
        }
        $additionalUserAttributes.Add("mail", $userPrincipalName)

        $securePassword = ConvertTo-SecureString $temporaryPassword -AsPlainText -Force
        New-ADUser -Name "$accountName" -UserPrincipalName $userPrincipalName -OtherAttributes $additionalUserAttributes -Accountpassword $securePassword -Enabled $true -Path $ouDN
        Write-Host "Created user $accountName" -ForegroundColor Green
    }
    $user = Get-ADUser $accountName -Properties displayName, mail
    $allUsers += $user
}

# Bulk add groups if they do not already exist AND set their membership
$usersMemberOfAllGroups = [System.Linq.Enumerable]::Where($usersWithSpecificSettings, [Func[object, bool]] { param($x) $x.IsMemberOfAllGroups -eq $true }) | Select-Object -Property @{ Name = "AccountName"; Expression = { $_.UserPrincipalName.Split("@")[0] } }
$usersMemberOfNestedGroups = [System.Linq.Enumerable]::Where($usersWithSpecificSettings, [Func[object, bool]] { param($x) $x.IsMemberOfNestedGroups -eq $true }) | Select-Object -Property @{ Name = "AccountName"; Expression = { $_.UserPrincipalName.Split("@")[0] } }
$nestedGroups = [System.Linq.Enumerable]::Where($groupsWithSpecificSettings, [Func[object, bool]] { param($x) $x.IsNestedGroup -eq $true })
$totalGroups = 50
$allGroups = @()
for ($i = 1; $i -le $totalGroups; $i++) {
    $groupName = "$($groupNamePrefix)$("{0:D3}" -f $i)"
    $group = $(try { Get-ADGroup -Identity $groupName } catch { $null })
    if ($null -eq $group) {
        New-ADGroup -Name $groupName -DisplayName $groupName -GroupCategory Security -GroupScope Global -Path $ouDN
        Write-Host "Created group $groupName" -ForegroundColor Green
    }
    $group = Get-ADGroup -Identity $groupName
    $allGroups += $group

    # Remove and re-add all group members
    Get-ADGroupMember $groupName | ForEach-Object { Remove-ADGroupMember -Identity $groupName -Members $_ -Confirm:$false }
    Write-Host "Removed all members of group $groupName" -ForegroundColor Green
    
    $groupSettings = [System.Linq.Enumerable]::FirstOrDefault($groupsWithSpecificSettings, [Func[object, bool]] { param($x) $x.GroupName -like $groupName })
    if ($null -ne $groupSettings -and $groupSettings.ContainsKey("EveryoneIsMember") -and $groupSettings["EveryoneIsMember"] -eq $true) {
        # Add all users as members
        Add-ADGroupMember -Identity $groupName -Members $allUsersAccountNames
        Write-Host "Added all test users as members of group $groupName" -ForegroundColor Green
    }
    else {
        # Only users with IsMemberOfAllGroups true are members of this group
        $group | Add-ADGroupMember -Members $usersMemberOfAllGroups.AccountName
        Write-Host "Added users with EveryoneIsMember = true as members of group $groupName" -ForegroundColor Green
    }
    
    if ($null -ne $groupSettings -and $groupSettings.ContainsKey("IsNestedGroup") -and $groupSettings["IsNestedGroup"] -eq $true) {
        # Also add users with IsMemberOfNestedGroups
        $group | Add-ADGroupMember -Members $usersMemberOfNestedGroups.AccountName
        Write-Host "Added users with IsMemberOfNestedGroups = true as members of group $groupName" -ForegroundColor Green
    } elseif ($null -ne $groupSettings -and $groupSettings.ContainsKey("NestedGroupsAreMembers") -and $groupSettings["NestedGroupsAreMembers"] -eq $true) {
        # Also add nested groups since this group has NestedGroupsAreMembers
        $group | Add-ADGroupMember -Members $nestedGroups.GroupName
        Write-Host "Added nested groups to group $groupName because it has property NestedGroupsAreMembers true" -ForegroundColor Green
    }    
}

# export users and groups to their CSV file
$allUsers | 
Select-Object -Property UserPrincipalName, SamAccountName, Mail, GivenName, DisplayName, DistinguishedName, SID,
@{ Name = "IsMemberOfAllGroups"; Expression = { if ([System.Linq.Enumerable]::FirstOrDefault($usersWithSpecificSettings, [Func[object, bool]] { param($x) $x.UserPrincipalName -like $_.UserPrincipalName }).IsMemberOfAllGroups) { $true } else { $false } } },
@{ Name = "IsMemberOfNestedGroups"; Expression = { if ([System.Linq.Enumerable]::FirstOrDefault($usersWithSpecificSettings, [Func[object, bool]] { param($x) $x.UserPrincipalName -like $_.UserPrincipalName }).IsMemberOfNestedGroups) { $true } else { $false } } } |
Export-Csv -Path $exportedUsersFullFilePath -NoTypeInformation
Write-Host "Exported test users to CSV file $($exportedUsersFullFilePath)" -ForegroundColor Green

$allGroups | 
Select-Object -Property SamAccountName, DistinguishedName, SID, 
@{ Name = "EveryoneIsMember"; Expression = { if ([System.Linq.Enumerable]::FirstOrDefault($groupsWithSpecificSettings, [Func[object, bool]] { param($x) $x.GroupName -like $_.SamAccountName }).EveryoneIsMember) { $true } else { $false } } },
@{ Name = "IsNestedGroup"; Expression = { if ([System.Linq.Enumerable]::FirstOrDefault($groupsWithSpecificSettings, [Func[object, bool]] { param($x) $x.GroupName -like $_.SamAccountName }).IsNestedGroup) { $true } else { $false } } },
@{ Name = "NestedGroupsAreMembers"; Expression = { if ([System.Linq.Enumerable]::FirstOrDefault($groupsWithSpecificSettings, [Func[object, bool]] { param($x) $x.GroupName -like $_.SamAccountName }).NestedGroupsAreMembers) { $true } else { $false } } },
@{ Name = "AccountNameFqdn"; Expression = { "$($domainFqdn)\$($_.SamAccountName)" } } |
Export-Csv -Path $exportedGroupsFullFilePath -NoTypeInformation
Write-Host "Exported test groups to CSV file $($exportedGroupsFullFilePath)" -ForegroundColor Green
