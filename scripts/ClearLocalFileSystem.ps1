

. .\_Defaults.ps1

ls ($dataDir + "\actors\") | % { rm ($dataDir + "\actors\" + $_.Name) -force:$true}
ls ($dataDir + "\preferences\") | % { rm ($dataDir + "\preferences\" + $_.Name) -force:$true}
ls ($dataDir + "\repos\") | % { rm ($dataDir + "\repos\" + $_.Name) -force:$true}
ls ($dataDir + "\sessions\") | % { rm ($dataDir + "\sessions\" + $_.Name) -force:$true}
