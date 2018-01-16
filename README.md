# WoWUE4CmdImport
Command-line tool to batch-import assets to Unreal Engine without opening the Editor.

# Notes

* Make sure you have enough disk space when importing a lot of assets as the Derived Data Cache can get really big!
* Solution was created in VS 2017.

# Usage

```
class Options
{
    [Option('u', "ue4", Required = true,
      HelpText = "Path to UE4 Win64 Binaries.")]
    public String UE4Path { get; set; }

    [Option('p', "project", Required = true,
      HelpText = "Path to UE4 project.")]
    public String ProjectPath { get; set; }

    [Option('s', "source", Required = true,
      HelpText = "Path to folder with source files.")]
    public String SourceFilesPath { get; set; }

    [Option('i', "importto", Required = true,
      HelpText = "UE4 path to where files should get imported to")]
    public String ImportedFilesPath { get; set; }

    [Option('e', "extension", Required = true,
      HelpText = "File extensions to import.")]
    public String FileExtensionToSearch { get; set; }        

    [Option('c', "count", Required = true,
      HelpText = "Amount of assets to process at once.")]
    public int AmountOfAssetsToProcess { get; set; }

    [Option('j', "json", Required = true,
      HelpText = "Path to Import Settings JSON file.")]
    public string PathToImportJSON { get; set; }
}
```

# Example

Run from Powershell

```
.\WoWUE4CmdImport.exe -u "C:\Program Files\Epic Games\UE_4.18\Engine\Binaries\Win64" -p "C:\Users\Kia\Desktop\MyProject\MyProject.uproject" -s "D:\MyAssets" -i "Maps/MyMap/Textures/WMOs" -e "svg" -c 3 -j "C:\Git\WoWUE4CmdImport\import_texture.json"
```

# Example JSON
```
 {
     "ImportGroups": [
         {
             "FileNames": [],
             "bReplaceExisting": "true", 
             "DestinationPath": "", 
             "FactoryName": "FbxFactory", 
             "ImportSettings": {
                 "bImportMesh": 1, 
                 "bConvertSceneUnit": 1, 
                 "bConvertScene": 0,
                 "bCombineMeshes": 1, 
                 "bImportTextures": 0, 
                 "bImportMaterials": 0, 
                 "AnimSequenceImportData": {}, 
                 "SkeletalMeshImportData": {},
                 "TextureImportData": {}, 
                 "StaticMeshImportData": {
                     "bRemoveDegenerates": 1, 
                     "bAutoGenerateCollision": 1
                 }
             }
         }
     ]
 }
 ```
 
 Parameters are the same as the ones you can find in the UE4 Factory documentation which can be found here: https://docs.unrealengine.com/latest/INT/API/Editor/UnrealEd/Factories/

