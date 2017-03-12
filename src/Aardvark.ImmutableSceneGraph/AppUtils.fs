namespace Scratch

open Aardvark.Base

open Fablish
open Fable.Helpers.Virtualdom
open Fable.Helpers.Virtualdom.Html

module Choice =
    let index (c : Choice.Model) =
        match List.tryFindIndex (fun x -> x = c.selected) c.choices with
            | Some i -> i
            | None -> failwith "selected not found in choice list"    

module Html =
    let ofC4b (c : C4b) = sprintf "rgb(%i,%i,%i)" c.R c.G c.B

    let table rows = table [clazz "ui celled striped table unstackable"] [ tbody [] rows ]

    let row k v = tr [] [ td [clazz "collapsing"] [text k]; td [clazz "right aligned"] v ]

    module Layout =
        let boxH ch = td [clazz "collapsing"; Style["padding","0px 5px 0px 0px"]] ch

        let horizontal ch = Tags.table [clazz "ui table"; Style ["backgroundColor","transparent" ]] [ tbody [] [ tr [] ch ] ]

        let finish() = td[] []
    
module List =
    let rec updateIf (p : 'a -> bool) (f : 'a -> 'a) (xs : list<'a>) = 
        match xs with
            | x :: xs ->
                if(p x) then (f x) :: updateIf p f xs
                else x :: updateIf p f xs
            | [] -> []