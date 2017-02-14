namespace Aardvark.UI

open System.Text
open System.Collections.Generic
open System.Threading
open Aardvark.Base
open Aardvark.Base.Incremental

type AttributeValue<'msg> =
    | Event of list<string> * (list<string> -> 'msg)
    | Value of string


and Node<'msg>(tag : string, attributes : amap<string, AttributeValue<'msg>>, content : Either<alist<Node<'msg>>, IMod<string>>) =
    member x.Tag = tag
    member x.Attributes = attributes
    member x.Content = content

    new(tag : string, attributes : amap<string, AttributeValue<'msg>>, content : alist<Node<'msg>>) =
        Node(tag, attributes, Left content)

    new(tag : string, attributes : amap<string, AttributeValue<'msg>>, content : IMod<string>) =
        Node(tag, attributes, Right content)

[<AutoOpen>]
module ``Extensions for StringBuilder`` = 
    type StringBuilder with
        member x.append fmt = Printf.kprintf (fun str -> x.AppendLine str |> ignore) fmt

type UpdateState<'msg> =
    {
        code        : StringBuilder
        handlers    : Dictionary<string * string, list<string> -> 'msg>
    }

type ReaderNode<'msg>(node : Node<'msg>) =
    inherit AdaptiveObject()

    static let mutable currentId = 0
    static let newId() =
        let id = Interlocked.Increment(&currentId)
        "n" + string id
        
    let id = newId()
    let attributes = node.Attributes.ASet.GetReader()
    let content = 
        match node.Content with
            | Left children -> (AList.map ReaderNode children).GetReader() |> Left
            | Right m -> Right m 

    let mutable lastContent = ""

    let greatestSmaller (c : 'k) (s : seq<'k * 'a>) =
        let mutable res = None
        for (k,v) in s do
            if k < c then
                match res with
                    | Some(o,_) when o >= k ->
                        ()
                    | _ -> 
                        res <- Some (k,v)

        res

    member x.Id = id
    member x.Tag = node.Tag


    member x.Destroy(state : UpdateState<'msg>) =
        for (key, v) in attributes.Content do
            match v with
                | Event _ ->
                    state.handlers.Remove((id, key)) |> ignore
                | _ ->
                    ()

        attributes.Dispose()


        match content with
            | Left children ->
                for (_,c) in children.Content.All do
                    c.Destroy(state)

                children.Dispose()
            | _ ->
                lastContent <- ""


    member x.Update(caller : IAdaptiveObject, state : UpdateState<'msg>) =
        x.EvaluateIfNeeded caller () (fun () ->
            let code : StringBuilder = state.code
            let id =
                lazy (
                    code.append "var %s = $('#%s');" id id
                    id
                )

 
            match content with
                | Left children -> 
                    for d in children.GetDelta x do
                        match d with
                            | Add(key, n) ->
                                let prev = children.Content.All |> greatestSmaller key

                                match prev with
                                    | Some (_,prev) ->
                                        code.append "$('<%s/>').attr('id', '%s').insertAfter($('#%s'));" n.Tag n.Id prev.Id

                                    | _ ->
                                        code.append "%s.prepend($('<%s/>').attr('id', '%s'));" id.Value n.Tag n.Id

                            | Rem(_,n) ->
                                // delete the element
                                code.append "$('#%s').remove();" n.Id

                                n.Destroy(state)

                    for (_,c) in children.Content.All do
                        c.Update(x, state)

                | Right value ->
                    let value = value.GetValue x
                    if lastContent <> value then
                        lastContent <- value
                        code.append "%s.html('%s');" id.Value value


            let values = Dictionary()
            let removed = Dictionary()
            for d in attributes.GetDelta x do
                match d with
                    | Add(key, value) ->
                        removed.Remove key |> ignore
                        values.[key] <- value

                    | Rem(key, value) ->
                        if not (values.ContainsKey key) then
                            removed.[key] <- value


            for (key, value) in Dictionary.toSeq values do
                match value with
                    | Value str -> 
                        code.append "%s.attr('%s', '%s');" id.Value key str

                    | Event(args, f) ->
                        let args = sprintf "'%s'" id.Value :: sprintf "'%s'" key :: args
                        let args = String.concat ", " args
                        code.append "%s.attr('%s', \"aardvark.processEvent(%s)\");" id.Value key args
                        state.handlers.[(id.Value, key)] <- f

            for (key, value) in Dictionary.toSeq removed do
                code.append "%s.removeAttr('%s');" id.Value key
                match value with
                    | Event _ -> state.handlers.Remove((id.Value, key)) |> ignore
                    | _ -> ()


            ()
        )


[<AutoOpen>]
module ``Extensions for Node`` =
    type Node<'msg> with
        member x.GetReader() =
            ReaderNode(x)


module Bla =
    type Model =
        {
            elements : list<string>
        }

    type MModel =
        {
            elements : corderedset<ModRef<string>>
        }

    type Change = Transaction -> unit
        
    type Message = list<Change>


    type Action =
        | RemoveButton of int

    let update (m : Model) (msg : Action) =
        match msg with
            | RemoveButton n ->
                { m with elements = m.elements |> List.mapi (fun ii b -> if ii = n then None else Some b) |> List.choose id }


    let mutable currentModel = Mod.init { Model.elements = [] }
    let unpersist (mm : MModel) (m : Model)  =
        currentModel.Value <- m
        ()

    // let nm = RemoveButton 0 |> update m 
    // unpersist mm ({ m with elements = m.elements |> List.mapi (fun ii b -> if ii = 0 then None else Some b) |> List.choose id })
    // m <- nm 

    let test() =
        
        let click = CMap.ofList ["onclick", Event([], fun _ -> 5)]
        
        let hi = Mod.init "Hi"
        let elements = COrderedSet.ofList [hi]

        let globalSideEffect = List<ref<Model>>()

        let view (m : MModel) =
            let hugo = Mod.init false
            Node(
                "div",
                AMap.empty,
                AList.ofList [
                    Node(
                        "textarea",
                        CMap.ofList ["onchange", Event([], fun _ -> [fun t -> hugo.Value <- not hugo.Value])], 
                        AList.ofList [ Node("span", AMap.empty, hugo |> Mod.map string) ]
                    )
                ]
            )



        let ui =
            Node(
                "div",
                AMap.empty,
                AList.ofList [
                    Node(
                        "div",
                        AMap.empty,
                        elements |> AList.map (fun name -> 
                            let i = failwith ""
                            Node(
                                "button", 
                                CMap.ofList ["onclick", Event([], fun _ -> RemoveButton i)], 
                                AList.ofList [ Node("span", AMap.empty, name) ]
                            )
                        )
                    )
                ]
            )




        let ui =
            Node(
                "div",
                AMap.empty,
                AList.ofList [
                    Node(
                        "div",
                        AMap.empty,
                        elements |> AList.map (fun name -> 
                            Node(
                                "button", 
                                CMap.ofList ["onclick", Event([], fun _ -> elements.Remove name |> ignore)], 
                                AList.ofList [ Node("span", AMap.empty, name) ]
                            )
                        )
                    )
                ]
            )


        let r = ui.GetReader()


        let state =
            {
                code = StringBuilder()
                handlers = Dictionary()
            }

        printfn "initial"
        r.Update(null, state)
        printfn "%s" (state.code.ToString())
        printfn "%A" (state.handlers |> Dictionary.toList)

        state.code.Clear() |> ignore



        transact (fun () ->
            elements.Add (Mod.init "Sepp") |> ignore
        )
        printfn ""
        printfn "update"
        r.Update(null, state)
        printfn "%s" (state.code.ToString())
        printfn "%A" (state.handlers |> Dictionary.toList)
        state.code.Clear() |> ignore



        transact (fun () ->
            hi.Value <- "Heinzi"
        )
        printfn ""
        printfn "update"
        r.Update(null, state)
        printfn "%s" (state.code.ToString())
        printfn "%A" (state.handlers |> Dictionary.toList)
        state.code.Clear() |> ignore


        transact (fun () ->
            elements.Remove hi |> ignore
        )
        printfn ""
        printfn "update"
        r.Update(null, state)
        printfn "%s" (state.code.ToString())
        printfn "%A" (state.handlers |> Dictionary.toList)
        state.code.Clear() |> ignore



        transact (fun () ->
            click.Remove "onclick" |> ignore
        )
        printfn ""
        printfn "update"
        r.Update(null, state)
        printfn "%s" (state.code.ToString())
        printfn "%A" (state.handlers |> Dictionary.toList)
        state.code.Clear() |> ignore

        ()






        



