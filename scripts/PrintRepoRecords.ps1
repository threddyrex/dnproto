param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [Parameter(Position = 0)]
    [string]$actor = $null,
    [string]$collection = $null,
    [string]$month = $null
)

. .\_Defaults.ps1

$command = "/command PrintRepoRecords /dataDir $dataDir /actor $actor /logLevel $logLevel"

if($collection -ne $null)
{
    $command += " /collection $collection"
}

if($month -ne $null)
{
    $command += " /month $month"
}

& $dnprotoPath $command.Split(' ')