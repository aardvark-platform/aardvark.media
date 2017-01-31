#r "../../../bin/Debug/Aardvark.Base.dll"

open Aardvark.Base
(**
# First-level heading
Some more documentation using `Markdown`.
*)

(*** include: final-sample ***)

(** 
## Second-level heading
With some more documentation
*)

(*** define: final-sample ***)
let helloWorld() = printfn "Hello world! %A" V3d.OOO

let a = V3d.OOO