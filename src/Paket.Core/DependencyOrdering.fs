module Paket.DependencyOrdering

open Microsoft.Build
open Microsoft.Build.Framework
open Microsoft.Build.Tasks
open Microsoft.Build.Utilities
open System.Reflection
open System

type String = string
type Seq<'a> = seq<'a>
type Bool = bool

module List =
  let toStringWithDelims (fr: String) (sep: String) (bk: String) (xs: List<'a>) : String =
    let rec toSWD acc ys =
      match ys with
      | []       -> acc
      | [z]      -> sprintf "%s%A" acc z
      | y::z::zs -> toSWD (sprintf "%s%A%s" acc y sep) (z::zs)
    fr + toSWD "" xs + bk

module Object =
  let eqHack (f: 'a -> 'b) (x: 'a) (yobj: Object) : Boolean =
    match yobj with
    | :? 'a as y -> f x = f y
    | _          -> false

  let compHack (f: 'a -> 'b) (x: 'a) (yobj: Object) : Int32 =
    match yobj with
    | :? 'a as y -> compare (f x) (f y)
    | _          -> invalidArg "yobj" "Cannot compare elements of incompatible types"

type Digraph<'n> when 'n : comparison = 
  Map<'n, Set<'n>> 

module Digraph =
  
  let addNode (n: 'n) (g: Digraph<'n>) : Digraph<'n> =
    match Map.tryFind n g with
    | None -> Map.add n Set.empty g
    | Some _ -> g

  let addEdge ((n1, n2): 'n * 'n) (g: Digraph<'n>) : Digraph<'n> =
    let g' = 
      match Map.tryFind n2 g with
      | None -> addNode n2 g
      | Some _ -> g
    match Map.tryFind n1 g with
    | None -> Map.add n1 (Set.singleton n2) g'
    | Some ns -> Map.add n1 (Set.add n2 ns) g'

  let nodes (g: Digraph<'n>) : List<'n> =
    Map.fold (fun xs k _ -> k::xs) [] g

  let roots (g: Digraph<'n>) : List<'n> =
    List.filter (fun n -> not (Map.exists (fun _ v -> Set.contains n v) g)) (nodes g)

  let topSort (h: Digraph<'n>) : List<'n> =
    let rec dfs (g: Digraph<'n>, order: List<'n>, rts: List<'n>) : List<'n> =
      if List.isEmpty rts then
        order
      else
        let n = List.head rts
        let children = Map.find n g
        let order' = n::order
        let g' = Map.remove n g
        let rts' = roots g'
        dfs (g', order', rts')
    dfs (h, [], roots h)

[<CustomEquality>]
[<CustomComparison>]
[<StructuredFormatDisplay("{show}")>]
type AssemblyRef =
  {
    Path: String
    Assembly: Assembly
    Name: String
  }

  member this.show = this.ToString ()

  override this.Equals (obj: Object) : bool =
    Object.eqHack (fun (a:AssemblyRef) -> a.Name) this obj

  override this.GetHashCode () = 
    hash this.Name
  
  interface System.IComparable with 
    member this.CompareTo (obj: Object) =
      Object.compHack (fun (p:AssemblyRef) -> p.Name) this obj

  override x.ToString () = x.Path

[<Serializable>]
type ForeignAidWorker () =

  let mkGraph (seeds: seq<AssemblyRef>) : Digraph<AssemblyRef> =

    let findRef (s: Seq<AssemblyRef>) (m: AssemblyName) : Seq<AssemblyRef> =
      match Seq.tryFind (fun (r: AssemblyRef) -> r.Name = m.Name) seeds with
      | None    -> s
      | Some ar -> Seq.append (Seq.singleton ar) s

    let processNode (g: Digraph<AssemblyRef>) (n: AssemblyRef) : Digraph<AssemblyRef> =
      let depNames = n.Assembly.GetReferencedAssemblies ()
      let depRefs = Array.fold findRef Seq.empty depNames
      Seq.fold (fun h c -> Digraph.addEdge (n, c) h) g depRefs

    let rec fixpoint (g: Digraph<AssemblyRef>) : Digraph<AssemblyRef> =
      let ns = Digraph.nodes g
      let g' = List.fold processNode g ns
      if g = g' then g else fixpoint g'

    fixpoint (Seq.fold (fun g s -> Digraph.addNode s g) Map.empty seeds)

  let mkAssemblyRef (t: String) : AssemblyRef =
    let asmBytes = System.IO.File.ReadAllBytes(t)
    let assm = Assembly.Load(asmBytes)
    {
      Path = t
      Assembly = assm
      Name = assm.GetName().Name
    }

  member x.Work(rs: String[]) : String = 
    let asmRefs = Array.map mkAssemblyRef rs
    let graph = mkGraph asmRefs
    let ordering = Digraph.topSort graph
    let str = List.toStringWithDelims "#r @\"" "\"\n#r @\"" "\"" ordering
    str 

type OrderAssemblyReferences() = 
  inherit Task ()

  // These dlls are explicitly excluded. This is to prevent
  // triggering a bug in Mono where the path to mscorlib is
  // overridden by the empty string. In any case, System.dll
  // depends on itself, and was never output by the topological
  // sort. All these dlls are loaded by FSI automatically.
  let excludes = ["mscorlib.dll"; "System.dll"; "FSharp.Core.dll"; "System.Core.dll"]

  member val ReferencePaths = null with get,set

  [<Output>]
  member val Ordering = "" with get,set

  override x.Execute () : Bool =
    let setup = AppDomainSetup()
    do setup.ApplicationBase <- System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
    let appDomain = AppDomain.CreateDomain("TestDomain", null, setup)
    try 
      let paths = Array.map (fun (i: ITaskItem) -> i.ItemSpec) x.ReferencePaths
                  |> Array.filter (fun (s: String) -> List.forall (fun s' -> not (s.EndsWith s')) excludes)
      let faw = (appDomain.CreateInstanceAndUnwrap(typeof<ForeignAidWorker>.Assembly.FullName, typeof<ForeignAidWorker>.FullName)) :?> ForeignAidWorker
      let ordering = faw.Work(paths)
      do x.Ordering <- ordering
      true
    finally
      do AppDomain.Unload(appDomain)
    
