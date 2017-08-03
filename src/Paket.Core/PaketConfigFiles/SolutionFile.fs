namespace Paket

open System.IO
open Pri.LongPath
open Logging
open System

/// Contains methods to read and manipulate solution files.
type SolutionFile(fileName: string) =
   
    let originalContent = File.ReadAllLines fileName |> Array.toList
    let originalEncoding = getFileEncoding fileName
    let content = ResizeArray( originalContent )
    
    let removeNugetSlnFolderIfEmpty() =
        match content |> Seq.tryFindIndex (fun line -> 
                line.StartsWith(sprintf "Project(\"{%s}\")" Constants.SolutionFolderProjectGuid) && line.Contains(".nuget")) with
        | Some(index) -> 
            if content.[index+1].Contains("ProjectSection(SolutionItems)") &&
               content.[index+2].Contains("EndProjectSection") &&
               content.[index+3].Contains("EndProject")
            then 
                content.RemoveRange(index, 4)
        | None -> ()

    let projectSectionLines = 
        [ "\tProjectSection(SolutionItems) = preProject";
          "\tEndProjectSection" ]

    let addPaketFolder () = 
        let lines = 
              [sprintf "Project(\"{%s}\") = \".paket\", \".paket\", \"{%s}\"" Constants.SolutionFolderProjectGuid (Guid.NewGuid().ToString("D").ToUpper())]
            @ projectSectionLines
            @ [ "EndProject" ]

        let index = 
            match content |> Seq.tryFindIndex (fun line -> line.StartsWith("Project")) with
            // insert before the first project in a solution
            | Some index -> index
            | None -> 
                // there are no project in solution
                match content |> Seq.tryFindIndex (fun line -> line.StartsWith("Global")) with
                // insert before ``global`` entry
                | Some index -> index
                // cannot find ``global`` entry, just append to the end of file
                | None -> content.Count

        content.InsertRange(index, lines)
        index, lines |> List.length

    let addPaketFiles(paketProjectIndex, length, dependenciesFile, lockFile) = 
        let projectLines = content.GetRange(paketProjectIndex, length)

        // This here is ad-hoc hacky solution to sln ambiguity.
        // The problen is that the .sln file may or may not contain a "ProjectSection" within the ".paket" "Project" (see #2161 and #2512).
        // When the "ProjectSection" is not there, we need to add it, and then add new files within it.
        // A better solution would be to have a proper parser for the .sln file, but that would take too long, and would probably be a waste anyway.
        let firstLineInPaketProject = if paketProjectIndex < content.Count-1 then content.[paketProjectIndex+1] else ""
        if not (firstLineInPaketProject.Contains "ProjectSection") then
            if firstLineInPaketProject.Contains "EndProject" then 
                // The "Project" is empty => insert "ProjectSection"
                content.InsertRange( paketProjectIndex+1, projectSectionLines )
            else 
                // The "Project" is not empty, but also doesn't contain a "ProjectSection" => no idea what this means, 
                // so just fall back to old behavior and insert new lines at +2 after "Project" start.
                ()
        
        let add s =
            if not <| Seq.exists (fun line -> line = s) projectLines 
            then content.Insert(paketProjectIndex + 2, s)

        Option.iter (fun lockFile -> 
            add (sprintf"		%s = %s" lockFile lockFile)) lockFile

        add (sprintf   "		%s = %s" dependenciesFile dependenciesFile)
        

    member __.FileName = fileName

    member __.RemoveNuGetEntries() =
        for file in ["nuget.targets"; Constants.PackagesConfigFile; "nuget.exe"; "nuget.config"] do
            match content |> Seq.tryFindIndex (fun line -> String.containsIgnoreCase (sprintf ".nuget\\%s" file)line) with
            | Some(index) -> content.RemoveAt(index)
            | None -> ()            
        
        removeNugetSlnFolderIfEmpty()

    member __.AddPaketFolder(dependenciesFile, lockFile) =
        let paketProjectIndex, length = 
            match content |> Seq.tryFindIndex (fun line ->
                line.StartsWith(sprintf   "Project(\"{%s}\") = \".paket\", \".paket\"" Constants.SolutionFolderProjectGuid)) with
            | Some paketProjectIndex -> 
                let length = content |> Seq.skip paketProjectIndex |> Seq.findIndex (fun line -> line = "EndProject")
                paketProjectIndex, length
            | None -> addPaketFolder()       
        
        addPaketFiles(paketProjectIndex, length, dependenciesFile, lockFile)

    member __.Save() =
        if content |> Seq.toList <> originalContent 
        then 
            File.WriteAllLines(fileName, content, originalEncoding)
            tracefn "Solution %s changed" fileName