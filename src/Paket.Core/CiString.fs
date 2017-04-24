namespace Paket

open System
open System.Collections.Generic

type CiString(value) =
    override this.ToString() = value
    override this.Equals(other) =
        match other with
        | :? CiString as o -> (this :> IEquatable<CiString>).Equals(o)
        | _ -> invalidArg "other" "cannot compare to non-CiString"
    override this.GetHashCode() = StringComparer.OrdinalIgnoreCase.GetHashCode(this.ToString())
    interface IEquatable<CiString> with
        member this.Equals(other) = StringComparer.OrdinalIgnoreCase.Equals(this.ToString(), other.ToString())
    interface IComparable<CiString> with
        member this.CompareTo(other) = StringComparer.OrdinalIgnoreCase.Compare(this.ToString(), other.ToString())
    interface IComparable with
        member this.CompareTo(other) =
            match other with
            | :? CiString as o -> (this :> IComparable<CiString>).CompareTo(o)
            | _ -> invalidArg "other" "cannot compare to non-CiString"
