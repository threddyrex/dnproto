param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [string]$actor = $null,
    [string]$month = $null
)

. .\_Defaults.ps1


if([string]::isnullorempty($month))
{
& $dnprotoPath /command PrintRepoPosts /dataDir $dataDir /actor $actor /logLevel $logLevel
}
else
{
& $dnprotoPath /command PrintRepoPosts /dataDir $dataDir /actor $actor /logLevel $logLevel /month $month
}
