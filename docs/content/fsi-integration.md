# F# Interactive Integration

F# Interactive v5 and above ships with [extensions mechanism](https://github.com/fsharp/fslang-design/blob/dcc45b557f713a9aee75d85eae7555d41cd1cb0b/tooling/FST-1027-fsi-references.md).

FSharp.DependencyManager.Paket implements this extension mechanism to hook same power as [paket.dependencies](dependencies-file.html) file right inside .fsx scripts.

## Installation

You have two choices:

### Deploying the assembly

Install the FSharp.DependencyManager.Paket.dll aside of your F# Interactive binaries or the binary of the host process loading the extensions.

### Passing the folder of the extension as --compilertool flag

Use the `--compilertool` flag when invoking F# Interactive or refer to the documentation of the host process.

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
