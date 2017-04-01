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
    | CreateElement of tag : string
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

    let rx = System.Text.RegularExpressions.Regex "\\\"|\\\\"

    let escape (str : string) =
        rx.Replace(str, fun m ->
            if m.Value = "\"" then "\\\""
            else "\\\\"
        )
        
    let rec toJQueryString (e : JSExpr) =
        match e with
            //| AddReferences map ->
            //    let args = map |> Map.toSeq |> Seq.map (fun (n,u) -> sprintf "{ name: \"%s\", url: \"%s\"}" n u) |> String.concat "," |> sprintf "[%s]"
            //    sprintf "aardvark.addReferences(%s);" args

            | Raw code ->
                code

            | Body ->
                "$(document.body)"

            | Nop ->
                ""

            | CreateElement(tag) ->
                sprintf "$([document.createElement(\"%s\")])" tag

            | SetAttribute(t, name, value) ->
                let t = toJQueryString t
                sprintf "%s.attr(\"%s\", \"%s\");" t name (escape value)

            | RemoveAttribute(t, name) ->
                let t = toJQueryString t
                sprintf "%s.removeAttr(\"%s\");" t name

            | GetElementById(id) ->
                sprintf "$(\"#%s\")" id

            | Let(var, value, body) ->
                match toJQueryString body  with
                    | "" -> ""
                    | body -> sprintf "var %s = %s;\r\n%s" var.name (toJQueryString value) body

            | Sequential all ->
                match all |> List.map toJQueryString |> List.filter (fun str -> str <> "") with
                    | [] -> ""
                    | l -> l |> String.concat "\r\n"

            | InnerText(target,text) -> 
                sprintf "%s.html(\"%s\");" (toJQueryString target) (escape text)

            | AppendChild(parent,inner) -> 
                let parent = toJQueryString parent
                sprintf "%s.append(%s);" parent (toJQueryString inner)

            | InsertBefore(reference,element) -> 
                let ref = toJQueryString reference
                sprintf "%s.insertBefore(%s);" (toJQueryString element) ref

            | InsertAfter(reference,element) -> 
                let ref = toJQueryString reference
                sprintf "%s.insertAfter(%s);" (toJQueryString element) ref

            | Replace(o,n) ->
                let ref = toJQueryString o
                sprintf "%s.replaceWith(%s);" ref (toJQueryString n)

            | Var v ->
                v.name

            | Remove e ->
                sprintf "%s.remove();" (toJQueryString e)

    let rec toString (e : JSExpr) =
        match e with
            //| AddReferences map ->
            //    let args = map |> Map.toSeq |> Seq.map (fun (n,u) -> sprintf "{ name: \"%s\", url: \"%s\"}" n u) |> String.concat "," |> sprintf "[%s]"
            //    sprintf "aardvark.addReferences(%s);" args

            | Raw code ->
                code

            | Body ->
                "document.body"

            | Nop ->
                ""

            | CreateElement(tag) ->
                sprintf "document.createElement(\"%s\")" tag

            | SetAttribute(t, name, value) ->
                let t = toString t
                sprintf "%s.setAttribute(\"%s\", \"%s\");" t name (escape value)

            | RemoveAttribute(t, name) ->
                let t = toString t
                sprintf "%s.removeAttribute(\"%s\");" t name

            | GetElementById(id) ->
                sprintf "document.getElementById(\"%s\")" id

            | Let(var, value, body) ->
                match toString body  with
                    | "" -> ""
                    | body -> sprintf "var %s = %s;\r\n%s" var.name (toString value) body

            | Sequential all ->
                match all |> List.map toString |> List.filter (fun str -> str <> "") with
                    | [] -> ""
                    | l -> l |> String.concat "\r\n"

            | InnerText(target,text) -> 
                sprintf "%s.innerText = \"%s\";" (toString target) (escape text)

            | AppendChild(parent,inner) -> 
                let parent = toString parent
                sprintf "%s.appendChild(%s);" parent (toString inner)

            | InsertBefore(reference,element) -> 
                match reference with
                    | Var v -> 
                        sprintf "%s.parentElement.insertBefore(%s,%s);" v.name (toString element) v.name
                    | _ ->
                        let ref = toString reference
                        sprintf "var _temp = %s;\r\n_temp.parentElement.insertBefore(%s,_temp);" ref (toString element)

            | InsertAfter(reference,element) -> 
                let name, binding =
                    match reference with
                        | Var v -> v.name, ""
                        | v -> "_temp", sprintf "var _temp = %s;\r\n" (toString v)

                binding +
                sprintf "if(!%s.nextElementSibling) %s.parentElement.appendChild(%s);\r\n" name name (toString element) +
                sprintf "else %s.parentElement.insertBefore(%s, %s.nextElementSibling);" name (toString element) name

            | Replace(o,n) ->
                let ref = toString o
                sprintf "%s.parentElement.replaceChild(%s, %s);" ref (toString n) ref

            | Var v ->
                v.name

            | Remove e ->
                sprintf "%s.remove();" (toString e)