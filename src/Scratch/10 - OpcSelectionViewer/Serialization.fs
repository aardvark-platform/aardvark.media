namespace OpcSelectionViewer

#nowarn "8989"

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
