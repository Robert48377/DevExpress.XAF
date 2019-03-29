param(
    $Branch,
    $SourcePath = "$PSScriptRoot\..\..",
    $GitHubUserName,
    $Pass,
    $DXApiFeed
)
& "$SourcePath\go.ps1" -InstallModules
$ErrorActionPreference = "Stop"
# $VerbosePreference = "continue"
$packageSource = Get-XPackageFeed -Xpand

$localPackages = Get-ChildItem "$sourcePath\src\Modules" "*.csproj" -Recurse|ForEach-Object {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($_.FullName)
    $nextVersion = Get-XpandVersion -Next -module $name
    $localVersion = Get-XpandVersion -XpandPath $_.DirectoryName -module $name
    [PSCustomObject]@{
        Name         = $name
        NextVersion  = $nextversion
        LocalVersion = $localVersion
    }
}

$publishedPackages = & (Get-XNugetPath) list Xpand.XAF.Modules -source $packageSource| ConvertTo-PackageObject -LatestVersion| Where-Object {$_.Name -like "Xpand.XAF*"}| ForEach-Object {
    $publishedName = $_.Name
    $localPackages|Where-Object {$_.Name -eq $publishedName}
}

$newPackages = $localPackages|Where-Object {!(($publishedPackages|Select-Object -ExpandProperty Name) -contains $_.Name) }|ForEach-Object {
    $localVersion = New-Object System.Version($_.LocalVersion)
    [PSCustomObject]@{
        Name        = $_.Name
        NextVersion = New-Object System.Version($localVersion.Major, $localVersion.Minor, $localVersion.Build)
    }
}
$newPackages
                                                           
$yArgs = @{
    Owner        = $GitHubUserName
    Organization = "eXpandFramework"
    Repository   = "DevExpress.XAF"
    Branch       = $Branch
    Pass         = $Pass
    Packages     = ($publishedPackages + $newPackages)
    SourcePath   = $SourcePath
}
$yArgs|Write-Output
Update-NugetProjectVersion @yArgs 

$bArgs=@{
    msbuild="C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\MSBuild.exe"
    packageSources="$(Get-PackageFeed -Xpand);$DxApiFeed"
}
& $SourcePath\go.ps1 @bArgs

