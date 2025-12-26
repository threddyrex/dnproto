param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [Parameter(Position = 0)]
    [string]$actor = $null,
    [string]$repofile = $null
)

. .\_Defaults.ps1


if(-not [string]::IsNullOrWhiteSpace($repofile))
{
& $dnprotoPath /command PrintRepoStats /dataDir $dataDir /logLevel $logLevel /repofile $repofile
}
elseif(-not [string]::IsNullOrWhiteSpace($actor))
{
& $dnprotoPath /command PrintRepoStats /dataDir $dataDir /logLevel $logLevel /actor $actor 
}
