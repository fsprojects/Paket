/// Contains methods to read and manipulate project files.
module Paket.ProjectFile

open System.Xml
open System.IO

/// Reads the packages file which sits next to the projectFile.
let LoadReferencedPackages (projectFileName:string) = 
    let fi = FileInfo(projectFileName)
    let packagesDef = FileInfo(fi.Directory.FullName + Path.DirectorySeparatorChar.ToString() + "packages")
    if packagesDef.Exists then File.ReadAllLines(packagesDef.FullName) else [||]

let getProject (text:string) =
    let xmlDocument = new XmlDocument()
    xmlDocument.LoadXml text
    xmlDocument