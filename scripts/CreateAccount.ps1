param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$pds,
    [string]$handle,
    [string]$did,
    [string]$inviteCode,
    [string]$password
)

. .\_Defaults.ps1

# call dnproto.exe
& $dnprotoPath /command CreateAccount /pds $pds /handle $handle /did $did /invitecode $invitecode /password $password /logLevel $logLevel