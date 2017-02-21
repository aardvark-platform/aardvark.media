open Aardvark.Service

open System

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms

[<AutoOpen>]
module GCRef =
    open System.Collections.Concurrent
    open System.Collections.Generic
    
    type GCEntry<'a> =
        struct
            val mutable public Value : 'a
            val mutable public Next : int

            new(value : 'a, next : int) = { Value = value; Next = next }
        end

    type GCRef<'a when 'a :> IDisposable> private(id : int) =
        
        static let lockObj = obj()
        static let mutable table : GCEntry<'a>[] = Array.zeroCreate 1024
        static let mutable count = 0
        static let mutable free = 0

        static let rec add (value : 'a) =
            lock lockObj (fun () ->
                if free > 0 then
                    let id = free - 1
                    table.[id].Value <- value
                    free <- table.[id].Next
                    id
                else
                    if count < table.Length then
                        let id = count
                        table.[id].Value <- value
                        count <- id + 1
                        id 
                    else
                        Array.Resize(&table, 2 * table.Length)
                        add value
            )

        static let remove (id : int) =
            lock lockObj (fun () ->
                let o = table.[id].Value
                o.Dispose()
                table.[id] <- GCEntry(Unchecked.defaultof<_>, free)
                free <- id + 1
            )

        member x.Value =
            table.[id].Value

        override x.Finalize() =
            remove id

        new(value : 'a) = GCRef<'a>(add value)

module SimpleOrder =
    
    [<AllowNullLiteral>]
    type SortKey =
        class
            val mutable public Clock : Order
            val mutable public Tag : uint64
            val mutable public Next : SortKey
            val mutable public Prev : SortKey

            member x.Time =
                x.Tag - x.Clock.Root.Tag

            member x.CompareTo (o : SortKey) =
                if isNull o.Next || isNull x.Next then
                    failwith "cannot compare deleted times"

                if o.Clock <> x.Clock then
                    failwith "cannot compare times from different clocks"

                compare x.Time o.Time

            interface IComparable with
                member x.CompareTo o =
                    match o with
                        | :? SortKey as o -> x.CompareTo(o)
                        | _ -> failwithf "cannot compare time with %A" o

            interface IComparable<ISortKey> with
                member x.CompareTo o =
                    match o with
                        | :? SortKey as o -> x.CompareTo o
                        | _ -> failwithf "cannot compare time with %A" o

            interface IDisposable with
                member x.Dispose() =
                    x.Clock.Delete x

            override x.GetHashCode() = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(x)
            override x.Equals o = System.Object.ReferenceEquals(x,o)

            interface ISortKey with
                member x.Clock = x.Clock :> IOrder
                member x.IsDeleted = isNull x.Next
                //member x.Next = x.Next :> ISortKey

            new(c) = { Clock = c; Tag = 0UL; Next = null; Prev = null }
        end

    and Order =
        class
            val mutable public Root : SortKey
            val mutable public Count : int

            member x.After (t : SortKey) =
                if t.Clock <> x then
                    failwith "cannot insert after a different clock's time"

                let distance (a : SortKey) (b : SortKey) =
                    if a = b then System.UInt64.MaxValue
                    else b.Tag - a.Tag

                let mutable dn = distance t t.Next

                // if the distance to the next time is 1 (no room)
                // relabel all times s.t. the new one can be inserted
                if dn = 1UL then
                    // find a range s.t. distance(range) >= 1 + |range|^2 
                    let mutable current = t.Next
                    let mutable j = 1UL
                    while distance t current < 1UL + j * j do
                        current <- current.Next
                        j <- j + 1UL

                    // distribute all times in the range equally spaced
                    let step = (distance t current) / j
                    current <- t.Next
                    let mutable currentTime = t.Tag + step
                    for k in 1UL..(j-1UL) do
                        current.Tag <- currentTime
                        current <- current.Next
                        currentTime <- currentTime + step

                    // store the distance to the next time
                    dn <- step

                // insert the new time with distance (dn / 2) after
                // the given one (there has to be enough room now)
                let res = new SortKey(x)
                res.Tag <- t.Tag + dn / 2UL

                res.Next <- t.Next
                res.Prev <- t
                t.Next.Prev <- res
                t.Next <- res

                res

            member x.Before (t : SortKey) =
                if t = x.Root then
                    failwith "cannot insert before root-time"
                x.After t.Prev

            member x.Delete (t : SortKey) =
                if not (isNull t.Next) then
                    if t.Clock <> x then
                        failwith "cannot delete time from different clock"

                    t.Prev.Next <- t.Next
                    t.Next.Prev <- t.Prev
                    t.Next <- null
                    t.Prev <- null
                    t.Tag <- 0UL
                    t.Clock <- Unchecked.defaultof<_>      

            member x.Clear() =
                let r = new SortKey(x)
                x.Root <- r
                r.Next <- r
                r.Prev <- r
                x.Count <- 1

            interface IOrder with
                member x.Root = x.Root :> ISortKey
                member x.Count = x.Count

            static member New() =
                let c = Order()
                let r = new SortKey(c)
                c.Root <- r
                r.Next <- r
                r.Prev <- r
                c

            private new() = { Root = null; Count = 1 }
        end

    let create() =
        Order.New()


[<EntryPoint>]
let main args =

    let set = cset<int> [1; 2]

    let test = 
        set |> ASet.collect (fun v ->
            ASet.ofList [ v; 2 * v ]
        )

    let print (r : ISetReader<'a>) =
        let ops = r.GetOperations null
        Log.line "state: %A" r.State
        Log.line "ops:   %A" ops

    let r = test.GetReader()
    print r

    transact (fun () -> set.Remove 2 |> ignore)
    print r

    transact (fun () -> set.Add 3 |> ignore; set.Remove 1 |> ignore)
    print r

    transact (fun () -> set.Add 2 |> ignore; set.Add 0 |> ignore)
    print r

    Environment.Exit 0

    let test() =
        let o = SimpleOrder.create()

        let a = o.After(o.Root) |> GCRef
        let b = o.After(a.Value) |> GCRef
        let c = o.After(b.Value) |> GCRef
        let d = o.After(c.Value) |> GCRef



        (a,d)

    let (a,b) = test()
    System.GC.Collect()
    System.GC.WaitForFullGCComplete() |> ignore
    System.GC.WaitForFullGCComplete() |> ignore

    printfn "all dead: %A" (a.Value.Next = b.Value)
    Environment.Exit 0

    Ag.initialize()
    Aardvark.Init()





    use app = new OpenGlApplication()
    let runtime = app.Runtime




    Aardvark.UI.Bla.TestApp.start runtime 8888
//    Server.start runtime 8888 [] (fun id yeah ->
//        let view = CameraView.lookAt (V3d.III * 6.0) V3d.Zero V3d.OOI
//        let proj = yeah.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))
//
//        let view =
//            view |> DefaultCameraController.control yeah.Mouse yeah.Keyboard yeah.Time
//
//        let trafo = yeah.Time |> Mod.map (fun dt -> Trafo3d.RotationZ(float dt.Ticks / float TimeSpan.TicksPerSecond))
//
//        let sg =
//            Sg.box' C4b.Red (Box3d(-V3d.III, V3d.III))
//                |> Sg.trafo trafo
//                |> Sg.viewTrafo (view |> Mod.map CameraView.viewTrafo)
//                |> Sg.projTrafo (proj |> Mod.map Frustum.projTrafo)
//                |> Sg.diffuseFileTexture' @"E:\Development\WorkDirectory\DataSVN\cliffs_color.jpg" true
//                |> Sg.shader {
//                    do! DefaultSurfaces.trafo
//                    do! DefaultSurfaces.diffuseTexture
//                    do! DefaultSurfaces.simpleLighting
//                }
//
//
//        let task = runtime.CompileRender(yeah.FramebufferSignature, sg)
//        Some task
//    )

    Console.ReadLine() |> ignore
    0

