param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [string]$actor = $null
)

. .\_Defaults.ps1


& $dnprotoPath /command PrintRepoFollows /dataDir $dataDir /actor $actor /logLevel $logLevel
