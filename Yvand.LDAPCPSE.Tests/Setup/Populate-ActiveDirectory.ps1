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
    Write-Warning "Could not get the OU $ouDN : $_.Exception.Message"
}

$memberUsersNamePrefix = "testLdapcpUser_"
$groupNamePrefix = "testLdapcpGroup_"

# Set specific attributes for some users
$usersWithSpecificSettings = @( 
    @{ UserPrincipalName = "$($memberUsersNamePrefix)001@$($domainFqdn)"; IsMemberOfAllGroups = $true }
    @{ UserPrincipalName = "$($memberUsersNamePrefix)002@$($domainFqdn)"; UserAttributes = @{ "GivenName" = "firstname 002" } }
    @{ UserPrincipalName = "$($memberUsersNamePrefix)010@$($domainFqdn)"; IsMemberOfAllGroups = $true }
    @{ UserPrincipalName = "$($memberUsersNamePrefix)011@$($domainFqdn)"; IsMemberOfAllGroups = $true }
    @{ UserPrincipalName = "$($memberUsersNamePrefix)012@$($domainFqdn)"; IsMemberOfAllGroups = $true }
    @{ UserPrincipalName = "$($memberUsersNamePrefix)013@$($domainFqdn)"; IsMemberOfAllGroups = $true }
    @{ UserPrincipalName = "$($memberUsersNamePrefix)014@$($domainFqdn)"; IsMemberOfAllGroups = $true }
    @{ UserPrincipalName = "$($memberUsersNamePrefix)015@$($domainFqdn)"; IsMemberOfAllGroups = $true }
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
$totalUsers = 3
for ($i = 1; $i -le $totalUsers; $i++) {
    $accountName = "$($memberUsersNamePrefix)$("{0:D3}" -f $i)"
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

