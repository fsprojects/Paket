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

    
    let RemoveNugetPackagesFile(solutionName: string) = 
        let slnContent = ResizeArray( File.ReadAllLines solutionName )
        match slnContent |> Seq.tryFindIndex (fun line -> line.Contains(".nuget\\packages.config")) with
        | Some(index) -> 
            slnContent.RemoveAt(index)
            removeNugetSlnFolderIfEmpty(slnContent)
            File.WriteAllLines(solutionName, slnContent)
        | None -> ()        

    let RemoveNugetTargetsFile(solutionName: string) =
        let slnContent = ResizeArray( File.ReadAllLines solutionName )
        match slnContent |> Seq.tryFindIndex (fun line -> line.Contains(".nuget\\nuget.targets")) with
        | Some(index) -> 
            slnContent.RemoveAt(index)
            removeNugetSlnFolderIfEmpty(slnContent)
            File.WriteAllLines(solutionName, slnContent)
        | None -> ()        