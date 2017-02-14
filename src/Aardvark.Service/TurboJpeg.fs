namespace Aardvark.Service

open System
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Security

open Microsoft.FSharp.NativeInterop

open Aardvark.Base

#nowarn "9"
#nowarn "51"


type TJCompress =
    struct
        val mutable public Handle : nativeint

        static member Null = TJCompress(0n)

        member inline x.IsNull = x.Handle = 0n
        member inline x.IsValid = x.Handle <> 0n
    
        private new(v : nativeint) = { Handle = v }
    end

type TJDecompress =
    struct
        val mutable public Handle : nativeint

        member inline x.IsNull = x.Handle = 0n
        member inline x.IsValid = x.Handle <> 0n
    end

type TJPixelFormat =
    | RGB = 0
    | BGR = 1
    | RGBX = 2
    | BGRX = 3
    | XBGR = 4
    | XRGB = 5
    | GRAY = 6
    | RGBA = 7
    | BGRA = 8
    | ABGR = 9
    | ARGB = 10
    | CMYK = 11

type TJFlags =
    | None          = 0x00000
    | BottomUp      = 0x00002
    | FastUpSample  = 0x00100
    | NoRealloc     = 0x00400
    | FastDCT       = 0x00800
    | AccurateDCT   = 0x01000

type TJSubsampling =
    | S444 = 0
    | S422 = 1
    | S420 = 2
    | Gray = 3
    | S440 = 4
    | S411 = 5


module TurboJpegNative =
    [<Literal>]
    let lib = "turbojpeg"

    [<DllImport(lib); SuppressUnmanagedCodeSecurity>]
    extern TJCompress tjInitCompress()

    [<DllImport(lib); SuppressUnmanagedCodeSecurity>]
    extern TJDecompress tjInitDecompress()

    [<DllImport(lib); SuppressUnmanagedCodeSecurity>]
    extern int tjCompress2(TJCompress handle, void* srcBuf, int width, int pitch, int height, TJPixelFormat fmt, nativeint* jpegBuf, uint64* jpegSize, TJSubsampling jpegSubsamp, int jpegQual, TJFlags flags)
        
    [<DllImport(lib); SuppressUnmanagedCodeSecurity>]
    extern int tjDecompressHeader3(TJDecompress handle, void* jpegBuf, uint64 jpegSize, int* width, int* height, int* jpegSubsamp, int* jpegColorspace)
        
    [<DllImport(lib); SuppressUnmanagedCodeSecurity>]
    extern int tjDecompress2(TJDecompress handle, void* jpegBuf, uint64 jpegSize, void* dstBuf, int width, int height, TJPixelFormat fmt, TJFlags flags)

    [<DllImport(lib); SuppressUnmanagedCodeSecurity>]
    extern int tjDestroy(void* handle)

    [<DllImport(lib); SuppressUnmanagedCodeSecurity>]
    extern void tjFree(void* handle)

    [<DllImport(lib, CharSet = CharSet.Ansi); SuppressUnmanagedCodeSecurity>]
    [<MarshalAs(UnmanagedType.LPStr)>]
    extern string tjGetErrorStr()

type TJCompressor() =
    let mutable handle = TurboJpegNative.tjInitCompress()

    member x.Compress(srcData : nativeint, srcStride : int, srcWidth : int, srcHeight : int, fmt : TJPixelFormat, subsamp : TJSubsampling, quality : int, flags : TJFlags) =
        let mutable buffer = 0n
        let mutable bufferSize = 0UL

        try
            let result = 
                TurboJpegNative.tjCompress2(
                    handle,
                    srcData, srcWidth, srcStride, srcHeight,
                    fmt, &&buffer, &&bufferSize,
                    subsamp, quality, flags
                )
        
            if result = -1 then
                let err = TurboJpegNative.tjGetErrorStr()
                failwithf "[TJ] %s" err

            let data : byte[] = Array.zeroCreate (int bufferSize)
            Marshal.Copy(buffer, data, 0, data.Length)
            data

        finally
            TurboJpegNative.tjFree buffer
            
    member private x.Dispose(disposing : bool) =
        if disposing then GC.SuppressFinalize(x)
        if handle.IsValid then
            TurboJpegNative.tjDestroy(handle.Handle) |> ignore
            handle <- TJCompress.Null

    member x.Dispose() = x.Dispose(true)
    override x.Finalize() = x.Dispose(false)

    interface IDisposable with
        member x.Dispose() = x.Dispose()

