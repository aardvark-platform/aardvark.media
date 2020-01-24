module Inc.App
open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering
open Inc.Model

module Support =
    open System.Net
    open System.Net.Sockets

    let getFreePort() =  
        try
            let ep = new IPEndPoint(IPAddress.Loopback, 0)
            use s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            s.Bind(ep)
            let ep = unbox<IPEndPoint> s.LocalEndPoint
            ep.Port
        with _ ->
            4321

let update (send : InstanceMessage -> unit) (model : Model) (msg : Message) =
    match msg with
        | Start e -> 
            let id = System.Guid.NewGuid() |> string
            let port = Support.getFreePort()
            let stdout s = send (InstanceMessage.Stdout(id,s))
            let stderr s = send (InstanceMessage.Stdout(id,s))
            let dead () = send (InstanceMessage.Exit id)
            let p = InterProcess.runService e.assembly e.workingDirectory port stdout stderr dead id
            let instance = 
                {                   
                    p = p
                    id = id
                    port = port
                    endpoint = e
                }
            { model with running = HashMap.add id instance model.running }
        | InstanceStatus (InstanceMessage.Exit e) -> 
            printfn "removing instance: %A" e
            { model with running = HashMap.remove e model.running }
        | InstanceStatus (InstanceMessage.Stderr(id,e)) -> 
            printfn "instance %A errors: %s" id e
            model
        | InstanceStatus (InstanceMessage.Stdout(id,e)) -> 
            printfn "instance %A says: %s" id e 
            model
        | Kill i -> 
            match HashMap.tryFind i model.running with
                | Some instance -> 
                    Log.warn "killing: %A" instance
                    instance.p.Kill()
                    model
                | _ -> 
                    Log.warn "cannot kill non-existing instance"
                    model

let view (model : AdaptiveModel) =
    let runningInstances = model.running |> AMap.toASet |> ASet.sortBy fst
    require Html.semui (
        page (fun request -> 
            match Map.tryFind "page" request.queryParams with
                | Some "client" -> 
                    match Map.tryFind "id" request.queryParams with
                        | Some id -> 
                            let stdout = sprintf "./%s/stdout.txt" id
                            let stderr = sprintf "./%s/stderr.txt" id
                            let url =  AMap.tryFind id model.running  |> AVal.map (function None -> None | Some instance -> Some (instance.endpoint.url instance.port)) 
                            let urlLink =
                                amap {
                                    let! url = url
                                    match url with
                                        | Some url -> 
                                            yield attribute "href" url
                                        | None -> ()
                                }
                            div [] [
                                a [attribute "href" stdout] [text "stdout"]
                                br []
                                a [attribute "href" stderr] [text "stderr"]
                                br []
                                Incremental.a (AttributeMap.ofAMap urlLink) (AList.ofList [text "open"])
                                br []
                                a [attribute "href" "javascript:history.back()"] [text "Back"]  
                            ]
                        | None -> text "id not found"
                | Some "admin" -> 
                    match Map.tryFind "username" request.queryParams with
                        | Some "rockstar" -> 
                            div [] [
                                Incremental.div AttributeMap.empty <|
                                    alist {
                                        for endpoint in model.executables do
                                            yield 
                                                button [onClick (fun _ -> Start endpoint)] [
                                                    text endpoint.prettyName
                                                ]

                                    }
                                Incremental.div AttributeMap.empty <| 
                                    alist {
                                        for (id,i) in runningInstances do
                                            yield div [] [
                                                let link = sprintf ".?page=client&id=%s" id
                                                yield a [attribute "href" link] [text "super"]
                                                yield button [onClick (fun _ -> Kill id)] [text "kill"]
                                            ]
                                    }
                            ]
                        | _ -> div [] [text "unauthorized."]
                | _ -> 
                    require Html.semui (
                        div [] [
                        ]
                    )
        )
    )


let threads (model : Model) = 
    ThreadPool.empty


module Discovery = 
    open System
    open System.IO

    let discoverApps () =  
        [ 
            { assembly = DotnetAssembly "17 - Serviced.dll"         
              workingDirectory = "."
              prettyName = "Demo"       
              url = (fun port -> sprintf "http://localhost:%d" port)
            }

            { assembly = DotnetAssembly "17 - Serviced.exe"         
              workingDirectory = "publish"
              prettyName = "Demo"       
              url = (fun port -> sprintf "http://localhost:%d" port)
            }
        ]


let app send =                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
    {
        unpersist = Unpersist.instance     
        threads = threads 
        initial = 
            { 
               executables = 
                    Discovery.discoverApps() |> IndexList.ofList
               running = HashMap.empty
            }
        update = update send
        view = view
    }
