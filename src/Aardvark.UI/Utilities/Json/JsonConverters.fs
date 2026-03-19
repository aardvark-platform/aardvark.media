namespace Aardvark.UI

open System
open System.Text.Json
open System.Text.Json.Serialization
open Aardvark.Base

[<AutoOpen>]
module internal JsonConverters =

    [<AbstractClass>]
    type JsonRecordConverter<'TValue, 'TState when 'TState : struct>() =
        inherit JsonConverter<'TValue>()

        abstract member GetValue : state: inref<'TState> -> 'TValue

        abstract member ReadField : reader: byref<Utf8JsonReader> * name: string * state: byref<'TState> * options: JsonSerializerOptions -> unit
        default _.ReadField(_, _, _, _) = raise <| NotSupportedException("Deserialization not supported.")

        abstract member WriteFields : writer: Utf8JsonWriter * value: inref<'TValue> * options: JsonSerializerOptions -> unit
        default _.WriteFields(_, _, _) = raise <| NotSupportedException("Serialization not supported.")

        override this.Write(writer, value, options) =
            writer.WriteStartObject()
            this.WriteFields(writer, &value, options)
            writer.WriteEndObject()

        override this.Read(reader, _, options) =
            let mutable state = Unchecked.defaultof<'TState>

            while reader.Read() && reader.TokenType <> JsonTokenType.EndObject do
                match reader.TokenType with
                | JsonTokenType.PropertyName ->
                    let propertyName = reader.GetString()
                    reader.Read() |> ignore
                    this.ReadField(&reader, propertyName, &state, options)
                | token ->
                    raise <| JsonException($"Expected property name but got {token}.")

            this.GetValue(&state)

    [<AbstractClass>]
    type JsonStructConverter<'T when 'T : struct>() =
        inherit JsonRecordConverter<'T, 'T>()
        override _.GetValue(state) = state

    type JsonV2iConverter() =
        inherit JsonStructConverter<V2i>()

        override this.WriteFields(writer, value, _) =
            writer.WriteNumber("X", value.X)
            writer.WriteNumber("Y", value.Y)

        override this.ReadField(reader, name, value, _) =
            match name with
            | "X" | "x" -> value.X <- reader.GetInt32()
            | "Y" | "y" -> value.Y <- reader.GetInt32()
            | _         -> reader.Skip()

    type JsonC4bConverter() =
        inherit JsonStructConverter<C4b>()

        override this.WriteFields(writer, value, _) =
            writer.WriteNumber("R", int value.R)
            writer.WriteNumber("G", int value.G)
            writer.WriteNumber("B", int value.B)
            writer.WriteNumber("A", int value.A)

        override this.ReadField(reader, name, value, _) =
            match name with
            | "R" | "r" -> value.R <- reader.GetByte()
            | "G" | "g" -> value.G <- reader.GetByte()
            | "B" | "b" -> value.B <- reader.GetByte()
            | "A" | "a" -> value.A <- reader.GetByte()
            | _         -> reader.Skip()

    type JsonSerializer with
        static member inline Deserialize<'TValue>(data: ArraySegment<byte>, serializerOptions: JsonSerializerOptions) =
            JsonSerializer.Deserialize<'TValue>(
                ReadOnlySpan<byte>(data.Array, data.Offset, data.Count),
                serializerOptions
            )