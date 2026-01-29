param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null
)

. .\_Defaults.ps1

# call dnproto.exe to get legacy sessions
& $dnprotoPath /command ListLegacySessions /logLevel $logLevel /dataDir $dataDir