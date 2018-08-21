namespace Aardvark.UI.Primitives

open Aardvark.UI
open Aardvark.Base.Incremental

type DockElement =
    {
        id : string
        weight : float
        title : Option<string>
        deleteInvisible : Option<bool>
    }


type DockNodeConfig =
    | Vertical of weight : float * children : list<DockNodeConfig>
    | Horizontal of weight : float * children : list<DockNodeConfig>
    | Stack of weight : float * activeId : Option<string> * children : list<DockElement>
    | Element of element : DockElement

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DockNodeConfig =
    

    open Newtonsoft.Json
    open Newtonsoft.Json.Linq

    let rec internal tryReadJObject (o : JObject) =
        match o.TryGetValue "kind" with
            | (true, value) ->
                let kind : string = JToken.op_Explicit value
                let weight : float = JToken.op_Explicit o.["weight"]
                match kind with
                    | "vertical" ->
                        let children = o.["children"].Values() |> Seq.toList |> List.choose tryReadJObject
                        let res = Vertical(weight, children)
                        Some res

                    | "horizontal" ->
                        let children = o.["children"].Values() |> Seq.toList |> List.choose tryReadJObject
                        let res = Horizontal(weight, children)
                        Some res
                        
                    | "stack" ->
                        let children = o.["children"].Values() |> Seq.toList |> List.choose tryReadJObject |> List.choose (function Element e -> Some e | _ -> None)
                        let active : Option<string> =
                            match o.TryGetValue "activeTabId" with
                                | (true, v) -> Some (JToken.op_Explicit v)
                                | _ -> None

                        let res = Stack(weight, active, children)
                        Some res

                    | "element" ->
                        let id : string = o.["id"] |> JToken.op_Explicit
                        let title : Option<string> = match o.TryGetValue "title" with | (true, value) -> Some (JToken.op_Explicit value) | _ -> None
                        let deleteInvisible : Option<bool> = match o.TryGetValue "deleteInvisible" with | (true, value) -> Some (JToken.op_Explicit value) | _ -> None

                        Some (Element { DockElement.id = id; DockElement.weight = weight; DockElement.title = title; DockElement.deleteInvisible = deleteInvisible })

                    | _ ->
                        None

            | _ ->
                None

    let rec internal toJObject (cfg : DockNodeConfig) =
        let o = JObject()

        match cfg with
            | Vertical(w, children) ->
                let children = children |> List.toArray |> Array.map (fun v -> v |> toJObject :> obj)
                o.["kind"] <- JToken.op_Implicit "vertical"
                o.["weight"] <- JToken.op_Implicit w
                o.["children"] <- JArray(children)
                o

            | Horizontal(w, children) -> 
                let children = children |> List.toArray |> Array.map (fun v -> v |> toJObject :> obj)
                o.["kind"] <- JToken.op_Implicit "horizontal"
                o.["weight"] <- JToken.op_Implicit w
                o.["children"] <- JArray(children)
                o

            | Stack(w, active, elements) ->
                let children = elements |> List.toArray |> Array.map (fun v -> v |> Element |> toJObject :> obj)
                o.["kind"] <- JToken.op_Implicit "stack"
                o.["weight"] <- JToken.op_Implicit w
                o.["children"] <- JArray(children)

                match active with
                    | Some a -> o.["activeTabId"] <- JToken.op_Implicit a
                    | None -> ()

                o

            | Element e ->
                o.["kind"] <- JToken.op_Implicit "element"
                o.["id"] <- JToken.op_Implicit e.id
                o.["weight"] <- JToken.op_Implicit e.weight
                match e.title with | Some v -> o.["title"] <- JToken.op_Implicit v | None -> ()
                match e.deleteInvisible with | Some v -> o.["deleteInvisible"] <- JToken.op_Implicit v | None -> ()

                o



    let rec tryOfJSON (str : string) =
        let o = JObject.Parse(str)
        tryReadJObject o

    let ofJSON (str : string) =
        match tryOfJSON str with
            | Some res -> res
            | None -> failwithf "[Docking] not a config"

    let toJSON (cfg : DockNodeConfig) =
        let o = toJObject cfg
        o.ToString()

type DockConfig =
    {
        content : DockNodeConfig
        specialDockSize : Option<float>

        appName         : Option<string>
        useCachedConfig : Option<bool>
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DockConfig =
    
    open Newtonsoft.Json
    open Newtonsoft.Json.Linq

    let tryOfJSON (str : string) =
        let o = JObject.Parse(str)
        match DockNodeConfig.tryReadJObject((unbox o.["content"])) with
            | Some res ->
                let specialDockSize : Option<float> = match o.TryGetValue "specialDockSize" with | (true, v) -> Some (JToken.op_Explicit v) | _ -> None
                let appName : Option<string> = match o.TryGetValue "appName" with | (true, v) -> Some (JToken.op_Explicit v) | _ -> None
                let useCachedConfig : Option<bool> = match o.TryGetValue "useCachedConfig" with | (true, v) -> Some (JToken.op_Explicit v) | _ -> None
                Some { content = res; specialDockSize = specialDockSize; appName = appName; useCachedConfig = useCachedConfig }
            | None ->
                None

    let ofJSON (str : string) =
        match tryOfJSON str with
            | Some cfg -> cfg
            | None -> failwith "[Docking] not a config"

    let toJSON (cfg : DockConfig) =
        let o = JObject()
        match cfg.specialDockSize with | Some v -> o.["specialDockSize"] <- JToken.op_Implicit v | _ -> ()
        match cfg.appName with | Some v -> o.["appName"] <- JToken.op_Implicit v | _ -> ()
        match cfg.useCachedConfig with | Some v -> o.["useCachedConfig"] <- JToken.op_Implicit v | _ -> ()
        o.["content"] <- DockNodeConfig.toJObject cfg.content
        o.ToString(Formatting.None)

[<AutoOpen>]
module DockingBuilder =
    
    let inline vertical (weight : float) (children : list<DockNodeConfig>) =
        DockNodeConfig.Vertical(weight, children)
    
    let inline horizontal (weight : float) (children : list<DockNodeConfig>) =
        DockNodeConfig.Horizontal(weight, children)

    let inline stack (weight : float) (active : Option<string>) (children : list<DockElement>) =
        DockNodeConfig.Stack(weight, active, children)

    type DockElementBuilder<'a>(run : DockElement -> 'a) =
        member x.Yield(()) = { id = ""; title = None; weight = 1.0; deleteInvisible = None }

        [<CustomOperation("id")>]
        member x.Id(cfg : DockElement, id : string) =
            { cfg with id = id }
            
        [<CustomOperation("weight")>]
        member inline x.Weight(cfg : DockElement, w : 'x) =
            { cfg with weight = (float w) }

        [<CustomOperation("title")>]
        member x.Title(cfg : DockElement, title : string) =
            { cfg with title = Some title }
            
        [<CustomOperation("deleteInvisible")>]
        member x.DeleteIfInvisible(cfg : DockElement) =
            { cfg with deleteInvisible = Some true }

        member x.Run(v : DockElement) = run v

    let dockelement = DockElementBuilder<DockElement>(id)
    let element = DockElementBuilder<DockNodeConfig>(DockNodeConfig.Element)

    let test = element { id "hugo"; title "Hugo"; weight 10 }

    type ConfigBuilder() =
        member x.Yield(()) = { content = Vertical(1.0,[]); specialDockSize = None; appName = None; useCachedConfig = None }

        [<CustomOperation("content")>]
        member x.Content(cfg : DockConfig, content : DockNodeConfig) =
            { cfg with content = content }

        [<CustomOperation("specialDockSize")>]
        member x.SpecialDockSize(cfg : DockConfig, content : float) =
            { cfg with specialDockSize = Some content }

        [<CustomOperation("appName")>]
        member x.AppName(cfg : DockConfig, name : string) =
            { cfg with appName = Some name }

        [<CustomOperation("useCachedConfig")>]
        member x.UseCachedConfig(cfg : DockConfig, name : bool) =
            { cfg with useCachedConfig = Some name }

    let config = ConfigBuilder()


[<AutoOpen>]
module DockingUIExtensions =
    
    let private bootCode = """
        var init = function(element,id,info) {
            element.innerHTML = "<iframe src='./?page=" + id + "' style='border:none;width:100%;height:100%;'></iframe>";
        };   

        var root = document.getElementById('__ID__');
        var config = JSON.parse("__INITIALCONFIG__");
        var layouter = new Docking.DockLayout(root, config, init);

        root.classList.add('dock-root');

        if(typeof dockconfig != 'undefined') {
            dockconfig.onmessage = function(data) {
                var cfg = JSON.parse(data);
                layouter.currentConfig = cfg;
            }
        }
        if(__NEEDSEVENT__){
            layouter.onlayoutchanged = function(cfg) {
                aardvark.processEvent('__ID__', 'onlayoutchanged', JSON.stringify(cfg));
            };
        }
        """

    let private dependencies =
        [
            { name = "docking-js-style"; url = "./rendering/docking.css"; kind = Stylesheet }
            { name = "docking-js"; url = "./rendering/docking.js"; kind = Script }
        ]

    let onLayoutChanged (f : DockConfig -> 'msg) =
        let callback (l : list<string>) =
            match l with
                | h :: _ ->
                    let h = Pickler.json.UnPickleOfString h
                    match DockConfig.tryOfJSON h with
                        | Some cfg -> Seq.singleton (f cfg)
                        | _ -> Seq.empty
                | _ ->
                    Seq.empty

        "onlayoutchanged", AttributeValue.Event(Event.ofDynamicArgs [] callback)

    [<AutoOpen>]
    module Static = 
        let docking (atts : list<string * AttributeValue<'msg>>) (cfg : IMod<DockConfig>) = 
            let initial =  DockConfig.toJSON (Mod.force cfg)
            let boot = bootCode.Replace("__INITIALCONFIG__", initial.Replace("\\", "\\\\").Replace("\"", "\\\""))
            let hasLayoutChanged = atts |> List.exists (fst >> ((=) "onlayoutchanged"))
            let boot = boot.Replace("__NEEDSEVENT__", if hasLayoutChanged then "true" else "false")
            let channels = if cfg.IsConstant then [] else ["dockconfig", Mod.channel (cfg |> Mod.map DockConfig.toJSON) ]
            require dependencies (
                onBoot' channels boot (
                    div atts []
                )
            )





