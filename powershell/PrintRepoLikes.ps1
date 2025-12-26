param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [string]$actor = $null,
    [string]$month = $null
)

. .\_Defaults.ps1

& $dnprotoPath /command PrintRepoLikes /dataDir $dataDir /actor $actor /month $month /logLevel $logLevel
