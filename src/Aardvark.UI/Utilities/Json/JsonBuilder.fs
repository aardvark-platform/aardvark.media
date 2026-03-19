namespace Aardvark.UI

[<AutoOpen>]
module JsonBuilder =
    open Newtonsoft.Json.Linq

    module JsonBuilderInternals =

        type JTokenCreator private () =
            static member inline Token(v : bool) : JToken = JToken.op_Implicit v
            static member inline Token(v : string) : JToken = JToken.op_Implicit v
            static member inline Token(v : int8) : JToken = JToken.op_Implicit v
            static member inline Token(v : int16) : JToken = JToken.op_Implicit v
            static member inline Token(v : int32) : JToken = JToken.op_Implicit v
            static member inline Token(v : int64) : JToken = JToken.op_Implicit v
            static member inline Token(v : uint8) : JToken = JToken.op_Implicit v
            static member inline Token(v : uint16) : JToken = JToken.op_Implicit v
            static member inline Token(v : uint32) : JToken = JToken.op_Implicit v
            static member inline Token(v : uint64) : JToken = JToken.op_Implicit v
            static member inline Token(v : float32) : JToken = JToken.op_Implicit v
            static member inline Token(v : float) : JToken = JToken.op_Implicit v
            static member inline Token(v : decimal) : JToken = JToken.op_Implicit v
            static member inline Token(v : System.Uri) : JToken = JToken.op_Implicit v
            static member inline Token(v : System.DateTime) : JToken = JToken.op_Implicit v
            static member inline Token(v : System.DateTimeOffset) : JToken = JToken.op_Implicit v
            static member inline Token(v : System.TimeSpan) : JToken = JToken.op_Implicit v
            static member inline Token(v : System.Guid) : JToken = JToken.op_Implicit v
            static member inline Token(v : byte[]) : JToken = JToken.op_Implicit v

            static member inline Token(v : JObject) : JToken = v :> JToken
            static member inline Token(v : string[]) : JToken = JArray(v) :> JToken
            static member inline Token(v : seq<string>) : JToken = JArray(Seq.toArray (Seq.cast<obj> v)) :> JToken
            static member inline Token(v : seq<JObject>) : JToken = JArray(Seq.toArray (Seq.cast<obj> v)) :> JToken

        let inline private jtokenAux (_ : ^a) (v : ^b) : ^c =
            ((^a or ^b) : (static member Token : ^b -> ^c) v)

        let inline private jtoken v = jtokenAux Unchecked.defaultof<JTokenCreator> v

        type JsonBuilder() =
            member inline x.Zero() = []

            member inline x.Yield(k : string, v) = [k, jtoken v]

            member inline x.Delay(action : unit -> list<string * JToken>) =
                action

            member inline x.Combine(l : list<string * JToken>, r : unit -> list<string * JToken>) =
                l @ r()

            member inline x.For(elements : seq<'a>, mapping : 'a -> list<string * JToken>) =
                elements |> Seq.toList |> List.collect mapping

            member inline x.Run(action : unit -> list<string * JToken>) =
                let o = JObject()
                for k, v in action() do
                    o.[k] <- v
                o

    let json = JsonBuilderInternals.JsonBuilder()