namespace Aardvark.UI

open System
open Aardvark.Base

type Event<'msg> =
    {
        clientSide : (string -> list<string> -> string) -> string -> string
        serverSide : Guid -> string -> list<string> -> seq<'msg>
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Event =

    let private processEvent (name : string) (version : byte voption) (id : string) (args : list<string>) =
        let event = match version with ValueSome version -> $"{{ name: '{name}', version: {version} }}" | _ -> $"'{name}'"
        let args = $"'{id}'" :: event :: args |> String.concat ", "
        $"aardvark.processEvent({args});"

    let toString (id : string) (name : string) (evt : Event<'msg>) =
        let send = processEvent name ValueNone
        evt.clientSide send id

    let toString' (id : string) (name : string) (version : byte) (evt : Event<'msg>) =
        let send = processEvent name (ValueSome version)
        evt.clientSide send id

    let empty<'msg> : Event<'msg> =
        {
            clientSide = fun _ _ -> ""
            serverSide = fun _ _ _ -> Seq.empty
        }

    let ofTrigger (reaction : unit -> 'msg) =
        {
            clientSide = fun send id -> send id []
            serverSide = fun _ _ _ -> Seq.delay (reaction >> Seq.singleton)
        }

    let ofDynamicArgs (args : list<string>) (reaction : list<string> -> seq<'msg>) =
        {
            clientSide = fun send id -> send id args
            serverSide = fun _ _ -> reaction
        }

    let create1 (a : string) (reaction : 'a -> 'msg) =
        {
            clientSide = fun send id -> send id [a]
            serverSide = fun _ _ args ->
                match args with
                | [a] ->
                    try
                        Seq.delay (fun () -> Seq.singleton (reaction (Pickler.json.UnPickleOfString a)))
                    with e ->
                        Log.warn "[UI] expected args (%s) but got (%A)" typename<'a> a
                        Seq.empty

                | _ ->
                    Log.warn "[UI] expected args (%s) but got %A" typename<'a> args
                    Seq.empty
        }

    let create2 (a : string) (b : string) (reaction : 'a -> 'b -> 'msg) =
        {
            clientSide = fun send id -> send id [a; b]
            serverSide = fun _ _ args ->
                match args with
                | [a; b] ->
                    try
                        Seq.delay (fun () -> Seq.singleton (reaction (Pickler.json.UnPickleOfString a) (Pickler.json.UnPickleOfString b) ))
                    with e ->
                        Log.warn "[UI] expected args (%s, %s) but got (%A, %A)" typename<'a> typename<'b> a b
                        Seq.empty

                | _ ->
                    Log.warn "[UI] expected args (%s, %s) but got %A" typename<'a> typename<'b> args
                    Seq.empty
        }

    let create3 (a : string) (b : string) (c : string) (reaction : 'a -> 'b -> 'c -> 'msg) =
        {
            clientSide = fun send id -> send id [a; b; c]
            serverSide = fun _ _ args ->
                match args with
                | [a; b; c] ->
                    try
                        Seq.delay (fun () -> Seq.singleton (reaction (Pickler.json.UnPickleOfString a) (Pickler.json.UnPickleOfString b) (Pickler.json.UnPickleOfString c)))
                    with e ->
                        Log.warn "[UI] expected args (%s, %s, %s) but got (%A, %A, %A)" typename<'a> typename<'b> typename<'c> a b c
                        Seq.empty

                | _ ->
                    Log.warn "[UI] expected args (%s, %s, %s) but got %A" typename<'a> typename<'b> typename<'c> args
                    Seq.empty
        }

    let combine (l : Event<'msg>) (r : Event<'msg>) =
        {
            clientSide = fun send id ->
                l.clientSide (fun id args -> send id ("0" :: args)) id + "; " +
                r.clientSide (fun id args -> send id ("1" :: args)) id

            serverSide = fun session id args ->
                match args with
                | "0" :: args -> l.serverSide session id args
                | "1" :: args -> r.serverSide session id args
                | _ ->
                    Log.warn "[UI] expected args ((1|2)::args) but got %A" args
                    Seq.empty

        }

    let combineMany (events : seq<Event<'msg>>) =
        let events = Seq.toArray events

        match events.Length with
        | 0 -> empty
        | 1 -> events.[0]
        | _ ->
            {
                clientSide = fun send id ->
                    let clientScripts =
                        events |> Seq.mapi (fun i e ->
                            e.clientSide (fun id args -> send id (string i :: args)) id
                        )
                    String.concat "; " clientScripts

                serverSide = fun session id args ->
                    match args with
                    | index :: args ->
                        match index with
                        | Int index when index >= 0 && index < events.Length ->
                            events.[index].serverSide session id args

                        | _ ->
                            Log.warn "[UI] unexpected index for dispatcher: %A" index
                            Seq.empty
                    | [] ->
                        Log.warn "[UI] expected at least one arg for dispatcher"
                        Seq.empty
            }

    let map (f : 'a -> 'b) (e : Event<'a>) =
        {
            clientSide = e.clientSide;
            serverSide = fun session id args -> Seq.map f (e.serverSide session id args)
        }