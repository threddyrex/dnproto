param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [bool]$logToDataDir = $false,
    [string]$dataDir = $null,
    [Parameter(Position = 0)]
    [string]$actor = $null
)

. .\_Defaults.ps1


& $dnprotoPath /command PrintRepoMstAuthorFeed /dataDir $dataDir /actor $actor /logLevel $logLevel /logToDataDir $logToDataDir