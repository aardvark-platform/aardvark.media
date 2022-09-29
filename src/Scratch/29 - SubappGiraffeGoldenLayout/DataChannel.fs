namespace Test

open Aardvark.UI
open Aardvark.UI.Primitives

open Aardvark.Base
open FSharp.Data.Adaptive

module DataChannel =
    type BespokeChannelReader<'a> (m        : aval<'a>,
                                   pickle   : 'a -> string) =
        inherit ChannelReader()

        let mutable last = None

        override x.Release() =
            last <- None

        override x.ComputeMessages t =
            let v = m.GetValue t

            if Unchecked.equals last (Some v) then
                []
            else
                last <- Some v
                [ pickle v ]

    type BespokeAValChannel<'a> (m        : aval<'a>, 
                                 pickle   : 'a -> string) =
        inherit Channel()
        override x.GetReader() = 
            new BespokeChannelReader<_>(m, pickle) :> ChannelReader

    /// all information necessary to create a data channel
    type DataChannelSettings<'a> =
        {
            updateJs : string
            pickle   : 'a -> string
            data     : aval<'a>
        }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module DataChannelSettings =
        let toChannel ( settings : DataChannelSettings<'a>) =
            BespokeAValChannel(settings.data, settings.pickle) :> Channel

        let toNamedFunction (number : int) ( settings : DataChannelSettings<'a>)  =
            let currentChannel = sprintf "valueCh%i" number
            let f = sprintf "%s.onmessage = function (data) {%s}" currentChannel settings.updateJs
            (currentChannel, f)

    /// adds two data channels to one DomNode
    let addTwoChannels (onBootJs  : string) 
                       (settings0 : DataChannelSettings<'a>)
                       (settings1 : DataChannelSettings<'b>) = 
        let channels = 
            [
                settings0 |> DataChannelSettings.toChannel
                settings1 |> DataChannelSettings.toChannel
            ]
        let namedFunctions = 
            [
                settings0 |> DataChannelSettings.toNamedFunction 0
                settings1 |> DataChannelSettings.toNamedFunction 1
            ]

        let zipped = List.zip namedFunctions channels
        let namedChannels =
            zipped |> List.map (fun ((name, update), channel) -> name, channel)

        let updateData = 
            String.concat ";" ([onBootJs]@(namedFunctions|> List.map snd))

        let onBoot = onBoot' namedChannels updateData 
        onBoot


    /// adds multiple data channels to one DomNode
    let addDataChannels (onBootJs     : string) 
                        (updateJs     : list<string>)
                        (pickle       : list<'a -> string>)
                        (data         : list<aval<'a>>) = 
        let channels =
            List.zip data pickle
                |> List.map (fun (x, p) -> BespokeAValChannel(x, p) :> Channel)

        let namedFunctions =
            seq {
                let mutable i = 0
                for body in updateJs do
                    let currentChannel = sprintf "valueCh%i" i
                    let f = sprintf "%s.onmessage = function (data) {%s}" currentChannel body
                    yield (currentChannel, f)
                i <- i + 1
            } |> List.ofSeq

        let zipped = List.zip namedFunctions channels 
        let namedChannels =
            zipped |> List.map (fun ((name, update), channel) -> name, channel)

        let updateData = 
            String.concat ";" ([onBootJs]@(namedFunctions|> List.map snd))

        let onBoot = onBoot' namedChannels updateData 
        onBoot

    
    /// Adds a data channel to a DomNode. updateJs is the content 
    /// of the javascript function that is called when the data changes. The data
    /// is addressed in js using the variable 'data'. 
    /// 
    let addDataChannel (onBootJs     : string) 
                       (updateJs     : string)
                       (pickle       : option<'a -> string>)
                       (data         : aval<'a>) = 
        let channel =
            match pickle with
            | Some pickle ->
                BespokeAValChannel(data, pickle) :> Channel
            | None -> AVal.channel data

        let updateData = 
            String.concat ";" [
                onBootJs
                sprintf "valueCh.onmessage = function (data) {%s}" updateJs
            ]

        let onBoot = onBoot' ["valueCh", channel] updateData 
        onBoot

    module Pickler =
        open MBrace.FsPickler
        open MBrace.FsPickler.Json
        let json = FsPickler.CreateJsonSerializer(false, true)

    type DataChangeAttributeSettings<'a,'msg> =
        {
            updateAction : 'a -> 'msg
            data         : aval<'a>
            parse        : string -> 'a
        }

    let onDataChangeAttribute' (settings : DataChangeAttributeSettings<'a, 'msg>) = 
        let unpickle (data : string) =
            try 
                settings.parse data
                    |> Some
            with
                | ex -> 
                    Log.error "[DataChannel] Could not parse %s" data
                    None

        let value = 
            if settings.data.IsConstant 
            then AVal.custom (fun t -> settings.data.GetValue t) 
            else settings.data

        let upd v =
            value.MarkOutdated()
            settings.updateAction v

        let dataEventFunction (lst : list<string>) = 
            let str = lst.Head
            str 
                |> unpickle 
                |> Option.toList 
                |> Seq.map upd 

        onEvent' "data-event" 
                    [] 
                    dataEventFunction

    let onDataChangeAttribute (updateAction : 'a -> 'msg)
                              (data         : aval<'a>)
                              (parse        : string -> 'a) = 
        onDataChangeAttribute'
            {
                updateAction = updateAction
                data         = data        
                parse        = parse       
            }

    let twoDataChangeAttributes (settings0 : DataChangeAttributeSettings<'a, 'msg>)
                                   (settings1 : DataChangeAttributeSettings<'b, 'msg>) = 

        let getValueIfConstant s =
            if s.data.IsConstant 
            then AVal.custom (fun t -> s.data.GetValue t) 
            else s.data

        let dataEventFunction (lst : list<string>) = 
            let str = lst.Head
            try 
                let newValue = settings0.parse str
                let oldValue = getValueIfConstant settings0
                let action =
                    oldValue.MarkOutdated ()
                    settings0.updateAction newValue
                action
                |> Seq.singleton
            with ex -> 
                try 
                    let newValue = settings1.parse str
                    let oldValue = getValueIfConstant settings1
                    let action =
                        oldValue.MarkOutdated ()
                        settings1.updateAction newValue
                    action
                    |> Seq.singleton
                with ex ->    
                    Log.error "[DataChannel] Could not parse %s" str
                    Seq.empty

        onEvent' "data-event" [] dataEventFunction