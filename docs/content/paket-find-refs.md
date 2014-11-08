# paket find-refs

Finds all [`paket.references` files](references-file.html) that contain the given Nuget packages.

    [lang=batchfile]
    $ paket find-refs --packages PACKAGENAME1 PACKAGENAME1 ...

## Sample
.src/Paket/paket.references contains:

	UnionArgParser
	FSharp.Core.Microsoft.Signed

.src/Paket.Core/paket.references contains:

	Newtonsoft.Json
	DotNetZip
	FSharp.Core.Microsoft.Signed

Now we run `paket find-refs --packages DotNetZip FSharp.Core.Microsoft.Signed`:
	
	DotNetZip
	.src/Paket.Core/paket.references

	FSharp.Core.Microsoft.Signed
	.src/Paket.Core/paket.references
	.src/Paket/paket.references