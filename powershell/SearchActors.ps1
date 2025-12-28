param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [string]$actor = $null,
    [string]$query
)

. .\_Defaults.ps1


& $dnprotoPath /command SearchActors /actor $actor /query $query /logLevel $logLevel /dataDir $dataDir
