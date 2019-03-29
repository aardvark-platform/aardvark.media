namespace DiscoverOpcs

open System.IO
open Aardvark.Base

//type OpcFolder =
//  | SurfaceFolder of string //contains surfaces
//  | Surface       of string //contains OPCs
//  | Opc           of string
//  | Other         of string

type DiscoverFolder = 
  | OpcFolder of string
  | Directory of string  

module Discover = 
  

  /// <summary>
  /// checks if "path" is a valid opc folder containing "images", "patches", and patchhierarchy.xml
  /// </summary>
  let isOpcFolder (path : string) = 
      let imagePath = Path.combine [path; "images"]
      let patchPath = Path.combine [path; "patches"]
      (Directory.Exists imagePath) &&
      (Directory.Exists patchPath) && 
      File.Exists(patchPath + "\\patchhierarchy.xml")
    
  
  let toDiscoverFolder path = 
    if path |> isOpcFolder then
      OpcFolder path
    else
      Directory path

  /// <summary>
  /// checks if "path" is a valid surface folder
  /// </summary>        
  let isSurfaceFolder (path : string) =
    let subdirs = Directory.GetDirectories(path)
    match subdirs.IsEmptyOrNull() with
        | true -> false
        | _ -> subdirs |> Seq.forall isOpcFolder

         

  let rec superDiscovery (input : string) :  string * list<string> =
    match input |> toDiscoverFolder with
    | Directory path -> 
      let opcs = 
        path 
          |> Directory.EnumerateDirectories 
          |> Seq.toList 
          |> List.map(fun x -> superDiscovery x |> snd) |> List.concat 
      input, opcs
    | OpcFolder p -> input,[p]

  let rec superDiscoveryMultipleSurfaceFolder (input : string) : list<string> =
    let isSurfFolder = input |> isSurfaceFolder
    match isSurfFolder with
        | true -> [input]
        | _-> input 
                |> Directory.EnumerateDirectories 
                |> Seq.toList 
                |> List.map(fun x -> superDiscoveryMultipleSurfaceFolder x ) |> List.concat

  ///// <summary>
  ///// checks if "path" is a valid surface folder
  ///// </summary>        
  let isSurface (path : string) =
      Directory.GetDirectories(path) |> Seq.exists isOpcFolder

  ///// <summary>
  ///// checks if "path" is a valid surface folder
  ///// </summary>        
  //let isSurfaceFolder (path : string) =
  //    Directory.GetDirectories(path) |> Seq.exists isSurface
  
  let discover (p : string -> bool) path : list<string> =
    if Directory.Exists path then
      Directory.EnumerateDirectories path
        |> Seq.filter p            
        |> Seq.toList
    else List.empty
  
  /// returns all valid surface folders in "path"   
  let discoverSurfaces path = 
    discover isSurface path          
  
  let discoverOpcs path = 
    discover isOpcFolder path

  //let decideFolder 

  //let rec visitDirectories path : list<string> =
  //  match path |> toDiscoverFolder with
  //  | 