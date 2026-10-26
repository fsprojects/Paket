[<NUnit.Framework.TestFixture>]
[<NUnit.Framework.Category "Script Generation">]
module Paket.DependencyOrderTests

open NUnit.Framework
open FsUnit
open Mono.Cecil
open Paket
open TestHelpers

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
    let result, warnings = collectWarnings (fun () -> DependencyOrder.getDllOrder [ reactive; core ])
    names result |> List.sort |> shouldEqual [ "FSharp.Core"; "System.Reactive" ]
    warnings |> shouldEqual []

[<Test>]
let ``getDllOrder still orders dependencies first`` () =
    let a = makeAssembly "A" [] true
    let b = makeAssembly "B" [ a ] false
    names (DependencyOrder.getDllOrder [ b; a ]) |> shouldEqual [ "A"; "B" ]

[<Test>]
let ``getDllOrder warns about the assembly it forces out of a cycle`` () =
    let a = makeAssembly "A" [] false
    let b = makeAssembly "B" [ a ] false
    a.MainModule.AssemblyReferences.Add(AssemblyNameReference("B", System.Version(1, 0, 0, 0)))
    let result, warnings = collectWarnings (fun () -> DependencyOrder.getDllOrder [ a; b ])
    names result |> shouldEqual [ "A"; "B" ]
    warnings |> List.length |> shouldEqual 1
    StringAssert.Contains("assembly 'A' ", warnings.Head)

[<Test>]
let ``getPackageOrderGeneric keeps items it cannot order`` () =
    let deps = dict [ "a", [ "b" ]; "b", [ "a" ]; "c", [] ]
    let warned = ResizeArray()
    DependencyOrder.getPackageOrderGeneric id (fun p -> deps.[p] :> seq<_>) warned.Add [ "a"; "b"; "c" ]
    |> shouldEqual [ "c"; "a"; "b" ]
    List.ofSeq warned |> shouldEqual [ "a" ]

[<Test>]
let ``getPackageOrderGeneric orders the items depending on a cycle after it`` () =
    let deps = dict [ "a", [ "b" ]; "b", [ "a" ]; "c", []; "d", [ "a" ] ]
    let warned = ResizeArray()
    DependencyOrder.getPackageOrderGeneric id (fun p -> deps.[p] :> seq<_>) warned.Add [ "d"; "a"; "b"; "c" ]
    |> shouldEqual [ "c"; "a"; "b"; "d" ]
    List.ofSeq warned |> shouldEqual [ "a" ]

[<Test>]
let ``getPackageOrderGeneric keeps every item of the name it forces out of a cycle`` () =
    let deps = dict [ "a", [ "b" ]; "b", [ "a" ] ]
    let warned = ResizeArray()
    DependencyOrder.getPackageOrderGeneric fst (fun (p, _) -> deps.[p] :> seq<_>) warned.Add [ "a", 1; "a", 2; "b", 0 ]
    |> shouldEqual [ "a", 1; "a", 2; "b", 0 ]
    List.ofSeq warned |> shouldEqual [ "a", 1 ]
