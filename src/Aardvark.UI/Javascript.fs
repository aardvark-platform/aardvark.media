namespace Aardvark.UI

open System
open System.Threading
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Application

type JSVar = { name : string }

type JSExpr =
    | Body
    | CreateElement of tag : string * ns : Option<string>
    | SetAttribute of target : JSExpr * name : string * value : string
    | RemoveAttribute of target : JSExpr * name : string

    | Remove of target : JSExpr
    | InnerText of target : JSExpr * text : string 

    | Replace of oldElement : JSExpr * newElement : JSExpr
    | AppendChild  of parent : JSExpr * inner : JSExpr
    | InsertBefore of reference : JSExpr * inner : JSExpr // in html arguments switched
    | InsertAfter of reference : JSExpr * inner : JSExpr

    | Raw of code : string
    | Sequential of list<JSExpr>
    | GetElementById of string
    | Let of JSVar * JSExpr * JSExpr
    | Var of JSVar
    | Nop
    
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module JSExpr =
    open Aardvark.Base.Monads.State
    
    type UsedState =
        {
            usedVariables : Set<string>
        }
        
    let rx = System.Text.RegularExpressions.Regex "\\\"|\\\\"

    let escape (str : string) =
        rx.Replace(str, fun m ->
            if m.Value = "\"" then "\\\""
            else "\\\\"
        )

    let rec eliminateDeadBindings (e : JSExpr) : State<Set<JSVar>, JSExpr> =
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
                code

            | Body ->
                "document.body"

            | Nop ->
                ""

            | CreateElement(tag,ns) ->
                match ns with
                    | None -> 
                        sprintf "document.createElement(\"%s\")" tag
                    | Some ns -> 
                        sprintf "document.createElementNS(\"%s\", \"%s\")" ns tag 

            | SetAttribute(t, name, value) ->
                let t = toStringInternal t
                sprintf "aardvark.setAttribute(%s,\"%s\", \"%s\");" t name (escape value)

            | RemoveAttribute(t, name) ->
                let t = toStringInternal t
                sprintf "%s.removeAttribute(\"%s\");" t name

            | GetElementById(id) ->
                sprintf "document.getElementById(\"%s\")" id

            | Let(var, value, body) ->
                match toStringInternal body  with
                    | "" -> ""
                    | body -> sprintf "var %s = %s;\r\n%s" var.name (toStringInternal value) body

            | Sequential all ->
                match all |> List.map toStringInternal |> List.filter (fun str -> str <> "") with
                    | [] -> ""
                    | l -> l |> String.concat "\r\n"

            | InnerText(target,text) -> 
                let o = text |> System.Web.HttpUtility.JavaScriptStringEncode
                sprintf "%s.textContent = \"%s\";" (toStringInternal target) o

            | AppendChild(parent,inner) -> 
                let parent = toStringInternal parent
                sprintf "%s.appendChild(%s);" parent (toStringInternal inner)
  
            | InsertBefore(reference,element) -> 
                match reference with
                    | Var v -> 
                        sprintf "%s.parentElement.insertBefore(%s,%s);" v.name (toStringInternal element) v.name
                    | _ ->
                        let ref = toStringInternal reference
                        sprintf "var _temp = %s;\r\n_temp.parentElement.insertBefore(%s,_temp);" ref (toStringInternal element)

            | InsertAfter(reference,element) -> 
                let name, binding =
                    match reference with
                        | Var v -> v.name, ""
                        | v -> "_temp", sprintf "var _temp = %s;\r\n" (toStringInternal v)

                binding +
                sprintf "if(!%s.nextElementSibling) %s.parentElement.appendChild(%s);\r\n" name name (toStringInternal element) +
                sprintf "else %s.parentElement.insertBefore(%s, %s.nextElementSibling);" name (toStringInternal element) name

            | Replace(o,n) ->
                let ref = toStringInternal o
                sprintf "%s.parentElement.replaceChild(%s, %s);" ref (toStringInternal n) ref
             
            | Var v ->
                v.name

            | Remove e ->
                sprintf "%s.remove();" (toStringInternal e)


    let toString (e : JSExpr) =
        let e = eliminateDeadBindings e
        let mutable state = Set.empty
        let e = e.Run(&state)
        toStringInternal e
//
//
//type Html =
//    | Node of string * list<string*string> * list<Html>
//    | Text of string
//    | Script of JSExpr