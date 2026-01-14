param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null
)

. .\_Defaults.ps1

# call dnproto.exe to get handle info
& $dnprotoPath /command ListFirehoseEvents /logLevel $logLevel /dataDir $dataDir