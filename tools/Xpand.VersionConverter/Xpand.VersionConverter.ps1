using namespace System
using namespace System.IO
using namespace System.IO.Compression
using namespace System.Reflection
using namespace System.Text.RegularExpressions
using namespace Mono.Cecil
using namespace Mono.Cecil.pdb
param(
    [parameter(Mandatory)]
    [string]$projectFile,
    [parameter(Mandatory)]
    [string]$targetPath,
    [string]$referenceFilter = "DevExpress*",
    [string]$assemblyFilter = "Xpand.XAF.*"
)
 
$ErrorActionPreference = "Stop"
set-location $targetPath
# $VerbosePreference="Continue"
Write-Verbose "Running Version Converter on project $projectFile with target $targetPath"
$projectFileInfo = Get-Item $projectFile
[xml]$csproj = Get-Content $projectFileInfo.FullName
$references = $csproj.Project.ItemGroup.Reference
$dxReferences = $references | Where-Object { $_.Include -like "$referenceFilter" }
$root = $PSScriptRoot
"Loading Mono.Cecil"
$monoPath = "$root\mono.cecil.0.10.3\lib\net40"
if (!(Test-Path "$monoPath\Mono.Cecil.dll")) {
    $client = New-Object System.Net.WebClient
    $client.DownloadFile("https://www.nuget.org/api/v2/package/Mono.Cecil/0.10.3", "$root\mono.cecil.0.10.3.zip")
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [ZipFile]::ExtractToDirectory("$root\mono.cecil.0.10.3.zip", "$root\mono.cecil.0.10.3")
}

[Assembly]::Load([File]::ReadAllBytes("$monoPath\Mono.Cecil.dll")) | Out-Null
[Assembly]::Load([File]::ReadAllBytes("$monoPath\Mono.Cecil.pdb.dll")) | Out-Null
Add-Type @"
using Mono.Cecil;
public class MyDefaultAssemblyResolver : DefaultAssemblyResolver{

    public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters){
        try{
            return base.Resolve(name, parameters);
        }
        catch (AssemblyResolutionException){
            var assemblyDefinition = AssemblyDefinition.ReadAssembly(string.Format(@"$targetPath\{0}.dll", name.Name));
            return assemblyDefinition;
        }
    }
}
"@ -ReferencedAssemblies @("$monoPath\Mono.Cecil.dll")
$devExpressAssemblyName = Invoke-Command {

    Write-Verbose "Finding DX assembly name"
    $dxAssemblyPath = Get-ChildItem $targetPath "$referenceFilter*.dll" | Select-Object -First 1
    if ($dxAssemblyPath) {
        $dxAssembly = [AssemblyDefinition]::ReadAssembly($dxAssemblyPath.FullName)
        Write-Verbose "$($dxAssembly.Name.Name) found from $($dxAssemblyPath.FullName)"
        $dxAssembly.Name
    }
    else {
        $name = ($dxReferences | Where-Object { $_.Include -like "*Version*" } | Select-Object -First 1).Include
        New-Object System.Reflection.AssemblyName($name)
    }
} | Select-Object -last 1
if (!$devExpressAssemblyName) {
    throw "Cannot find $referenceFilter version in $($projectFileInfo.Name)"
}

$references | Where-Object { $_.Include -like $assemblyFilter } | ForEach-Object {
    "$targetPath\$([Path]::GetFileName($_.HintPath))", "$($projectFileInfo.DirectoryName)\$($_.HintPath)" | ForEach-Object {
        if (Test-Path $_) {
            $modulePath = (Get-Item $_).FullName
            $readerParams = New-Object ReaderParameters
            $readerParams.ReadWrite = $true
            $readerParams.AssemblyResolver=New-Object MyDefaultAssemblyResolver
            $readerParams.SymbolReaderProvider = New-Object PdbReaderProvider
            $readerParams.ReadSymbols = $true
            $moduleAssembly = [AssemblyDefinition]::ReadAssembly($modulePath, $readerParams)
            Write-Verbose "Checking $modulePath references.."
            $moduleAssembly.MainModule.AssemblyReferences.ToArray() | Write-Verbose
            $moduleAssembly.MainModule.AssemblyReferences.ToArray() | Where-Object { $_.FullName -like $referenceFilter } | ForEach-Object {
                $nowReference = $_
                Write-Verbose "Checking $_ reference..."
                if ($nowReference.Version -ne $devExpressAssemblyName.Version) {
                    $moduleAssembly.MainModule.AssemblyReferences.Remove($nowReference)
                    $newMinor = "$($devExpressAssemblyName.Version.Major).$($devExpressAssemblyName.Version.Minor)"
                    $newName = [Regex]::Replace($nowReference.Name, ".(v[\d]{2}\.\d)", ".v$newMinor")
                    $regex = New-Object Regex("PublicKeyToken=([\w]*)")
                    $token = $regex.Match($nowReference).Groups[1].Value
                    $regex = New-Object Regex("Culture=([\w]*)")
                    $culture = $regex.Match($nowReference).Groups[1].Value
                    $newReference = [AssemblyNameReference]::Parse("$newName, Version=$($devExpressAssemblyName.Version), Culture=$culture, PublicKeyToken=$token")
                    $moduleAssembly.MainModule.AssemblyReferences.Add($newreference)
                    $moduleAssembly.MainModule.Types | ForEach-Object {
                        $moduleAssembly.MainModule.GetTypeReferences() | Where-Object { $_.Scope -eq $nowReference } |ForEach-Object { 
                            $_.Scope = $newReference 
                        }
                    }
                    Write-Verbose "$($_.Name) version changed from $($_.Version) to $($devExpressAssemblyName.Version)" 
                }
                else {
                    Write-Verbose "Versions ($($nowReference.Version)) matched nothing to do."
                }
            }
            $writeParams = New-Object WriterParameters
            $writeParams.WriteSymbols=$true
            $f = New-Object FileStream("$root\Xpand.snk", [FileMode]::Open)
            $writeParams.StrongNameKeyPair = New-Object System.Reflection.StrongNameKeyPair ( $f)
            $moduleAssembly.Write($writeParams)
            $f.Dispose()
            $moduleAssembly.Dispose()   
        }
    }
}
