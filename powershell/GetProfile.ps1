param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [Parameter(Position = 0)]
    [string]$actor = $null,
    [string]$sessionActor = $null
)

. .\_Defaults.ps1


& $dnprotoPath /command GetProfile /actor $actor /logLevel $logLevel /dataDir $dataDir /sessionActor $sessionActor
