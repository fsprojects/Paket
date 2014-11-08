#r @"..\..\bin\Paket.Core.dll"

open Paket

// locate the paket.dependencies file
Dependencies.Locate(__SOURCE_DIRECTORY__)
// [fsi:found: D:\code\Paket\docs\content\paket.dependencies]

Dependencies.Add "FSharp.Data"
// [fsi:found: Adding FSharp.Data  to D:\code\Paket\docs\content\paket.dependencies]
// [fsi:found: Resolving packages:]
// [fsi:found:   - fetching versions for FSharp.Data]
// [fsi:found:     - exploring FSharp.Data 2.1.0]
// [fsi:found:   - fetching versions for Zlib.Portable]
// [fsi:found:     - exploring Zlib.Portable 1.10.0]
// [fsi:found: Locked version resolutions written to D:\code\Paket\docs\content\paket.lock]
// [fsi:found: Zlib.Portable 1.10.0 unzipped to D:\code\Paket\docs\content\packages\Zlib.Portable]
// [fsi:found: FSharp.Data 2.1.0 unzipped to D:\code\Paket\docs\content\packages\FSharp.Data]
// [fsi:found: Dependencies files saved to D:\code\Paket\docs\content\paket.dependencies]

Dependencies.GetInstalledVersion "FAKE"

Dependencies.Remove "FSharp.Data"