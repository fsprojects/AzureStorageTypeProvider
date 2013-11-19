# This script copies the Azure Storage dlls into the package folder where Azure Type Provider lives
param($installPath, $toolsPath, $package)

# This is where we want to copy Azure Storage dlls to.
$destPath = $installPath + "\lib\net40\"

# Get the Windows Storage folder
$deps = Get-ChildItem -Path ($installPath + "\..") | Where-Object { $_.Name.Contains("WindowsAzure.Storage") }
Foreach($d in $deps)
{
    # Find files in "<package-name>\lib\net40"
    $files = Get-ChildItem ($d.FullName + "\lib\net40") | where {$_.PSIsContainer -eq $False}
    Foreach ($file in $files)
    {
        # This gets executed each time project is loaded, so skip files if they exist already
        Copy-Item $file.FullName ($destPath + $file.Name) -Force -ErrorAction SilentlyContinue
    }
}