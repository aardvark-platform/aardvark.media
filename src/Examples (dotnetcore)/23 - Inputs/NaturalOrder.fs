namespace NaturalOrder

open System
open System.Text.RegularExpressions
open FSharp.Data.Adaptive

module NaturalOrder =

    // http://www.fssnip.net/7X8/title/Natural-Sort-Order
    module private Impl =
        
        let regex = Regex ("([0-9]+)", RegexOptions.Compiled)

        let trimLeadingZeros (s: string) =
            s.TrimStart '0'

        let toChars (s: string) =
            s.ToCharArray()

        let split text =
            text
            |> regex.Split
            |> Seq.filter (fun s -> s.Length > 0)
            |> Seq.toArray

        let compareStrings (s1: string) (s2: string) =
            // each string is either all letters or all numbers
            let isNumeric1 = Char.IsDigit s1.[0]
            let isNumeric2 = Char.IsDigit s2.[0]

            // If we have a string and a number, the number comes first. When we have 
            // two strings, compare them normally. The tricky case is two numbers.
            match isNumeric1, isNumeric2 with
            | true, false -> -1
            | false, true -> 1
            | false, false -> String.Compare (s1, s2, true)
            | true, true -> 
                // leading zeros will trip us up, get rid of them
                let n1, n2 = trimLeadingZeros s1, trimLeadingZeros s2
                if n1.Length < n2.Length then -1
                elif n2.Length < n1.Length then 1
                else
                    // compare digit-by-digit
                    let chars1, chars2 = toChars n1, toChars n2
                    let result =
                        chars2
                        |> Seq.zip chars1
                        |> Seq.tryPick (fun (c1, c2) -> 
                            if c1 < c2 then Some -1
                            elif c2 < c1 then Some 1
                            else None)
                    match result with
                    | Some i -> i
                    | None -> 0

        type Content<'a> = {
            Pieces: string[]
            Content : 'a
        }
    
    open Impl

    let compare (name1: string) (name2: string) : int = 
        Array.compareWith compareStrings (split name1) (split name2)

    let sort (names : seq<string>) : seq<string> =
        names
        |> Seq.map (fun name -> { Content = name; Pieces = split name })
        |> Seq.sortWith (fun p1 p2 -> Array.compareWith compareStrings p1.Pieces p2.Pieces)
        |> Seq.map (fun pair -> pair.Content)

    let sortBy (key : 'a -> string * 'a) (names : seq<'a>) : seq<'a> =
        names
        |> Seq.map key
        |> Seq.map (fun (name, content) -> { Content = content; Pieces = split name })
        |> Seq.sortWith (fun p1 p2 -> Array.compareWith compareStrings p1.Pieces p2.Pieces)
        |> Seq.map (fun pair -> pair.Content)

    let sortAList (names: alist<string>) : alist<string> =
        names
        |> AList.map (fun name -> { Content = name; Pieces = split name })
        |> AList.sortWith (fun p1 p2 -> Array.compareWith compareStrings p1.Pieces p2.Pieces)
        |> AList.map (fun pair -> pair.Content)

    let sortByAList (key : 'a -> string * 'a) (names: alist<'a>) : alist<'a> =
        names
        |> AList.map key
        |> AList.map (fun (name, content) -> { Content = content; Pieces = split name })
        |> AList.sortWith (fun p1 p2 -> Array.compareWith compareStrings p1.Pieces p2.Pieces)
        |> AList.map (fun pair -> pair.Content)
  
[<AutoOpen>]
module Seq =
    
    let sortNaturally (input : seq<string>) : seq<string> =    
        input |> NaturalOrder.sort

    let sortByNaturally (key : 'a -> string * 'a) (input : seq<'a>) : seq<'a> =    
        input |> NaturalOrder.sortBy key

[<AutoOpen>]
module AList = 
        
    let sortNaturally (input : alist<string>) : alist<string> =    
        input |> NaturalOrder.sortAList

    let sortByNaturally (key : 'a -> string * 'a) (input : alist<'a>) : alist<'a> =    
        input |> NaturalOrder.sortByAList key