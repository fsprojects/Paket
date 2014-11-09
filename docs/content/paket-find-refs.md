# paket find-refs

Finds all project files that have the given NuGet packages installed.

    [lang=batchfile]
    $ paket find-refs --packages PACKAGENAME1 PACKAGENAME1 ...

## Sample
*.src/Paket/paket.references* contains:

	UnionArgParser
	FSharp.Core.Microsoft.Signed

*.src/Paket.Core/paket.references* contains:

	Newtonsoft.Json
	DotNetZip
	FSharp.Core.Microsoft.Signed

Now we run `paket find-refs --packages DotNetZip FSharp.Core.Microsoft.Signed`:
	
	DotNetZip
	.src/Paket.Core/Paket.Core.fsproj

	FSharp.Core.Microsoft.Signed
	.src/Paket.Core/Paket.Core.fsproj
	.src/Paket/Paket.fsproj