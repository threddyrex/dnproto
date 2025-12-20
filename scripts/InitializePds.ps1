param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [bool]$logToDataDir = $false,
    [string]$pdsHostName = $null,
    [string]$availableUserDomain = $null,
    [string]$userHandle = $null,
    [string]$userDid = $null
)

. .\_Defaults.ps1


& $dnprotoPath /command InitializePds /dataDir $dataDir /logLevel $logLevel /logToDataDir $logToDataDir /pdshostname $pdsHostName /availableuserdomain $availableUserDomain /userHandle $userHandle /userDid $userDid