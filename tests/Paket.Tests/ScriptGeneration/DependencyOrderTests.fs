module Paket.DependencyOrderTests

open NUnit.Framework
open FsUnit
open Mono.Cecil
open Paket

let private makeAssembly name (references: AssemblyDefinition list) selfReference =
    let asm = AssemblyDefinition.CreateAssembly(AssemblyNameDefinition(name, System.Version(1, 0, 0, 0)), name, ModuleKind.Dll)
    for r in references do
        asm.MainModule.AssemblyReferences.Add(AssemblyNameReference(r.Name.Name, r.Name.Version))
    if selfReference then
        asm.MainModule.AssemblyReferences.Add(AssemblyNameReference(name, System.Version(1, 0, 0, 0)))
    asm

let private names = List.map (fun (a: AssemblyDefinition) -> a.Name.Name)

[<Test>]
let ``getDllOrder ignores assembly self-references`` () =
    let core = makeAssembly "FSharp.Core" [] false
    let reactive = makeAssembly "System.Reactive" [] true
    let result = DependencyOrder.getDllOrder [ reactive; core ]
    names result |> List.sort |> should equal [ "FSharp.Core"; "System.Reactive" ]

[<Test>]
let ``getDllOrder still orders dependencies first`` () =
    let a = makeAssembly "A" [] true
    let b = makeAssembly "B" [ a ] false
    names (DependencyOrder.getDllOrder [ b; a ]) |> should equal [ "A"; "B" ]

[<Test>]
let ``getPackageOrderGeneric keeps items it cannot order`` () =
    let deps = dict [ "a", [ "b" ]; "b", [ "a" ]; "c", [] ]
    let result = DependencyOrder.getPackageOrderGeneric id (fun p -> deps.[p] :> seq<_>) [ "a"; "b"; "c" ]
    result |> List.sort |> should equal [ "a"; "b"; "c" ]
    result.Head |> should equal "c"
