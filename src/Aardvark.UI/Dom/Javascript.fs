namespace Aardvark.UI

open System

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
    | InsertBefore    of reference : JSExpr * inner : JSExpr // in html arguments switched
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

    let rx = System.Text.RegularExpressions.Regex "\\\"|\\\\"

    let escape (str : string) =
        rx.Replace(str, fun m ->
            if m.Value = "\"" then "\\\""
            else "\\\\"
        )

    let rec eliminateDeadBindings (e : JSExpr) : State<Set<string>, JSExpr> =
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
                    let! e = withoutValue e
                    return Sequential [e; b]

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

    and withoutValue (e : JSExpr) =
        state {
            return Nop
        }

    let rec toStringInternal (e : JSExpr) =
        match e with
        | Raw code ->
            "(function() { " + code + "})();"

        | Body ->
            "document.body"

        | Nop ->
            ""

        | CreateElement(tag, ns) ->
            if String.IsNullOrEmpty ns then
                $"document.createElement(\"{tag}\")"
            else
                $"document.createElementNS(\"{ns}\", \"{tag}\")"

        | SetAttribute(t, name, value) ->
            let t = toStringInternal t
            $"aardvark.setAttribute({t}, \"{name}\", \"{escape value}\");"

        | RemoveAttribute(t, name) ->
            let t = toStringInternal t
            $"{t}.removeAttribute(\"{name}\");"

        | SetEventHandler(t, name, version) ->
            let t = toStringInternal t
            $"aardvark.setEventHandler(\"{t}\", \"{name}\", {version});"

        | GetElementById(id) ->
            $"document.getElementById(\"{id}\")"

        | Let(var, value, body) ->
            match toStringInternal body  with
            | "" -> ""
            | body -> $"var {var} = {toStringInternal value};\r\n{body}"

        | Sequential all ->
            match all |> List.map toStringInternal |> List.filter (fun str -> str <> "") with
            | [] -> ""
            | l -> l |> String.concat "\r\n"

        | InnerText(target, text) ->
            let o = text |> System.Web.HttpUtility.JavaScriptStringEncode
            let target = toStringInternal target
            $"{target}.textContent = \"{o}\";"

        | AppendChild(parent, inner) ->
            let parent = toStringInternal parent
            let inner = toStringInternal inner
            $"{parent}.appendChild({inner});"

        | InsertBefore(reference, element) ->
            let element = toStringInternal element

            match reference with
            | Var v ->
                $"{v}.parentElement.insertBefore({element}, {v});"
            | _ ->
                let reference = toStringInternal reference
                $"var _temp = {reference};\r\n_temp.parentElement.insertBefore({element}, _temp);"

        | InsertAfter(reference, element) ->
            let name, binding =
                match reference with
                | Var v -> v, ""
                | v -> "_temp", $"var _temp = {toStringInternal v};\r\n"

            let element = toStringInternal element

            binding +
            $"if(!{name}.nextElementSibling) {name}.parentElement.appendChild({element});\r\n" +
            $"else {name}.parentElement.insertBefore({element}, {name}.nextElementSibling);"

        | Replace(o, n) ->
            let o = toStringInternal o
            let n = toStringInternal n
            $"{o}.parentElement.replaceChild({n}, {o});"

        | Var v ->
            v

        | Remove e ->
            let e = toStringInternal e
            $"{e}.remove();"

    let toString (e : JSExpr) =
        let e = eliminateDeadBindings e
        let mutable state = Set.empty
        let e = e.Run(&state)
        toStringInternal e