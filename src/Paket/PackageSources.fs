module Paket.PackageSources

type NugetSource = 
    { Url : string
      Auth : Auth option }

/// Represents the package source type.
type PackageSource =
| Nuget of NugetSource
| LocalNuget of string
    override this.ToString() =
        match this with
        | Nuget source -> source.Url
        | LocalNuget path -> path

    static member Parse(source,auth) = 
        match System.Uri.TryCreate(source, System.UriKind.Absolute) with
        | true, uri -> if uri.Scheme = System.Uri.UriSchemeFile then LocalNuget(source) else Nuget({ Url = source; Auth = auth })
        | _ -> failwith "unable to parse package source: %s" source

    static member NugetSource url = Nuget { Url = url; Auth = None }

let DefaultNugetSource = PackageSource.NugetSource Constants.DefaultNugetStream