


## msbuild 15 nuget restore

`dotnet restore` is the same as `dotnet msbuild /t:Restore`

the `Restore` target is in file '%dotnetsdk%\sdk\1.0.0-preview4-004233\NuGet.targets`

The `dotnet restore -v n` doesnt give lots of info, so running directory the target with `dotnet msbuild`
can give more info.
Like `dotnet msbuild src\console\console.csproj /t:Restore /v:d`

The `--packages` arguments is `/p:RestorePackagesPath=path\to\packages\dir\to\use`


## Possibili extensioni

### replacing Nuget msbuild tasks, like RestoreTask

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

Interested is `GetRestorePackageReferencesTask`

### replace `dotnet restore`, and write `obj/project.assets.json`



