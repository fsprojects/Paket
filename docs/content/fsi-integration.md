# F# Interactive Integration

F# Interactive v5 and above ships with [extensions mechanism](https://github.com/fsharp/fslang-design/blob/dcc45b557f713a9aee75d85eae7555d41cd1cb0b/tooling/FST-1027-fsi-references.md).

FSharp.DependencyManager.Paket implements this extension mechanism to hook same power as [paket.dependencies](dependencies-file.html) file right inside .fsx scripts.

## Making sure paket.exe is found

The extension is searching for paket.exe in the folder hierarchy containing the script, checking for a `.paket` folder containing `paket.exe`.

It falls back to those user directories if it can't find it in the parent folders:

*  `~/.paket/paket.exe`
*  `~/.dotnet/tools/paket.exe`
*  `~/.nuget/packages/paket/{most-recent-version}/tools/paket.exe`

## Install the extension

You have two choices to deploy the assembly:

### Copying the assembly next to the host process

Install the FSharp.DependencyManager.Paket.dll aside of your F# Interactive binaries or the binary of the host process loading the extensions.

In general, the process will explicitly list the folders it is currently checking for extensions:

```
error FS3216: Package manager key 'paket' was not registered in [C:\Program Files\dotnet\sdk\5.0.100\FSharp; C:\Program Files\dotnet\sdk\5.0.100\FSharp\], []. Currently registered: nuget
```

The locations to copy `FSharp.DependencyManager.Paket.dll`

Dotnet SDK:
 - Windows  
   `C:\Program Files\dotnet\sdk\<version number>\FSharp`
 - macOS  
   `/usr/local/share/dotnet/sdk/<version number>/FSharp`
 - Linux  
   `/home/user/dotnet/sdk/<version number>/FSharp`

Visual Studio:
 - For Editor Support  
   `C:\Program Files\Microsoft Visual Studio\<version number>\Community\Common7\IDE\CommonExtensions\Microsoft\FSharp`
 - For Fsi Window Support  
   `C:\Program Files\Microsoft Visual Studio\<version number>\Community\Common7\IDE\CommonExtensions\Microsoft\FSharp\Tools`


### Passing the folder of the extension as --compilertool flag

Use the `--compilertool` flag when invoking F# Interactive or refer to the documentation of the host process.

For example on windows:

```
--compilertool:"c:\users\username\.nuget\packages\fsharp.dependencymanager.paket\6.0.0-alpha055\lib\netstandard2.0"
```

For example on unix:

```
--compilertool:"~/.nuget/packages/fsharp.dependencymanager.paket/6.0.0-alpha055/lib/netstandard2.0"
```

## Usage in scripts

Using it involves the `#r` directive with `paket` extension.

Here is an example importing FSharp.Data library:

```fsharp
#r "paket: nuget FSharp.Data"

open FSharp.Data
// do cool stuff
```

It is also possible to use github dependencies to import individual files:

```fsharp
#r "paket: github fsharp/FAKE src/legacy/FakeLib/Globbing/Globbing.fs"
#load @"fsharp\FAKE\src\legacy\FakeLib\Globbing\Globbing.fs"
 
let f = Fake.Globbing.search
```

## Usage in FSharp.Compiler.Service

Same remarks as for installation section above, you'd either make sure the assembly is deployed aside the host process binary, or you'd pass `--compilertool` flag when building the project options (through `GetProjectOptionsFromScript`).
