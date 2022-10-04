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
        | BindComponent str ->
            Log.line "Bind component"
            m
        | UnbindComponent str ->
            Log.line "Unbind component"
            m
            
    let dependencies = 
        [
           // { name = "js"; url = "http://code.jquery.com/jquery-1.11.1.min.js"; kind = Script } // ADDING THIS BREAKS FOMANTIC UI JAVASCRIPT
            { kind = Stylesheet; name = "semui"; url = "resources/semantic.css" }
            { kind = Stylesheet; name = "semui-overrides"; url = "resources/semantic-overrides.css" }
            { kind = Script; name = "semui"; url = "resources/semantic.js" }
            { kind = Script; name = "essential"; url = "resources/essentialstuff.js" }
            { name = "Golden"; url = @"resources/golden-layout.js"; kind = Script }
            { name = "GoldenCSS"; url = @"resources/goldenlayout-base.css"; kind = Stylesheet }
            { name = "Theme"; url = @"resources/goldenlayout-dark-theme.css"; kind = Stylesheet }
            { name = "aardvark.js"; url = "resources/aardvark.js"; kind = Script }
            
        ]     

    let viewUserSelection (m : AdaptiveDashboard) =
        let content = [Insert.dropdownEnum m.userMode SetUserMode]
        To.placeholderSegment "user icon" "Please select dashboard mode" content

    let debugView (m : AdaptiveDashboard) = 
        
        let viewPage id =
            let content =
                (AMap.find id m.incApps) |> AVal.map Tmp.Inc.App.view
                                |> To.divAval
                                |> UI.map (fun msg -> IncMessage (id, msg))
            div [clazz id] [ //style "overflow:hidden; position: absolute;"] [
                    content
                ]

        let onBindComponent =
            let f =  (fun (lst : list<string>) -> lst |> List.head |> Pickler.unpickleOfJson |> BindComponent)
            onEvent "bindComponentEvent" [] f

        let onUnbindComponent =
            let f =  (fun (lst : list<string>) -> lst |> List.head |> Pickler.unpickleOfJson |> UnbindComponent)
            onEvent "unbindComponentEvent" [] f

        require dependencies (
            onBoot "aardvark.golden.layout = getTopAardvark().golden.initLayout($('#__ID__'))" (
                div [clazz "layoutContainer";onBindComponent;onUnbindComponent; style "width: 100%; height: 100%"] [
                    viewPage WORKSPACE
                    viewPage SIMULATION
                    viewPage MAP
                    viewPage TRACK
                ]
            )
        )
       
        

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
            div [clazz "wrapper"] [
                allPages
                content |> To.divA
            ]
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
