namespace Aardvark.Service

open System
open System.IO

open Aardvark.Base


type FSEntryKind =
    | Unknown = 0
    | File = 1
    | Directory = 2
    | Root = 3
    | Disk = 4
    | DVD = 5
    | Share = 6
    | Removable = 7

type FSEntry =
    {
        kind            : FSEntryKind
        isDevice        : bool
        path            : string
        name            : string
        length          : int64
        lastWriteTime   : DateTime
        lastAccessTime  : DateTime
        creationTime    : DateTime
        hasChildren     : bool
        hasFolders      : bool
        isHidden        : bool
        isSystem        : bool
    }

type FSContent =
    {
        success : bool
        fullPath : string
        entries : list<FSEntry>
    }

type FileSystem private(rootPath : Option<string>) =
    let rootPath =
        match rootPath with
            | Some p -> 
                let p = Path.GetFullPath p 
                if p.EndsWith "\\" then p.Substring(0, p.Length - 1) |> Some
                else p |> Some
            | None -> 
                None

    let rec appendPath (p : string) (c : list<string>) =
        match c with
            | [] -> Some p
            | ".." :: rest -> appendPath (Path.GetDirectoryName(p)) rest
            | "." :: rest -> appendPath p rest
            | h :: rest -> appendPath (Path.Combine(p, h)) rest

    let localPath (path : string) =
        let path = 
            if path.StartsWith "/" then path.Substring(1)
            else path

        let parts = path.Split([|'/'; '\\'|], StringSplitOptions.RemoveEmptyEntries) |> Array.toList
            

        match rootPath, parts with
            | Some r, parts -> 
                match appendPath r parts with
                    | Some p ->
                        if p.StartsWith r then Some p
                        else None
                    | None ->
                        None
            | None, r :: parts ->
                match Environment.OSVersion with
                    | Windows -> appendPath (r + ":\\") parts
                    | _ -> appendPath "" (r :: parts)

            | None, [] ->
                Some "/"
                                
    let remotePath (fullPath : string) =

        let sep =
            match Environment.OSVersion with
                | Windows -> '\\'
                | _ -> '/'

        let relative = 
            match rootPath with
                | Some root -> 
                    if fullPath.StartsWith root then
                        Some(fullPath.Substring(root.Length).Split([|sep|], StringSplitOptions.RemoveEmptyEntries) |> Array.toList)
                    else
                        None
                | None -> 
                    let comp = fullPath.Split([|sep|], StringSplitOptions.RemoveEmptyEntries) |> Array.toList
                    match Environment.OSVersion, comp with
                        | Windows, h :: rest ->
                            Some (h.Substring(0, h.Length - 1 ) :: rest)
                        | _ ->
                            Some comp

             
        relative |> Option.map (String.concat "/" >> sprintf "/%s")

    let createEntry (path : string) =
        match remotePath path with
            | Some remotePath ->
                let att = File.GetAttributes(path)
                if att.HasFlag FileAttributes.Directory then
                    let d = DirectoryInfo(path)

                    let hasChildren =
                        try d.EnumerateFileSystemInfos() |> Seq.isEmpty |> not
                        with _ -> false

                    let hasFolders =
                        if hasChildren then
                            try d.EnumerateDirectories() |> Seq.isEmpty |> not
                            with _ -> false
                        else
                            false

                    Some {
                        kind            = FSEntryKind.Directory
                        isDevice        = false
                        path            = remotePath
                        name            = d.Name
                        length          = 0L
                        lastWriteTime   = d.LastWriteTimeUtc
                        lastAccessTime  = d.LastAccessTimeUtc
                        creationTime    = d.CreationTimeUtc
                        hasChildren     = hasChildren
                        hasFolders      = hasFolders
                        isHidden        = att.HasFlag FileAttributes.Hidden
                        isSystem        = att.HasFlag FileAttributes.System
                    }

                else
                    let f = FileInfo(path)
                
                    Some {
                        kind            = FSEntryKind.File
                        isDevice        = false
                        path            = remotePath
                        name            = f.Name
                        length          = f.Length
                        lastWriteTime   = f.LastWriteTimeUtc
                        lastAccessTime  = f.LastAccessTimeUtc
                        creationTime    = f.CreationTimeUtc
                        hasChildren     = false
                        hasFolders      = false
                        isHidden        = att.HasFlag FileAttributes.Hidden
                        isSystem        = att.HasFlag FileAttributes.System
                    }
            | None -> 
                None
        
    let rootEntries =
        DriveInfo.GetDrives()
            |> Array.toList
            |> List.choose (fun di ->
                let kind =
                    match di.DriveType with
                        | DriveType.Fixed -> FSEntryKind.Disk
                        | DriveType.Network -> FSEntryKind.Share
                        | DriveType.Removable -> FSEntryKind.Removable
                        | DriveType.CDRom -> FSEntryKind.DVD
                        | _ -> FSEntryKind.Unknown
                    
                let should =
                    match System.Environment.OSVersion with
                        | Linux | Mac when di.Name = "/" -> true
                        | Windows -> true
                        | _ -> false
                    
                if should then
                        
                    let name = 
                        match System.Environment.OSVersion with 
                            | Windows -> di.Name.Substring(0, di.Name.Length - 2)
                            | _ -> di.Name

                    let hasChildren, length =
                        if di.IsReady then
                            try 
                                let empty = DirectoryInfo(di.Name).EnumerateFiles() |> Seq.isEmpty
                                not empty, di.TotalSize
                            with _ -> 
                                false, 0L
                        else
                            false, 0L

                    {
                        kind            = kind
                        isDevice        = true
                        path            = "/" + name
                        name            = name
                        length          = length
                        lastWriteTime   = DateTime.MinValue
                        lastAccessTime  = DateTime.MinValue
                        creationTime    = DateTime.MinValue
                        hasChildren     = hasChildren
                        hasFolders      = hasChildren
                        isHidden        = false
                        isSystem        = false
                    } |> Some
                else None

            )

    member x.GetEntries(path : string) =
        match Environment.OSVersion, localPath path with
            | Windows, Some "/" ->
                {
                    success = true
                    fullPath = "/"
                    entries = rootEntries
                }

            | _,Some localPath ->
                if Directory.Exists localPath then
                    let entries = 
                        Directory.GetFileSystemEntries(localPath)
                            |> Array.toList
                            |> List.choose createEntry
                    {
                        success = true
                        fullPath = path
                        entries = entries
                    }
                else
                    {
                        success = false
                        fullPath = path
                        entries = []
                    }
            | _,None ->
                {
                    success = false
                    fullPath = path
                    entries = []
                }
          
    new(rootDir : string) =
        if not (Directory.Exists rootDir) then failwithf "[FS] cannot open directory: %A" rootDir
        FileSystem(Some rootDir)

    new() = FileSystem(None)
    

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FileSystem =
    open Suave
    open Suave.Filters
    open Suave.Operators
    open Suave.Successful
    
    let toWebPart (fs : FileSystem) : WebPart =
        fun (ctx : HttpContext) ->
            async {
                match ctx.request.queryParamOpt "path" with
                    | Some(_, Some path) ->
                        let entries = 
                            if String.IsNullOrWhiteSpace path then fs.GetEntries "/"
                            else fs.GetEntries path

                        let data = Pickler.json.Pickle entries

                        return! ctx |> (ok data >=> Writers.setMimeType "text/json")
                    | _ ->
                        return! never ctx
            }

