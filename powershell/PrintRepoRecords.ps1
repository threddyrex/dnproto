param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [bool]$logToDataDir = $false,
    [Parameter(Position = 0)]
    [string]$actor = $null,
    [string]$repofile = $null,
    [string]$collection = $null,
    [string]$month = $null
)

. .\_Defaults.ps1

$command = "/command PrintRepoRecords /dataDir $dataDir /logLevel $logLevel /logToDataDir $logToDataDir"

if($collection -ne $null)
{
    $command += " /collection $collection"
}

if(-not [string]::IsNullOrWhiteSpace($month))
{
    $command += " /month $month"
}

if(-not [string]::IsNullOrWhiteSpace($repofile))
{
    $command += " /repofile $repofile"
}
elseif(-not [string]::IsNullOrWhiteSpace($actor))
{
    $command += " /actor $actor"
}

& $dnprotoPath $command.Split(' ')