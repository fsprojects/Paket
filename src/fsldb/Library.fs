namespace fsldb

open System

module C =
  open System.Runtime.InteropServices
  
  // Return the major version number for this release.
  [<DllImport("libleveldb",CallingConvention=CallingConvention.Cdecl)>]
  extern int leveldb_major_version()

  // Return the minor version number for this release.
  [<DllImport("libleveldb",CallingConvention=CallingConvention.Cdecl)>]
  extern int leveldb_minor_version()
