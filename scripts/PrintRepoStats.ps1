param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [Parameter(Position = 0)]
    [string]$actor = $null
)

. .\_Defaults.ps1


& $dnprotoPath /command PrintRepoStats /dataDir $dataDir /actor $actor /logLevel $logLevel
