param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [string]$actor = $null,
    [string]$text,
    [string]$rkey
)

. .\_Defaults.ps1

# call dnproto.exe to get handle info
& $dnprotoPath /command ApplyWritesUpdatePost /actor $actor /dataDir $dataDir /logLevel $logLevel /text $text /rkey $rkey