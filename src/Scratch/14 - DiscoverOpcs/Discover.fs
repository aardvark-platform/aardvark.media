namespace DiscoverOpcs

open System.IO
open Aardvark.Base

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

  let rec superDiscovery (input : string) :  string * list<string> =
    match input |> toDiscoverFolder with
    | Directory path -> 
      let opcs = 
        path 
          |> Directory.EnumerateDirectories 
          |> Seq.toList 
          |> List.map(fun x -> x |> superDiscovery |> snd) |> List.concat 
      input, opcs
    | OpcFolder p -> input,[p]

  /// <summary>
  /// checks if "path" is a valid surface folder, has at least 1 opc
  /// </summary>        
  let isSurface (path : string) =
      Directory.GetDirectories(path) |> Seq.exists isOpcFolder

  /// <summary>
  /// checks if "path" is a valid surface folder
  /// </summary>        
  let isSurfaceFolder (path : string) =
      Directory.GetDirectories(path) |> Seq.exists isSurface
  
  let discover (p : string -> bool) path : list<string> =
    if Directory.Exists path then
      Directory.EnumerateDirectories path
        |> Seq.filter p            
        |> Seq.toList
    else List.empty
    
  let discoverSurfaces path = 
    discover isSurface path          
  
  let discoverOpcs path = 
    discover isOpcFolder path  