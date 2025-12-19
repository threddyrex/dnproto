param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [Parameter(Position = 0)]
    [string]$actor = $null,
    [string]$cursor = $null
)

. .\_Defaults.ps1

if($cursor -ne $null)
{
& $dnprotoPath /command StartFirehoseConsumer /actor $actor /dataDir $dataDir /logLevel $logLevel /cursor $cursor
}
else
{
& $dnprotoPath /command StartFirehoseConsumer /actor $actor /dataDir $dataDir /logLevel $logLevel
}
