#Requires -Modules ActiveDirectory

<#
.SYNOPSIS
    Creates the users and groups in Active Directory, required to run the unit tests in LDAPCP project
.DESCRIPTION
    It creates the objects only if they do not exist (no overwrite)
.LINK
    https://github.com/Yvand/LDAPCP/
#>


$domainFqdn = (Get-ADDomain -Current LocalComputer).DNSRoot
$domainDN = (Get-ADDomain -Current LocalComputer).DistinguishedName
$ouName = "ldapcp"
$ouDN = "OU=$($ouName),$($domainDN)"

try {
    Get-ADOrganizationalUnit -Identity $ouDN | Out-Null
} catch {
    Write-Warning "Could not get the OU $ouDN : $($_.Exception.Message)"
    return
}

$memberUsersNamePrefix = "testLdapcpUser_"
$groupNamePrefix = "testLdapcpGroup_"

# Set specific attributes for some users
$usersWithSpecificSettings = @( 
    @{ UserPrincipalName = "$($memberUsersNamePrefix)001@$($domainFqdn)"; IsMemberOfAllGroups = $true }
    @{ UserPrincipalName = "$($memberUsersNamePrefix)002@$($domainFqdn)"; UserAttributes = @{ "GivenName" = "firstname_002" } }
    @{ UserPrincipalName = "$($memberUsersNamePrefix)003@$($domainFqdn)"; UserAttributes = @{ "GivenName" = "test_special)" } }
    @{ UserPrincipalName = "$($memberUsersNamePrefix)010@$($domainFqdn)"; IsMemberOfAllGroups = $true }
    @{ UserPrincipalName = "$($memberUsersNamePrefix)011@$($domainFqdn)"; IsMemberOfAllGroups = $true }
    @{ UserPrincipalName = "$($memberUsersNamePrefix)012@$($domainFqdn)"; IsMemberOfAllGroups = $true }
    @{ UserPrincipalName = "$($memberUsersNamePrefix)013@$($domainFqdn)"; IsMemberOfAllGroups = $true }
    @{ UserPrincipalName = "$($memberUsersNamePrefix)014@$($domainFqdn)"; IsMemberOfAllGroups = $true }
    @{ UserPrincipalName = "$($memberUsersNamePrefix)015@$($domainFqdn)"; IsMemberOfAllGroups = $true }
)

$groupsWithSpecificSettings = @(
    @{
        GroupName              = "$($groupNamePrefix)001"
        EveryoneIsMember = $true
    },
    @{
        GroupName              = "$($groupNamePrefix)005"
        EveryoneIsMember = $true
    },
    @{
        GroupName              = "$($groupNamePrefix)018"
        EveryoneIsMember = $true
    },
    @{
        GroupName              = "$($groupNamePrefix)025"
        EveryoneIsMember = $true
    },
    @{
        GroupName              = "$($groupNamePrefix)038"
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
$totalUsers = 999
$allUsersAccountNames = @()
for ($i = 1; $i -le $totalUsers; $i++) {
    $accountName = "$($memberUsersNamePrefix)$("{0:D3}" -f $i)"
    $allUsersAccountNames += $accountName
    $user = $(try {Get-ADUser $accountName} catch {$null})
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
}

# Bulk add groups if they do not already exist
$totalGroups = 50
for ($i = 1; $i -le $totalGroups; $i++) {
    $accountName = "$($groupNamePrefix)$("{0:D3}" -f $i)"
    $group = $(try {Get-ADGroup -Identity $accountName} catch {$null})
    if ($null -eq $group) {
        New-ADGroup -Name $accountName -DisplayName $accountName -GroupCategory Security -GroupScope Global -Path $ouDN
        Write-Host "Created group $accountName" -ForegroundColor Green
    }
}

# Set group membership
$groupsEveryoneIsMember = [System.Linq.Enumerable]::Where($groupsWithSpecificSettings, [Func[object, bool]] { param($x) $x.EveryoneIsMember -eq $true })
foreach ($groupEveryoneIsMember in $groupsEveryoneIsMember) {
    $group = $(try {Get-ADGroup -Identity $groupEveryoneIsMember.GroupName} catch {$null})
    if ($null -ne $group) {
        # Remove and re-add all group members
        Get-ADGroupMember $groupEveryoneIsMember.GroupName | ForEach-Object {Remove-ADGroupMember $groupEveryoneIsMember.GroupName $_ -Confirm:$false}
        Add-ADGroupMember -Identity $groupEveryoneIsMember.GroupName -Members $allUsersAccountNames
        Write-Host "Added all test users as members of group $($groupEveryoneIsMember.GroupName)" -ForegroundColor Green
    }
}
