module Program

open System
open System.IO
open System.Reflection
open System.Runtime.InteropServices

open CefSharp
open CefSharp.RenderProcess

module Native =

    [<DllImport("Aardvark.Cef.Process.Core.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern void onContextCreated(voidptr pContext)

type RenderProcessHandler() =
    member _.OnContextCreated(frame: IFrame, context: IV8Context) =
        if frame.IsMain && frame.IsValid then
            let _context = context.GetType().GetField("_context", BindingFlags.NonPublic ||| BindingFlags.Instance)
            if isNull context then raise <| MissingMemberException("Could not find _context field")

            let get = _context.FieldType.GetMethod("get", BindingFlags.Public ||| BindingFlags.Instance, null, [||], null)
            if isNull get then raise <| MissingMethodException(_context.FieldType.Name, "get")

            let ptr = get.Invoke(_context.GetValue(context), [||])
            if isNull ptr then raise <| NullReferenceException("Field _context is null.")

            Native.onContextCreated(Pointer.Unbox ptr)

    interface IRenderProcessHandler with
        member this.OnContextCreated(_, frame, context) = this.OnContextCreated(frame, context)
        member _.OnContextReleased(_, _, _) = ()
        member _.OnWebKitInitialized() = ()

[<EntryPoint>]
let main argv =
    let location = Assembly.GetExecutingAssembly().Location

    if String.IsNullOrEmpty location then
        -1
    else
        let asm =
            let path = Path.Combine(Path.GetDirectoryName location, "CefSharp.BrowserSubprocess.Core.dll")
            Assembly.LoadFrom path

        let run =
            let typ = asm.GetType("CefSharp.BrowserSubprocess.BrowserSubprocessExecutable")
            if isNull typ then raise <| TypeLoadException("Could not find BrowserSubprocessExecutable type.")

            let meth = typ.GetMethod("Main", [| typeof<string[]>; typeof<IRenderProcessHandler> |])
            if isNull meth then raise <| MissingMethodException(typ.Name, "Main")

            let inst = Activator.CreateInstance typ

            fun (argv: string[]) -> meth.Invoke(inst, [| argv; RenderProcessHandler() |]) |> unbox<int>

        run argv