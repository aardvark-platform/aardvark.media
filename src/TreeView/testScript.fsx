#r @"C:\Users\walch\Desktop\media\packages\Newtonsoft.Json\lib\net45\Newtonsoft.Json.dll"
#r @"C:\Users\walch\Desktop\media\packages\FsPickler\lib\net45\FsPickler.dll"
#r @"C:\Users\walch\Desktop\media\packages\FsPickler.Json\lib\net45\FsPickler.Json.dll"

open System.IO

type NodeData =
    {
        key : string
        title : string
        folder : bool
        customIcon : Option<string>
        tooltip : string
    }


type TreePointer = 
    | Root
    | Selected
    | Node of key : string

type InsertMode =
    | Before
    | After
    | FirstChild
    | LastChild

type InsertPostion = { pointer : TreePointer; mode : InsertMode }

type Msg = 
    | Add of location : InsertPostion * node : NodeData 
    | Remove of location : TreePointer
    | Move of source : TreePointer * target : InsertPostion
    | Select of location : TreePointer
    | Deselect of location : TreePointer

let s = MBrace.FsPickler.Json.FsPickler.CreateJsonSerializer(false, true)
let pickle o = s.PickleToString(o).Replace("\"", "\\\"") |> sprintf "emit(JSON.parse(\"%s\"));"


let testCode =
    List.map pickle [
        Add({ pointer = Root; mode = LastChild }, { key = "1234"; title = "hi"; folder = true; customIcon = None; tooltip = ""})
        Add({ pointer = Node "1234"; mode = LastChild }, { key = "4321"; title = "hi nested"; folder = false; customIcon = None; tooltip = ""})
        Add({ pointer = Node "4321"; mode = LastChild }, { key = "3454"; title = "hi nested nested"; folder = false; customIcon = None; tooltip = ""})

        Move(Node "3454", { pointer = Node "4321"; mode = Before })
    ]
    |> String.concat "\r\n"

File.WriteAllText(@"C:\Users\walch\Desktop\lol\run.js", testCode);