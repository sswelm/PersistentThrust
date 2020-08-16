param ($DllPath = $(throw "Need filepath to dll!"), $ZipFilePath = $(throw "Need filepath to zip!"))

# example $DllPath = "D:\Development\KSP\PersistentThrust\8.1\GameData\PersistentThrust\Plugin\PersistentThrust.dll"
# example $ZipFilePath = "D:\Development\KSP\PersistentThrust\8.1\GameData.zip"

$FileName = [System.IO.Path]::GetFileNameWithoutExtension($DllPath) 

$vPSObject = get-command $DllPath
$Major = $vPSObject[0].Version.Major
$Minor = $vPSObject[0].Version.Minor
$Build = $vPSObject[0].Version.Build
$Revision = $vPSObject[0].Version.Revision
$NewName = "$($FileName) $($Major).$($Minor).$($Build).$($Revision).zip"


echo "new filename: $($NewName)"

Rename-Item -Path $ZipFilePath -NewName $NewName
