param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$pds = $null,
    [string]$adminPassword = $null,
    [int]$useCount = 1
)

. .\_Defaults.ps1

# call dnproto.exe
& $dnprotoPath /command CreateInviteCode /pds $pds /adminPassword $adminPassword /useCount $useCount /logLevel $logLevel