namespace Aardvark.UI.Tests

open System
open System.Collections
open System.Collections.Generic
open System.Reflection
open Microsoft.FSharp.Reflection
open Aardvark.UI
open FSharp.Data.Adaptive
open Expecto

type private NativeCommand = Aardvark.SceneGraph.RenderCommand
type private NativeScene = Aardvark.SceneGraph.ISg

module ``RenderCommand Tests`` =
    // Inspect collection construction only; no render objects, runtime or GPU are used.
    let private fields (command: NativeCommand) =
        FSharpValue.GetUnionFields(command, typeof<NativeCommand>, BindingFlags.Public ||| BindingFlags.NonPublic)

    let private scenesOf command =
        let case, values = fields command
        match case.Name with
        | "REmpty" -> []
        | "RUnorderedScenes" ->
            (unbox<aset<NativeScene>> values.[0]).Content |> AVal.force |> HashSet.toList
            |> List.map (fun scene -> scene :?> ISg<unit>)
        | name -> failtestf "Expected scene collection, got %s" name

    let private commandsOf (command: RenderCommand<unit>) =
        let case, values = fields command.Command
        match case.Name with
        | "REmpty" -> []
        | "ROrdered" -> (unbox<alist<NativeCommand>> values.[0]).Content |> AVal.force |> IndexList.toList
        | name -> failtestf "Expected ordered collection, got %s" name

    type private Constructor<'T> =
        { Name: string
          Create: seq<'T> -> RenderCommand<unit>
          Read: RenderCommand<unit> -> 'T list
          Identity: 'T -> obj
          Ordered: bool }

    let private sceneConstructors : Constructor<ISg<unit>> list = [
        { Name = "Unordered scenes"; Create = RenderCommand<unit>.Unordered
          Read = fun command -> scenesOf command.Command
          Identity = box; Ordered = false }
        { Name = "Ordered scenes"; Create = RenderCommand<unit>.Ordered
          Read = fun command -> commandsOf command |> List.map (fun child -> scenesOf child |> List.exactlyOne)
          Identity = box; Ordered = true }
    ]

    let private commandConstructor : Constructor<RenderCommand<unit>> =
        { Name = "Ordered commands"; Create = RenderCommand<unit>.Ordered
          Read = fun command -> commandsOf command |> List.map (fun child -> RenderCommand<unit>(child))
          Identity = fun command -> box command.Command
          Ordered = true }

    let private newScene () : ISg<unit> = Sg.Set<unit>(ASet.empty) :> ISg<unit>
    let private scenes = List.init 3 (fun _ -> newScene())
    let private commands = scenes |> List.map RenderCommand<unit>.Render

    let private checkItems (constructor: Constructor<'T>) (expected: 'T list) (actual: 'T list) =
        if constructor.Ordered then
            Expect.equal actual.Length expected.Length (constructor.Name + ": count")
            List.iter2 (fun expected actual ->
                Expect.isTrue (obj.ReferenceEquals(constructor.Identity expected, constructor.Identity actual))
                    (constructor.Name + ": ordered input identity")) expected actual
        else
            let expected = expected |> List.distinctBy constructor.Identity
            Expect.equal actual.Length expected.Length (constructor.Name + ": deduplicated count")
            for value in expected do
                Expect.isTrue (actual |> List.exists (fun item -> obj.ReferenceEquals(constructor.Identity value, constructor.Identity item)))
                    (constructor.Name + ": retained scene identity")

    type private Observations =
        { mutable Enumerations: int
          mutable Disposals: int
          Evaluated: ResizeArray<int> }

    let private tracked (values: 'T list) singleUse failAt =
        let observed = { Enumerations = 0; Disposals = 0; Evaluated = ResizeArray() }
        let error = InvalidOperationException("source failure")
        let elements = seq {
            try
                for index, value in List.indexed values do
                    if failAt = Some index then raise error
                    observed.Evaluated.Add index
                    yield value
            finally
                observed.Disposals <- observed.Disposals + 1
        }
        let getEnumerator () =
            observed.Enumerations <- observed.Enumerations + 1
            if singleUse && observed.Enumerations > 1 then
                raise (InvalidOperationException("Second GetEnumerator call"))
            elements.GetEnumerator()
        let source =
            { new IEnumerable<'T> with
                member _.GetEnumerator() = getEnumerator()
              interface IEnumerable with
                member _.GetEnumerator() = getEnumerator() :> IEnumerator }
        source, observed, error

    let private checkTracked (constructor: Constructor<'T>) (values: 'T list) singleUse =
        let source, observed, _ = tracked values singleUse None
        let command = constructor.Create source
        let evaluatedAtConstruction = List.ofSeq observed.Evaluated
        let disposedAtConstruction = observed.Disposals
        let actual = constructor.Read command
        checkItems constructor values actual
        Expect.equal evaluatedAtConstruction [0 .. values.Length - 1] "Evaluate every element once at construction"
        Expect.equal disposedAtConstruction 1 "Dispose before construction returns"
        for _ in 1 .. 2 do checkItems constructor values (constructor.Read command)
        Expect.equal observed.Enumerations 1 "One GetEnumerator call"
        Expect.equal (List.ofSeq observed.Evaluated) [0 .. values.Length - 1] "No repeated element effects"
        Expect.equal observed.Disposals 1 "One disposal"

    let private suite (constructor: Constructor<'T>) (values: 'T list) =
        testList constructor.Name [
            test "Consuming sequences retain every input" {
                let remaining = Queue<_>(values)
                let source = seq { while remaining.Count > 0 do yield remaining.Dequeue() }
                let command = constructor.Create source
                let remainingAtConstruction = remaining.Count
                let actual = constructor.Read command
                checkItems constructor values actual
                Expect.equal remainingAtConstruction 0 "Finite input is materialized during construction"
                for _ in 1 .. 2 do checkItems constructor values (constructor.Read command)
                Expect.equal remaining.Count 0 "Content reads must not revisit the source"
            }

            test "Single-use sources require only one enumerator" {
                checkTracked constructor values true
            }

            test "Element side effects run once and the enumerator is disposed" {
                checkTracked constructor values false
            }

            test "Empty, singleton and duplicate inputs retain their collection semantics" {
                for input in [[]; [values.Head]; values @ [values.Head]] do
                    let source, observed, _ = tracked input true None
                    let command = constructor.Create source
                    let actual = constructor.Read command
                    checkItems constructor input actual
                    checkItems constructor input (constructor.Read command)
                    if input.IsEmpty then
                        Expect.isTrue (obj.ReferenceEquals(command.Command, NativeCommand.Empty)) "Canonical empty command"
                    Expect.equal observed.Enumerations 1 "Materialize even empty input only once"
                    Expect.equal observed.Disposals 1 "Release the input enumerator"
            }

            test "Source exceptions propagate at construction and dispose the enumerator" {
                for failAt in [0; 1] do
                    let source, observed, error = tracked values false (Some failAt)
                    let mutable constructed = false
                    let actual =
                        try
                            let command = constructor.Create source
                            constructed <- true
                            constructor.Read command |> ignore
                            null
                        with error -> error
                    Expect.isTrue (obj.ReferenceEquals(actual, error)) "Preserve source exception identity"
                    Expect.isFalse constructed "Finite source failures surface during construction"
                    Expect.equal observed.Enumerations 1 "Do not restart a failing input"
                    Expect.equal observed.Disposals 1 "Dispose after source failure"
            }
        ]

    [<Tests>]
    let tests =
        testList "RenderCommand Tests" [
            for constructor in sceneConstructors do yield suite constructor scenes
            yield suite commandConstructor commands

            yield test "List overloads preserve identity, order, duplicates and empty commands" {
                for input in [[]; [scenes.Head]; scenes @ [scenes.Head]] do
                    let unordered = RenderCommand<unit>.Unordered input
                    let ordered = RenderCommand<unit>.Ordered input
                    checkItems sceneConstructors.[0] input (sceneConstructors.[0].Read unordered)
                    checkItems sceneConstructors.[1] input (sceneConstructors.[1].Read ordered)
                    if input.IsEmpty then
                        Expect.isTrue (obj.ReferenceEquals(unordered.Command, NativeCommand.Empty)) "Empty scene list"
                        Expect.isTrue (obj.ReferenceEquals(ordered.Command, NativeCommand.Empty)) "Empty ordered scene list"
                for input in [[]; [commands.Head]; commands @ [commands.Head]] do
                    let command = RenderCommand<unit>.Ordered input
                    checkItems commandConstructor input (commandConstructor.Read command)
                    if input.IsEmpty then Expect.isTrue (obj.ReferenceEquals(command.Command, NativeCommand.Empty)) "Empty command list"
            }

            yield test "Adaptive overloads remain live from empty and populated inputs" {
                for initial in [[]; scenes] do
                    let set = cset initial
                    let list = clist initial
                    let initialCommands = initial |> List.map RenderCommand<unit>.Render
                    let commandList = clist initialCommands
                    let unordered = RenderCommand<unit>.Unordered(set :> aset<_>)
                    let orderedScenes = RenderCommand<unit>.Ordered(list :> alist<_>)
                    let orderedCommands = RenderCommand<unit>.Ordered(commandList :> alist<_>)
                    checkItems sceneConstructors.[0] initial (sceneConstructors.[0].Read unordered)
                    checkItems sceneConstructors.[1] initial (sceneConstructors.[1].Read orderedScenes)
                    checkItems commandConstructor initialCommands (commandConstructor.Read orderedCommands)
                    let added = newScene()
                    let addedCommand = RenderCommand<unit>.Render added
                    transact (fun () ->
                        set.Add added |> ignore
                        list.Add added |> ignore
                        commandList.Add addedCommand |> ignore)
                    checkItems sceneConstructors.[0] (initial @ [added]) (sceneConstructors.[0].Read unordered)
                    checkItems sceneConstructors.[1] (initial @ [added]) (sceneConstructors.[1].Read orderedScenes)
                    checkItems commandConstructor (initialCommands @ [addedCommand]) (commandConstructor.Read orderedCommands)
            }
        ]
