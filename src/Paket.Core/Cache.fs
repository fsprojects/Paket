namespace Paket

open System
open System.IO

type CacheType = 
    | AllVersion
    | CurrentVersiononly

type Cache = 
    { Location : string
      CacheType : CacheType }