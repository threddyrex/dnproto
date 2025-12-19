param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$pds = $null,
    [string]$adminPassword = $null,
    [int]$codeCount = 1,
    [int]$useCount = 1
)

. .\_Defaults.ps1

# call dnproto.exe
& $dnprotoPath /command CreateInviteCodes /pds $pds /adminPassword $adminPassword /codeCount $codeCount /useCount $useCount /logLevel $logLevel