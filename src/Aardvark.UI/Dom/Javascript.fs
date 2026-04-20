namespace Aardvark.UI

open System
open System.Text

type internal JSExpr =
    | Body
    | CreateElement   of tag : string * ns : string
    | SetAttribute    of target : JSExpr * name : string * value : string
    | RemoveAttribute of target : JSExpr * name : string
    | SetEventHandler of target : JSExpr * name : string * version : byte
    | Remove          of target : JSExpr
    | InnerText       of target : JSExpr * text : string
    | Replace         of oldElement : JSExpr * newElement : JSExpr
    | AppendChild     of parent : JSExpr * inner : JSExpr
    | InsertBefore    of reference : JSExpr * inner : JSExpr // in HTML arguments switched
    | InsertAfter     of reference : JSExpr * inner : JSExpr
    | Raw             of code : string
    | Sequential      of JSExpr list
    | GetElementById  of string
    | Let             of var: string * expr: JSExpr * body: JSExpr
    | Var             of string
    | Nop

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal JSExpr =
    open Aardvark.Base.Monads.State

    let private escape =
        let rx = System.Text.RegularExpressions.Regex "\\\"|\\\\"

        fun (str: string) ->
            rx.Replace(str, fun m ->
                if m.Value = "\"" then "\\\""
                else "\\\\"
            )

    let rec private eliminateDeadBindings (e : JSExpr) : State<Set<string>, JSExpr> =
        state {
            match e with
            | Raw _ | Body | Nop | CreateElement _ | GetElementById _ ->
                return e

            | SetAttribute(t, name, value) ->
                let! t = eliminateDeadBindings t
                return SetAttribute(t, name, value)

            | RemoveAttribute(t, name) ->
                let! t = eliminateDeadBindings t
                return RemoveAttribute(t, name)

            | SetEventHandler(t, name, version) ->
                let! t = eliminateDeadBindings t
                return SetEventHandler(t, name, version)

            | Let(v,e,b) ->
                let! b = eliminateDeadBindings b
                let! s = State.get
                if Set.contains v s then
                    let! e = eliminateDeadBindings e
                    return Let(v, e, b)
                else
                    return b

            | Sequential children ->
                let! children = children |> List.rev |> List.mapS eliminateDeadBindings |> State.map List.rev
                return Sequential children

            | InnerText(t, text) ->
                let! t = eliminateDeadBindings t
                return InnerText(t, text)

            | AppendChild(t,c) ->
                let! c = eliminateDeadBindings c
                let! t = eliminateDeadBindings t
                return AppendChild(t,c)

            | InsertBefore(t,c) ->
                let! c = eliminateDeadBindings c
                let! t = eliminateDeadBindings t
                return InsertBefore(t,c)

            | InsertAfter(t,c) ->
                let! c = eliminateDeadBindings c
                let! t = eliminateDeadBindings t
                return InsertAfter(t,c)

            | Replace(t,c) ->
                let! c = eliminateDeadBindings c
                let! t = eliminateDeadBindings t
                return Replace(t,c)

            | Var v ->
                do! State.modify (Set.add v)
                return e

            | Remove e ->
                let! e = eliminateDeadBindings e
                return Remove e
        }

    let inline private (<<) (sb: StringBuilder) (str: string) =
        sb.Append str |> ignore

    let rec private toStringInternal (e : JSExpr) : string =
        let sb = StringBuilder()
        buildStringInternal sb e
        sb.ToString()

    and private buildStringInternal (sb : StringBuilder) (e : JSExpr) : unit =
        match e with
        | Raw code ->
            sb << $"(function() {{ {code} }})();"

        | Body ->
            sb << "document.body"

        | Nop ->
            ()

        | CreateElement(tag, ns) ->
            if String.IsNullOrEmpty ns then
                sb << $"document.createElement(\"{tag}\")"
            else
                sb << $"document.createElementNS(\"{ns}\", \"{tag}\")"

        | SetAttribute(t, name, value) ->
            sb << "aardvark.setAttribute("
            buildStringInternal sb t
            sb << $", \"{name}\", \"{escape value}\");"

        | RemoveAttribute(t, name) ->
            buildStringInternal sb t
            sb << $".removeAttribute(\"{name}\");"

        | SetEventHandler(t, name, version) ->
            sb << "aardvark.setEventHandler(\""
            buildStringInternal sb t
            sb << $"\", \"{name}\", {version});"

        | GetElementById(id) ->
            sb << $"document.getElementById(\"{id}\")"

        | Let(var, value, body) ->
            let body = toStringInternal body

            if body.Length > 0 then
                sb << $"var {var} = "
                buildStringInternal sb value
                sb << $"; {body}"

        | Sequential all ->
            for e in all do buildStringInternal sb e

        | InnerText(target, text) ->
            let o = text |> System.Web.HttpUtility.JavaScriptStringEncode
            buildStringInternal sb target
            sb << $".textContent = \"{o}\";"

        | AppendChild(parent, inner) ->
            buildStringInternal sb parent
            sb << ".appendChild("
            buildStringInternal sb inner
            sb << ");"

        | InsertBefore(reference, element) ->
            match reference with
            | Var v ->
                sb << $"{v}.parentElement.insertBefore("
                buildStringInternal sb element
                sb << $", {v});"
            | _ ->
                sb << "var _temp = "
                buildStringInternal sb reference
                sb << "; _temp.parentElement.insertBefore("
                buildStringInternal sb element
                sb << ", _temp);"

        | InsertAfter(reference, element) ->
            let element = toStringInternal element

            let add (name: string) =
                sb << $"if(!{name}.nextElementSibling) {name}.parentElement.appendChild({element}); "
                sb << $"else {name}.parentElement.insertBefore({element}, {name}.nextElementSibling);"

            match reference with
            | Var v -> add v
            | v ->
                sb << "var _temp = "
                buildStringInternal sb v
                sb << "; "
                add "_temp"

        | Replace(o, n) ->
            let o = toStringInternal o
            sb << $"{o}.parentElement.replaceChild("
            buildStringInternal sb n
            sb << $", {o});"

        | Var v ->
            sb << v

        | Remove e ->
            buildStringInternal sb e
            sb << ".remove();"

    let toString (e : JSExpr) =
        let e = eliminateDeadBindings e
        let mutable state = Set.empty
        let e = e.Run(&state)
        toStringInternal e