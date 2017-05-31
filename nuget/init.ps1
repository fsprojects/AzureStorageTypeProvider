# This script copies the Azure Storage dlls into the package folder where Azure Type Provider lives
param($installPath, $toolsPath, $package)

# This is where we want to copy Azure Storage dlls to.
$destPath = $installPath + "\lib\net452\"

# Get the Windows Storage folder
$folders = ("WindowsAzure.Storage", "Microsoft.Data.Services.Client", "Microsoft.Data.OData", "Microsoft.Data.Edm", "System.Spatial")

Foreach($folder in $folders)
{
	$deps = Get-ChildItem -Path ($installPath + "\..\") | Where-Object { $_.Name.Contains($folder) }
	Foreach($d in $deps)
	{
	    $files = Get-ChildItem ($d.FullName + "\lib\net452") | where {$_.PSIsContainer -eq $False}
	    Foreach ($file in $files)
	    {
		# This gets executed each time project is loaded, so skip files if they exist already
		Copy-Item $file.FullName ($destPath + $file.Name) -Force -ErrorAction SilentlyContinue
	    }
	}
}