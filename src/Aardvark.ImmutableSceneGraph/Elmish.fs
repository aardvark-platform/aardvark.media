namespace Aardvark.Elmish

open Fablish
open Aardvark.Base
open Aardvark.Application
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open System.Collections.Generic
open System

open Aardvark.ImmutableSceneGraph


type Direction = Up | Down


type Sub<'msg> = 
    | NoSub
    | TimeSub of TimeSpan * (DateTime -> 'msg) 
    | Many of list<Sub<'msg>>
    | MouseClick of (MouseButtons -> PixelPosition -> Option<'msg>)
    | Mouse of (Direction -> MouseButtons -> PixelPosition -> Option<'msg>)
    | MouseMove of (PixelPosition * PixelPosition -> 'msg)
    | Key of (Direction -> Keys -> Option<'msg>)
    | ModSub of IMod<'msg> * (float ->'msg -> Option<'msg>)
            
                
module Sub =

    let rec leaves s =
        match s with
            | NoSub -> []
            | TimeSub _ -> [s]
            | Many xs -> xs |> List.collect leaves
            | MouseClick _ -> [s]
            | Mouse _ -> [s]
            | MouseMove _ -> [s]
            | Key f -> [s]
            | ModSub(m,f) -> [s]

    let filterMouseClicks s = s |> leaves |> List.choose (function | MouseClick f -> Some f | _ -> None)
    let filterMouseThings s = s |> leaves |> List.choose (function | Mouse f -> Some f | _ -> None)
    let filterMoves s = s |> leaves |> List.choose (function | MouseMove m -> Some m | _ -> None)
    let filterKeys s = s |> leaves |> List.choose (function | Key f -> Some f | _ -> None)
    let filterTimes s = s |> leaves |> List.choose (function | TimeSub (t,f) -> Some (t,f) | _ -> None)
    let filterMods s = s |> leaves |> List.choose (function | ModSub (m,f) -> Some (m,f) | _ -> None)

    let time timeSpan f = TimeSub(timeSpan,f)
    let ofMod m f = ModSub(m,f)



[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Subscriptions =
    let none = fun _ -> NoSub

module Input =
        
    let mouse (dir : Direction -> bool) (button : MouseButtons -> bool) (f : PixelPosition -> 'msg) = 
        Mouse (fun d b p -> if dir d && button b then Some (f p) else None)

    let click (button : MouseButtons -> bool) (f : PixelPosition -> 'msg) =
        MouseClick (fun b p -> if button b then Some (f p) else None)

    let move (f : PixelPosition * PixelPosition -> 'msg) = Sub.MouseMove f

    let moveDelta (f : V2d -> 'msg) = Sub.MouseMove (fun (oldP,newP) -> f (newP.NormalizedPosition - oldP.NormalizedPosition))

    let key (dir : Direction) (k : Keys) f = Sub.Key(fun occDir occ -> if k = occ && dir = occDir then Some (f occDir occ) else None)

    let toggleKey (k : Keys) (onDown : Keys -> 'msg) (onUp : Keys -> 'msg) =
        Sub.Many [
            key Direction.Down k (fun _ k -> onDown k)
            key Direction.Up k (fun _ k -> onUp k)
        ]

    module Mouse =
    
        let button ref p = if p = ref then true else false
        let left = button MouseButtons.Left
        let right = button MouseButtons.Right
        let up f = if f = Direction.Up then true else false
        let down f = if f = Direction.Down then true else false

type App<'model,'mmodel,'msg,'view> =
    {
        initial   : 'model
        update    :  Env<'msg> -> 'model  -> 'msg  -> 'model
        view      : 'mmodel    -> 'view

        subscriptions : 'model -> Sub<'msg>

        ofPickMsg : 'model     -> GlobalPick  -> list<'msg>
    }

type Unpersist<'immut,'mut> =
    {
        unpersist : 'immut -> ReuseCache -> 'mut
        apply     : 'immut -> 'mut -> ReuseCache -> unit
    }


module Elmish =
    
    let inline fst' (a,b,c) = a
    let inline snd' (a,b,c) = b
    let inline trd' (a,b,c) = c


    let inline unpersist< ^immut, ^mut when ^immut : (member ToMod : ReuseCache -> ^mut) and ^mut : (member Apply : ^immut * ReuseCache -> unit) > () : Unpersist< ^immut,^ mut> = 
        let inline u  (immut : ^immut) (scope : ReuseCache) = (^immut : (member ToMod : ReuseCache -> ^mut) (immut,scope))
        let inline a  (immut : ^immut) (mut : ^mut) (scope : ReuseCache) = (^mut : (member Apply : ^immut * ReuseCache -> unit) (mut,immut,scope))
        { unpersist = u; apply = a }

    type Running<'model,'msg> =
        {
            send : 'msg -> 'model
            emitModel : 'model -> unit
            sg : ISg
        }

    let createAppAdaptive (keyboard : IKeyboard) (mouse : IMouse) (viewport : IMod<Box2i>) (camera : IMod<Camera>) (unpersist :  Unpersist<'model,'mmodel>) (onMessageO : Option<Fablish.CommonTypes.Callback<'model,'msg>>) (app : App<'model,'mmodel,'msg, ISg<'msg>>)  =

        let model = Mod.init app.initial

        let mutable currentlyActive = false

        let transformPixel (p : PixelPosition) =
            let p = PixelPosition(p.Position, Mod.force viewport)
            p

        let reuseCache = ReuseCache()
        let mmodel = unpersist.unpersist model.Value reuseCache

        let mutable onMessage = Unchecked.defaultof<_>

        let mutable moves = []
        let mutable mouseActions = []
        let mutable clicks = []
        let mutable keys = []
        let mutable times = []

        let moveSub (oldP,newP) =
            if currentlyActive then
                moves |> List.map (fun f -> f (oldP,newP)) |> List.fold onMessage model.Value |> ignore

        let mouseActionsSub dir p =
            if currentlyActive then
                mouseActions |> List.choose (fun f -> f dir p (mouse.Position.GetValue())) |> List.fold onMessage model.Value |> ignore

        let mouseClicks p =
            if currentlyActive then
                clicks |> List.choose (fun f -> f p (mouse.Position.GetValue())) |> List.fold onMessage model.Value |> ignore

        let keysSub dir k =
            if currentlyActive then
                keys |> List.choose (fun f -> f dir k) |> List.fold onMessage model.Value |> ignore

        let currentTimer = new Dictionary<TimeSpan,ref<list<DateTime -> 'msg> * list<IDisposable>> * System.Timers.Timer>()
        let enterTimes xs =
            let newSubs = xs |> List.groupBy fst |> List.map (fun (k,xs) -> k,List.map snd xs) |> Dictionary.ofList
            let toRemove = List<_>()
            for (KeyValue(k,(r,timer))) in currentTimer do
                match newSubs.TryGetValue k with
                    | (true,v) -> 
                        let oldSubs = snd !r
                        oldSubs |> List.iter (fun a -> a.Dispose())
                        r := (v, v |> List.map (fun a -> timer.Elapsed.Subscribe(fun _ -> onMessage model.Value (a DateTime.Now) |> ignore)))
                    | _ -> 
                        !r |> snd |> List.iter (fun a -> a.Dispose())
                        timer.Dispose()
                        toRemove.Add(k)
            
            for (KeyValue(t,actions)) in newSubs do
                match currentTimer.TryGetValue t with
                    | (true,v) -> ()
                    | _ -> 
                        // new
                        let timer = new System.Timers.Timer()
                        timer.Interval <- t.TotalMilliseconds
                        let disps = actions |> List.map (fun a -> timer.Elapsed.Subscribe(fun _ -> onMessage model.Value (a DateTime.Now) |> ignore))
                        timer.Start()
                        currentTimer.Add(t,(ref (actions,disps), timer))
            
            for i in toRemove do currentTimer.Remove i |> ignore

        mouse.Move.Values.Subscribe(moveSub) |> ignore
        mouse.Click.Values.Subscribe(mouseClicks) |> ignore
        mouse.Down.Values.Subscribe(fun p -> mouseActionsSub Direction.Down p) |> ignore
        mouse.Up.Values.Subscribe(fun p -> mouseActionsSub Direction.Up p) |> ignore
        keyboard.Down.Values.Subscribe(fun k -> keysSub Direction.Down k) |> ignore
        keyboard.Up.Values.Subscribe(fun k -> keysSub Direction.Up k) |> ignore

        let mutable modSubscriptions = Dictionary<IMod<'msg>,IDisposable*System.Diagnostics.Stopwatch>()
        let fixupModRegistrations xs =
            let newAsSet = Dict.ofList xs
            let added = xs |> List.filter (fun (m,f) -> modSubscriptions.ContainsKey m |> not)
            let removed = modSubscriptions |> Seq.filter (fun (KeyValue(m,f)) -> newAsSet.ContainsKey m |> not) |> Seq.toList
            for (KeyValue(k,(d,sw))) in removed do
                d.Dispose()
            removed |> Seq.iter (fun (KeyValue(m,v)) -> modSubscriptions.Remove m |> ignore)
            for (m,f) in added do
                let sw = System.Diagnostics.Stopwatch()
                let d = m |> Mod.unsafeRegisterCallbackNoGcRoot (fun msg -> 
                    let elapsed = sw.Elapsed.TotalMilliseconds
                    if false then ()
                    else
                        if elapsed <= System.Double.Epsilon then () // recursion hack
                        else
                            sw.Restart()
                            match f elapsed msg with | Some nmsg -> onMessage model.Value nmsg |> ignore | None -> ()
                )
                sw.Start()
                modSubscriptions.Add(m, (d,sw))

        let updateSubscriptions (m : 'model) =
            if currentlyActive then
                let subs = app.subscriptions m
                moves <- Sub.filterMoves subs
                clicks <- Sub.filterMouseClicks subs
                mouseActions <- Sub.filterMouseThings subs
                keys <- Sub.filterKeys subs
                times <- Sub.filterTimes subs
                fixupModRegistrations (Sub.filterMods subs)
                enterTimes times
            ()

        let updateModel (m : 'model) =
            transact (fun () -> 
                model.Value <- m
                updateSubscriptions m |> ignore
                unpersist.apply m mmodel reuseCache
            )



        let mutable env = Unchecked.defaultof<_>

        let send msg =
            let m' = app.update env model.Value msg
            updateSubscriptions m'
            updateModel m'
            m'

        let emitEnv cmd = 
            match cmd with
                | NoCmd -> ()
                | Cmd cmd -> 
                    async {
                        let! msg = cmd
                        send msg |> ignore
                    } |> Async.Start

        env <- { run = emitEnv }

        onMessage <-
            match onMessageO with 
                | None -> (fun model msg -> let m' = app.update env model msg in updateModel m'; m')
                | Some v -> v

        let view = app.view mmodel
        let pickObjects = view.PickObjects()
        let pickReader = pickObjects.GetReader()


        let pick (pp : PixelPosition) : list<Ray3d*float*_> =
            pickReader.GetDelta() |> ignore
            let picks =
                pickReader.Content 
                    |> Seq.toList 
                    |> List.collect (fun p -> 
                        let r = Pick.pixel pp (Mod.force p.modeltrafo) (p.viewtrafo |> Mod.force) (p.projtrafo |> Mod.force)
                        Primitives.hitPrimitive p.primitive (Mod.force p.modeltrafo) r p.actions
                    )

            let rec depthTest xs =
                match xs with
                    | [] -> []
                    | (r1,d1,(f1,Solid))::           (r2,d2,(f2,Solid))::rest when d1 = d2 -> (r1,d1,(f1,Solid)) ::(r2,d2,(f2,Solid)) :: depthTest rest
                    | (r, d, (f,Solid))::_ ->       [(r, d, (f,Solid))]
                    | (r, d, (f,PickThrough))::xs -> (r, d, (f,PickThrough)) :: depthTest xs

            picks 
                |> List.collect (fun (ray,d,picks) -> picks |> List.map (fun p -> ray,d,p)) 
                |> List.sortBy snd'
                |> depthTest



        let updatePickMsg (m : GlobalPick) (model : 'model) =
            app.ofPickMsg model m |> List.fold onMessage model


        let handleMouseEvent mouseEvent =
            if currentlyActive then
                let picks = pick (mouse.Position |> Mod.force |> transformPixel)
                let mutable model = model.Value
                for (ray,d,(msg,transparency)) in picks do
                    let occ : PickOccurance = { mouse = mouseEvent; key = KeyEvent.NoEvent; point = ray.GetPointOnRay d; ray = ray }
                    match msg occ with
                        | Some r -> model <- onMessage model r
                        | _ -> ()
                updateModel model 

        mouse.Move.Values.Subscribe(fun (oldP,newP) ->
            let bounds = viewport |> Mod.force
            currentlyActive <- bounds.Contains newP.Position 
            handleMouseEvent MouseEvent.Move
        ) |> ignore

        mouse.Down.Values.Subscribe(fun p ->  
            handleMouseEvent (MouseEvent.Down p)
        ) |> ignore
 
        mouse.Click.Values.Subscribe(fun p ->  
            handleMouseEvent (MouseEvent.Click p)
        ) |> ignore

        mouse.Up.Values.Subscribe(fun p ->     
            handleMouseEvent (MouseEvent.Up p)
        ) |> ignore




        let handleKeyEvent (key : KeyEvent) =
            if currentlyActive then
                let picks = pick (mouse.Position |> Mod.force |> transformPixel)
                let mutable model = model.Value
                for (ray,d,(msg,transparency)) in picks do
                    let occ : PickOccurance = { mouse = MouseEvent.NoEvent; key = key; point = ray.GetPointOnRay d; ray = ray }
                    match msg occ with
                        | Some r -> model <- onMessage model r
                        | _ -> ()
                updateModel model 


        keyboard.KeyUp(altKey).Values.Subscribe( fun k ->
            handleKeyEvent (KeyEvent.Up altKey)
        ) |> ignore
        keyboard.KeyDown(altKey).Values.Subscribe( fun k ->
            handleKeyEvent (KeyEvent.Down altKey)
        ) |> ignore

        keyboard.KeyUp(ctrlKey).Values.Subscribe( fun k ->
            handleKeyEvent (KeyEvent.Up ctrlKey)
        ) |> ignore
        keyboard.KeyDown(ctrlKey).Values.Subscribe( fun k ->
            handleKeyEvent (KeyEvent.Down ctrlKey)
        ) |> ignore

        keyboard.KeyUp(shiftKey).Values.Subscribe( fun k ->
            handleKeyEvent (KeyEvent.Up shiftKey)
        ) |> ignore
        keyboard.KeyDown(shiftKey).Values.Subscribe( fun k ->
            handleKeyEvent (KeyEvent.Down shiftKey)
        ) |> ignore




        { send = send; sg = view :> ISg; emitModel = updateModel }

    let inline createAppAdaptiveD (keyboard : IKeyboard) (mouse : IMouse) (viewport : IMod<Box2i>) (camera : IMod<Camera>) (onMessage : Option<Fablish.CommonTypes.Callback<'model,'msg>>) (app : App<'model,'mmodel,'msg, ISg<'msg>>)=
        createAppAdaptive keyboard mouse viewport camera ( unpersist ()) onMessage app


