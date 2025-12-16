param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [string]$actor = $null
)

. .\_Defaults.ps1


# call dnproto
& $dnprotoPath /command BackupAccount /actor $actor /dataDir $dataDir /logLevel $logLevel
