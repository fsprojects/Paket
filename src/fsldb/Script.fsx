#load "Library.fs"
open fsldb

let test () =
  let m,n = C.leveldb_major_version(),C.leveldb_minor_version()
  printfn "LevelDB v%i.%i" m n

test ()
