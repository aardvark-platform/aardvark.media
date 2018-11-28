namespace DiscoverOpcs

open System.IO
open Aardvark.Base

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
  
  /// <summary>
  /// checks if "path" is a valid surface folder
  /// </summary>        
  let isSurfaceFolder (path : string) =
      Directory.GetDirectories(path) |> Seq.forall isOpcFolder
  
  let discover (p : string -> bool) path : list<string> =
    if Directory.Exists path then
      Directory.EnumerateDirectories path
        |> Seq.filter p            
        |> Seq.toList
    else List.empty
  
  /// returns all valid surface folders in "path"   
  let discoverSurfaces path = 
    discover isSurfaceFolder path          
  
  let discoverOpcs path = 
    discover isOpcFolder path