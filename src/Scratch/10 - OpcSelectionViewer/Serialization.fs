namespace OpcSelectionViewer

#nowarn "8989"

open Aardvark.Base

module Serialization =    
    open System.IO
    open MBrace.FsPickler
        
    let registry = new CustomPicklerRegistry()    
    let cache = PicklerCache.FromCustomPicklerRegistry registry    

    let binarySerializer = FsPickler.CreateBinarySerializer(picklerResolver = cache)    

    let save path (d : 'a) =    
        let arr =  binarySerializer.Pickle d
        File.WriteAllBytes(path, arr);
        d
    
    let loadAs<'a> path : 'a =
        let arr = File.ReadAllBytes(path)
        binarySerializer.UnPickle arr

    let writeToFile path (contents : string) =
        System.IO.File.WriteAllText(path, contents)  

    let fileExists filePath =
        match System.IO.File.Exists filePath with
          | true  -> Some filePath
          | false -> None

    let tryLoadAs<'a> path : Option<'a> =
        path 
          |> fileExists 
          |> Option.map(fun p ->
             let arr = File.ReadAllBytes(p)
             binarySerializer.UnPickle arr)

module Lenses = 
    let get    (lens : Lens<'s,'a>) (s:'s) : 'a              = lens.Get(s)
    let set    (lens : Lens<'s,'a>) (v : 'a) (s:'s) : 's     = lens.Set(s,v)
    let set'   (lens : Lens<'s,'a>) (s:'s) (v : 'a)  : 's    = lens.Set(s,v)
    let update (lens : Lens<'s,'a>) (f : 'a->'a) (s:'s) : 's = lens.Update(s,f)