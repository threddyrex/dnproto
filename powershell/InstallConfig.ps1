param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [bool]$logToDataDir = $false,
    [string]$pdsHostName = $null,
    [string]$availableUserDomain = $null,
    [string]$userHandle = $null,
    [string]$userDid = $null,
    [string]$userEmail = $null
)

. .\_Defaults.ps1


& $dnprotoPath /command InstallConfig /dataDir $dataDir /logLevel $logLevel /logToDataDir $logToDataDir /pdshostname $pdsHostName /availableuserdomain $availableUserDomain /userHandle $userHandle /userDid $userDid /userEmail $userEmail