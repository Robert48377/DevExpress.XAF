param(
    $Branch,
    $SourcePath = "$PSScriptRoot\..\..",
    $GitHubUserName,
    $Pass,
    $DXApiFeed,
    $artifactstagingdirectory
)
$ErrorActionPreference = "Stop"
& "$SourcePath\go.ps1" -InstallModules

$bArgs=@{
    msbuild="C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\MSBuild.exe"
    packageSources="$(Get-PackageFeed -Xpand);$DxApiFeed"
}
& $SourcePath\go.ps1 @bArgs

"$SourePath\Bin\Nupkg","$SourePath\Bin\Nuspec"|ForEach-Object{
    Get-ChildItem $_ -Recurse |ForEach-Object{
        Copy-Item $_.FullName -Destination $artifactstagingdirectory
    }
}
