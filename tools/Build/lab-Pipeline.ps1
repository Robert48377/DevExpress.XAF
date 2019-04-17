param(
    $Branch="lab",
    $SourcePath = "$PSScriptRoot\..\..",
    $GitHubUserName="apobekiaris",
    $Pass=$env:GithubPass,
    $DXApiFeed
)
$ErrorActionPreference = "Stop"
& "$SourcePath\go.ps1" -InstallModules
$packageSource = Get-XPackageFeed -Xpand

$localPackages = Get-ChildItem "$sourcePath\src\Modules" "*.csproj" -Recurse|Invoke-Parallel -script {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($_.FullName)
    $localVersion = Get-XpandVersion -XpandPath $_.DirectoryName -module $name
    $nextVersion = Get-XpandVersion -Next -module $name
    if (!$nextVersion){
        $nextVersion=$localVersion
    }
    [PSCustomObject]@{
        Name         = $name
        NextVersion  = $nextversion
        LocalVersion = $localVersion
    }
}
Write-Host "localPackages:" -f blue
Write-Host $localPackages
$localPackages|Write-Output
$publishedPackages = & (Get-XNugetPath) list Xpand.XAF.Modules -source $packageSource| ConvertTo-PackageObject -LatestVersion| Where-Object {$_.Name -like "Xpand.XAF*"}| ForEach-Object {
    $publishedName = $_.Name
    $localPackages|Where-Object {$_.Name -eq $publishedName}
}
Write-Host "publishedPackages:" -f blue
$publishedPackages
$newPackages = $localPackages|Where-Object {!(($publishedPackages|Select-Object -ExpandProperty Name) -contains $_.Name) }|ForEach-Object {
    $localVersion = New-Object System.Version($_.LocalVersion)
    $nextVersion=New-Object System.Version($localVersion.Major, $localVersion.Minor, $localVersion.Build)
    [PSCustomObject]@{
        Name        = $_.Name
        NextVersion = $nextVersion
        LocalVersion =$localVersion
    }
}
Write-host "newPackages:" -f blue
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
$yArgs.Packages|Write-Output
Update-NugetProjectVersion @yArgs 

$bArgs=@{
    msbuild="C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\MSBuild.exe"
    packageSources="$(Get-PackageFeed -Xpand);$DxApiFeed"
}
& $SourcePath\go.ps1 @bArgs

