param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null
)

. .\_Defaults.ps1

# call dnproto.exe to get oauth sessions
& $dnprotoPath /command DeleteOauthSessions /logLevel $logLevel /dataDir $dataDir