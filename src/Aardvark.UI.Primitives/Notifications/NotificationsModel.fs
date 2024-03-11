namespace Aardvark.UI.Primitives.Notifications

open Adaptify
open FSharp.Data.Adaptive

// Simple notifications using the Toast module of Fomantic UI
// https://fomantic-ui.com/modules/toast.html

type Color =
    | Default = 0
    | Red     = 1
    | Orange  = 2
    | Yellow  = 3
    | Olive   = 4
    | Green   = 5
    | Teal    = 6
    | Blue    = 7
    | Violet  = 8
    | Purple  = 9
    | Pink    = 10
    | Brown   = 11
    | Grey    = 12
    | Black   = 13

[<Struct>]
type Theme =
    { Color    : Color
      Inverted : bool }

module Theme =
    let Default = { Color = Color.Default; Inverted = false }

type Position =
    | TopRight       = 0
    | TopLeft        = 1
    | TopCenter      = 2
    | TopAttached    = 3
    | BottomRight    = 4
    | BottomLeft     = 5
    | BottomCenter   = 6
    | BottomAttached = 7

[<RequireQualifiedAccess>]
type Duration =
    | Milliseconds of int
    | Auto of minMilliseconds: int * wordsPerMinute: int

module Duration =
    let Default = Duration.Milliseconds 3000
    let DefaultAuto = Duration.Auto(1000, 120)
    let Infinite = Duration.Milliseconds 0

type Progress =
    { Top        : bool
      Theme      : Theme
      Increasing : bool }

module Progress =
    let Default = { Top  = false; Theme = Theme.Default; Increasing = false }

type Notification =
    { Title         : string option
      Message       : string
      Icon          : string option
      Progress      : Progress option
      Theme         : Theme
      Position      : Position
      CenterContent : bool
      CloseIcon     : bool
      Duration      : Duration }

[<ModelType>]
type Notifications =
    { Active : HashMap<int, Notification>
      NextId : int }

module Notifications =

    type Message =
        | Send   of Notification
        | Remove of id: int
        | Clear

    let Empty =
        { Active = HashMap.empty
          NextId = 0 }