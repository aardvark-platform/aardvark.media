namespace CorrelationDrawing

open System
open Aardvark.Base.Incremental
open Aardvark.Base

module CorrelationUtilities =
    let alistFromAMap (input : amap<_,'a>) : alist<'a> = input |> AMap.toASet |> AList.ofASet |> AList.map snd 


    let plistFromHMap (input : hmap<_,'a>) : plist<'a> = input |> HMap.toSeq |> PList.ofSeq |> PList.map snd 
