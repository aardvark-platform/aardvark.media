namespace PlaceTransformObjects

open Aardvark.Base
open Aardvark.Base.Incremental

type Object =
    {
        name: string
    }

type World =
    {
        objects: hmap<string, Object>
    }