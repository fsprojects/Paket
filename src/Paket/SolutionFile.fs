namespace Paket

open System.IO

/// Contains methods to read and manipulate solution files.
type SolutionFile(fileName: string) =
    let content = ResizeArray( File.ReadAllLines fileName )

    let removeNugetFolderIfEmpty() =
        match content |> Seq.tryFindIndex (fun line -> 
                line.StartsWith("""Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}")""") && line.Contains(".nuget")) with
        | Some(index) -> 
            if content.[index+1].Contains("ProjectSection(SolutionItems)") &&
               content.[index+2].Contains("EndProjectSection") &&
               content.[index+3].Contains("EndProject")
            then 
                content.RemoveRange(index, 4)
        | None -> ()

    member __.RemoveNugetPackagesFile() =
        match content |> Seq.tryFindIndex (fun line -> line.Contains(".nuget\packages.config")) with
        | Some(index) -> 
            content.RemoveAt(index)
            removeNugetFolderIfEmpty()
        | None -> ()
        
    member __.Save() = File.WriteAllLines(fileName, content)