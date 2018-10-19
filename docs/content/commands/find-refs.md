## Example

* `src/Paket/paket.references` contains:

```text
UnionArgParser
FSharp.Core
```

* `src/Paket.Core/paket.references` contains:

```text
Newtonsoft.Json
DotNetZip
FSharp.Core
```

Now we run:

```sh
$ paket find-refs DotNetZip FSharp.Core
Paket version 5.0.0
Main DotNetZip
src/Paket.Core/Paket.Core.fsproj

Main FSharp.Core
src/Paket.Core/Paket.Core.fsproj
src/Paket/Paket.fsproj

Performance:
 - Runtime: 1 second
```
