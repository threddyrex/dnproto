param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [Parameter(Position = 0)]
    [string]$actor = $null
)

. .\_Defaults.ps1

& $dnprotoPath /command StartFirehoseConsumer /actor $actor /dataDir $dataDir /logLevel $logLevel