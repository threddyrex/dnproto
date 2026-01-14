param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [Parameter(Mandatory=$true, Position = 0)]
    [string]$seq
)

. .\_Defaults.ps1

# call dnproto.exe to get handle info
& $dnprotoPath /command GetFirehoseEvent /seq $seq /logLevel $logLevel /dataDir $dataDir