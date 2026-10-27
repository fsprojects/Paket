> Note if your formula contains spaces (which it will most likely) you need to escape with quotes

## Example

```text
$ dotnet paket restriction "|| (== netcoreapp2.0) (&& (== netstandard2.0) (>= netcoreapp2.0))"
Paket version 12.0.0
Restriction: || (== netcoreapp2.0) (&& (== netstandard2.0) (>= netcoreapp2.0))
Simplified: || (== netcoreapp2.0) (&& (== netstandard2.0) (>= netcoreapp2.0))
Frameworks: [
   netcoreapp2.0
]
Total time taken: 0 milliseconds
```

