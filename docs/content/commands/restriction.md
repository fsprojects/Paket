# paket restriction

Determine how a restriction formula is interpreted by paket.

```sh
paket restriction <restriction formula>

RESTRICTION:

    <restriction formula> A paket formula representing a restriction

OPTIONS:

    --silent, -s          suppress console output
    --verbose, -v         print detailed information to the console
    --log-file <path>     print output to a file
    --from-bootstrapper   call coming from the '--run' feature of the bootstrapper
    --help                display this list of options.
```

> Note if your formula contains spaces (which it will most likely) you need to escape with quotes

## Example

```
$ ./paket.exe restriction "|| (== netcoreapp2.0) (&& (== netstandard2.0) (>= netcoreapp2.0))"
Paket version 5.145.1
Restriction: || (== netcoreapp2.0) (&& (== netstandard2.0) (>= netcoreapp2.0))
Simplified: || (== netcoreapp2.0) (&& (== netstandard2.0) (>= netcoreapp2.0))
Frameworks: [
   netcoreapp2.0
]
Performance:
 - Runtime: 763 milliseconds
```

