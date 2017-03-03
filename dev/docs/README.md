# miscellanous info


## msbuild 15 nuget restore

`dotnet restore` is the same as `dotnet msbuild /t:Restore`

the `Restore` target is in file '%dotnetsdk%\sdk\1.0.0-preview4-004233\NuGet.targets`

The `dotnet restore -v n` doesnt give lots of info, so running directory the target with `dotnet msbuild`
can give more info.
Like `dotnet msbuild src\console\console.csproj /t:Restore /v:d`

The `--packages` arguments is `/p:RestorePackagesPath=path\to\packages\dir\to\use`

The `sha512` string are `sha512 >> base64`

## high level

`dotnet restore` -> `obj\project.assets.json` and packages downloaded on local cache
`dotnet build` and `dotnet publish` use obj\project.assets.json

issues:

- for local cache nuget use `pkg/ver` structure, paket use `pkg`
- what hooks?
- same nuget targets used also for .net full, UWP, etc

## Possibilità

### A - use Nuget target but replace Nuget msbuild tasks, like RestoreTask

Setting `$(RestoreTaskAssemblyFile)` to load another assembly 

File `%dotnetsdk%\sdk\1.0.0-preview4-004233\NuGet.targets`:

```
  <UsingTask TaskName="NuGet.Build.Tasks.RestoreTask" AssemblyFile="$(RestoreTaskAssemblyFile)" />
  <UsingTask TaskName="NuGet.Build.Tasks.WriteRestoreGraphTask" AssemblyFile="$(RestoreTaskAssemblyFile)" />
  <UsingTask TaskName="NuGet.Build.Tasks.GetRestoreProjectJsonPathTask" AssemblyFile="$(RestoreTaskAssemblyFile)" />
  <UsingTask TaskName="NuGet.Build.Tasks.GetRestoreProjectReferencesTask" AssemblyFile="$(RestoreTaskAssemblyFile)" />
  <UsingTask TaskName="NuGet.Build.Tasks.GetRestorePackageReferencesTask" AssemblyFile="$(RestoreTaskAssemblyFile)" />
  <UsingTask TaskName="NuGet.Build.Tasks.GetRestoreDotnetCliToolsTask" AssemblyFile="$(RestoreTaskAssemblyFile)" />
```

PRO: targets are the same
CONS: task interface instability
UNK: how to reuse partially the tasks

Interesting is `GetRestorePackageReferencesTask`

### B - replace `dotnet restore` with `dotnet paket` and write `obj/project.assets.json`

PRO: total control
CONS: all logic of nuget restore target is lost

### C - prepare a packages directory

CONS: copies
PRO: reuse all logic
CONS: nuget and paket have different package version logic?

- override somewhere the property to fix where nuget expect the local path

### D - fix project.assets.json

```
    "System.Runtime.InteropServices/4.3.0": {
      "sha512": "uv1ynXqiMK8mp1GM3jDqPCFN66eJ5w5XNomaK2XD+TuCroNTLFGeZ+WCmBMcBDyTFKou3P6cR6J/QsaqDp7fGQ==",
      "type": "package",
      "path": "system.runtime.interopservices/4.3.0",
      "files": [
```
Fix the `"path"` from pkg/version (nuget) to pkg (nuget), so 
like `"system.runtime.interopservices/4.3.0"` -> `"system.runtime.interopservices"`

The packages directory is in

```
  "packageFolders": {
    "e:\\github\\Paket\\dev\\packages2": {}
  }
```

PRO project.assets.json is used by the rest (build/publish), so logic is reused

