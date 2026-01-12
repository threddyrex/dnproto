param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [bool]$logToDataDir = $false,
    [Parameter(Position = 0)]
    [string]$actor = $null,
    [string]$cursor = $null,
    [bool]$showDagCborTypes = $false
)

. .\_Defaults.ps1

if($cursor -ne $null)
{
& $dnprotoPath /command StartFirehoseConsumer /actor $actor /dataDir $dataDir /logLevel $logLevel /logToDataDir $logToDataDir /cursor $cursor /showDagCborTypes $showDagCborTypes
}
else
{
& $dnprotoPath /command StartFirehoseConsumer /actor $actor /dataDir $dataDir /logLevel $logLevel /logToDataDir $logToDataDir /showDagCborTypes $showDagCborTypes
}
