module Inc.App
open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Inc.Model

let update (model : Model) (msg : Message) =
    match msg with
        Inc -> { model with value = model.value + 1 }

let dependencies = 
    [
        //{ name = "sigma"; url = "https://cdnjs.cloudflare.com/ajax/libs/sigma.js/1.2.1/sigma.min.js"; kind = Script }
        //{ name = "labels"; url = "https://cdnjs.cloudflare.com/ajax/libs/sigma.js/1.2.1/plugins/sigma.renderers.edgeLabels.min.js"; kind = Script }
        //{ name = "force"; url = "https://cdnjs.cloudflare.com/ajax/libs/sigma.js/1.2.1/plugins/sigma.layout.forceAtlas2.min.js"; kind = Script }
        { name = "viva"; url = "https://cdnjs.cloudflare.com/ajax/libs/vivagraphjs/0.10.1/vivagraph.min.js"; kind = Script }
        { url = "style.css"; name = "style.css"; kind = Stylesheet }
        { url = "force.js"; name = "force.js"; kind = Script }
    ]

open Chiron

type Node = { name: string; } with
     static member ToJson (x:Node) =
        Json.write "name" x.name

type Edge = { id: string; weight: float; fromId : int; toId : int } with
    static member ToJson (x:Edge) =
        json {
            do! Json.write "id" x.id 
            do! Json.write "weight" x.weight
            do! Json.write "fromId" x.fromId
            do! Json.write "toId" x.toId
        }
    
type Graph = { nodes : array<Node>; edges : array<Edge> } with
    static member ToJson(x : Graph) =
        json {
            do! Json.write "nodes" x.nodes
            do! Json.write "edges" x.edges
        }

module Printing =
    let htmlify (s:string) =
        s.Replace("<","&lt;").Replace(">","&gt;").Replace("\"","&quot;").Trim().Replace("\t","").Replace("\r\n","").Replace("\r","").Replace("\n","")
    let unhtmlify (s:string) =
        s.Replace("&lt;","<").Replace("&gt;",">").Replace("&quot;","\"")

let view (model : MModel) =
    let graph = { nodes = [| { name= "a"}; {name="b"}; {name="c"}|]; edges = [| {id="e"; weight=1.0; fromId=0; toId=1}; {id="f"; weight=10.0; fromId=0; toId=2}; {id="g"; weight=5.0; fromId=1; toId=2}   |] }
    let a = graph |> Json.serialize |> Json.format
    require dependencies (
        onBoot (sprintf "mkForce(__ID__,'%s')" a) (
            div [style "width:500px;height:500px"] [
            
            ]
        )
    )


let threads (model : Model) = 
    ThreadPool.empty


let app =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
               value = 0
            }
        update = update 
        view = view
    }
