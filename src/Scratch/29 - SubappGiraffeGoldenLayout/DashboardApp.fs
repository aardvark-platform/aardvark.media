namespace Test.Dashboard

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering

open Aether
open Aether.Operators

open Test.UI


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Dashboard = 
    [<Literal>]
    let WORKSPACE = "Workspace"
    [<Literal>]
    let SIMULATION = "Simulation"
    [<Literal>]
    let TRACK = "Track"
    [<Literal>]
    let MAP = "Map"


    let update win (m : Dashboard) (msg : DashboardAction) =
        //Log.line "[Dashboard] Message: %s" (string msg)
        match msg with
        | SetUserMode userMode ->
            {m with userMode = userMode}
        | SetClientSession str ->
            Log.line "[Dashboard] Setting client session = %s" str
            {m with clientId = str}
        | IncMessage (id, msg) ->
            let inc = Tmp.Inc.App.update (m.incApps.Item id) msg
            let incApps = HashMap.add inc.id inc m.incApps
            {m with incApps = incApps}
            

    let dependencies = 
        //R4F.UI.JsCssRequirements.overrides 
        Html.semui 
        @ [
           // { name = "js"; url = "http://code.jquery.com/jquery-1.11.1.min.js"; kind = Script } // ADDING THIS BREAKS FOMANTIC UI JAVASCRIPT
            { name = "Golden"; url = @"https://golden-layout.com/files/latest/js/goldenlayout.min.js"; kind = Script }
            { name = "GoldenCSS"; url = @"resources/goldenlayout-base.css"; kind = Stylesheet }
            { name = "Theme"; url = @"resources/goldenlayout-dark-theme.css"; kind = Stylesheet }
            { name = "aardvark.js"; url = "resources/aardvark.js"; kind = Script }
            //{ name = "OnBoot"; url = "onBootDashboard.js"; kind = Script }
        ]     

    let viewUserSelection (m : AdaptiveDashboard) =
        let content = [Insert.dropdownEnum m.userMode SetUserMode]
        To.placeholderSegment "user icon" "Please select dashboard mode" content

    let debugView (m : AdaptiveDashboard) = 
        

//        div [] [
        page (fun request -> 
            match Map.tryFind "page" request.queryParams with
            | Some WORKSPACE -> 
                (AMap.find WORKSPACE m.incApps) |> AVal.map Tmp.Inc.App.view
                                                |> To.divAval
                |> UI.map (fun msg -> IncMessage (WORKSPACE, msg))

            | Some SIMULATION -> //TODO refactor Simulation
                (AMap.find SIMULATION m.incApps) |> AVal.map Tmp.Inc.App.view
                                                |> To.divAval
                |> UI.map (fun msg -> IncMessage (SIMULATION, msg))
            | Some TRACK -> 
                (AMap.find TRACK m.incApps) |> AVal.map Tmp.Inc.App.view
                                                |> To.divAval
                |> UI.map (fun msg -> IncMessage (TRACK, msg))
            | Some MAP -> 
                (AMap.find MAP m.incApps) |> AVal.map Tmp.Inc.App.view
                                                |> To.divAval
                |> UI.map (fun msg -> IncMessage (MAP, msg))
            | _ -> 
                require dependencies (
                    onBoot "aardvark.golden.layout = aardvark.golden.initLayout()" (
                        body [] []
                    )
                )
        )
                
        //] 
        

    let view (m : AdaptiveDashboard) =
        Log.line "[Dashboard] Starting View. Dashboard with client id %s" (string m.clientId)    
        let sessionChangeAttribute =
            Test.DataChannel.onDataChangeAttribute SetClientSession 
                                              m.clientId
                                              (fun str -> str)
        let appIds = AMap.keys m.incApps |> AList.ofASet
        let allPages =
            alist {
                for id in appIds do
                    let! inc = AMap.find id m.incApps
                    let txt = AVal.map2 (fun id count -> sprintf "View %s has count %i" id count) inc.id inc.value
                    yield (div [] [Incremental.text txt])
            } |> To.divA

        let content =
            alist {
                let! userMode = m.userMode
                match userMode with
                | UserMode.NotSelected ->
                    yield viewUserSelection m
                | UserMode.Developer ->
                    yield debugView m
                | UserMode.Expert ->
                    yield debugView m
                | UserMode.Simple ->
                    yield debugView m
                | _ -> 
                    yield viewUserSelection m
            }
        body [sessionChangeAttribute] [
            allPages
            content |> To.divA
        ] |> Test.DataChannel.addDataChannel "aardvark.processEvent('__ID__', 'data-event', aardvark.guid);" "" None m.clientId

    let threads (m : Dashboard) = 
        m.threads

    let initial inServerMode dataFolder guid =   
        Log.line "[DashboardApp] Init Dashboard %s" (string guid)

        let inc0 = Tmp.Inc.Model.init WORKSPACE
        let inc1 = Tmp.Inc.Model.init MAP
        let inc2 = Tmp.Inc.Model.init TRACK
        let inc3 = Tmp.Inc.Model.init SIMULATION
        let incApps =
            [
                (inc0.id, inc0)
                (inc1.id, inc1)
                (inc2.id, inc2)
                (inc3.id, inc3)
            ] |> HashMap.ofList
        {   
            userMode      = UserMode.NotSelected
            inServerMode  = inServerMode
            clientId      = guid
            background    = C4b(34,34,34)
            threads       = ThreadPool.empty
            debugCount    = 0
            incApps       = incApps
            
            
            
        }

    open Aardvark.Application.Slim

    let app (win : Aardvark.Glfw.Window) inServerMode
        dataFolder (application : OpenGlApplication) guid = 
        {
            unpersist = Unpersist.instance
            threads = threads 
            initial = initial inServerMode dataFolder guid
            update = update win
            view = view
        }
