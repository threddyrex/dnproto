param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [string]$actor1 = $null,
    [string]$actor2 = $null
)

. .\_Defaults.ps1

& $dnprotoPath /command PrintRepoComparison /dataDir $dataDir /actor1 $actor1 /actor2 $actor2 /logLevel $logLevel
