param (
$DllPath = $(throw "Need filepath to dll as first parameter!"), 
$ZipFilePath = $(throw "Need filepath to download as second parameter!"))

# reconstruct version
$vPSObject = get-command $DllPath
$Major = $vPSObject[0].Version.Major
$Minor = $vPSObject[0].Version.Minor
$Build = $vPSObject[0].Version.Build
$Revision = $vPSObject[0].Version.Revision
$Version = "$($Major).$($Minor).$($Build).$($Revision)"
$Version = $Version.TrimStart()

# construct tag name
$TagName = "v$($Version)"

# construct publish name 
$FileName = [System.IO.Path]::GetFileNameWithoutExtension($DllPath)
$PublishName = "$($FileName) $($Version)"
$DownloadName = "$($PublishName).zip"

# create header
$headers = New-Object "System.Collections.Generic.Dictionary[[String],[String]]"
$headers.Add("Authorization", "Bearer c50a9c2a8b150588c9001874e957e0ed2dfed388")
$headers.Add("Content-Type", "text/plain")
$headers.Add("Cookie", "_octo=GH1.1.132013813.1594973163; logged_in=no")

# create body
$body = "{
`n  `"tag_name`": `"$($TagName)`",
`n  `"target_commitish`": `"master`",
`n  `"name`": `"$($PublishName)`",
`n  `"body`": `"Test Release`",
`n  `"draft`": false,
`n  `"prerelease`": true
`n}"

$response = Invoke-RestMethod 'https://api.github.com/repos/sswelm/PersistentThrust/releases' -Method 'POST' -Headers $headers -Body $body

# retrieve releaseId
$ReleaseId = $response.id

$headers = New-Object "System.Collections.Generic.Dictionary[[String],[String]]"
$headers.Add("Authorization", "Bearer c50a9c2a8b150588c9001874e957e0ed2dfed388")
$headers.Add("Content-Type", "application/zip")
$headers.Add("Cookie", "_octo=GH1.1.132013813.1594973163; logged_in=no")

# read a file a an biary
$body = Get-Content($ZipFilePath) -Raw

$uri = "https://uploads.github.com/repos/sswelm/PersistentThrust/releases/$($ReleaseId)/assets?name=$($DownloadName)"

$response = Invoke-RestMethod $uri -Method 'POST' -Headers $headers -Body $body
$response | ConvertTo-Json

# rename file to download name
Rename-Item -Path $ZipFilePath -NewName $DownloadName