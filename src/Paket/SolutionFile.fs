namespace Paket

open System.IO

/// Contains methods to read and manipulate solution files.
module SolutionFile =
    let private removeNugetSlnFolderIfEmpty(slnContent: ResizeArray<string>) =
        match slnContent |> Seq.tryFindIndex (fun line -> 
                line.StartsWith("""Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}")""") && line.Contains(".nuget")) with
        | Some(index) -> 
            if slnContent.[index+1].Contains("ProjectSection(SolutionItems)") &&
               slnContent.[index+2].Contains("EndProjectSection") &&
               slnContent.[index+3].Contains("EndProject")
            then 
                slnContent.RemoveRange(index, 4)
        | None -> ()

    let RemoveNugetEntries(solutionName: string) =
        let slnContent = ResizeArray( File.ReadAllLines solutionName )
        let mutable modified = false
        match slnContent |> Seq.tryFindIndex (fun line -> line.Contains(".nuget\\nuget.targets")) with
        | Some(index) -> slnContent.RemoveAt(index); modified <- true
        | None -> ()        
        match slnContent |> Seq.tryFindIndex (fun line -> line.Contains(".nuget\\packages.config")) with
        | Some(index) -> slnContent.RemoveAt(index); modified <- true
        | None -> ()

        removeNugetSlnFolderIfEmpty(slnContent)
        if modified then File.WriteAllLines(solutionName, slnContent)