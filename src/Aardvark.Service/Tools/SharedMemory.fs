namespace Aardvark.Base

open System
open System.IO.MemoryMappedFiles

type ISharedMemory =
    inherit IDisposable
    abstract member Name : string
    abstract member Pointer : nativeint
    abstract member Size : int64


module SharedMemory =
    open System.Runtime.InteropServices

    [<System.Diagnostics.CodeAnalysis.SuppressMessage("NameConventions", "*")>]
    module private Windows =
        type private MappingInfo =
            {
                name : string
                file : MemoryMappedFile
                view : MemoryMappedViewAccessor
                size : int64
                data : nativeint
            }
            interface ISharedMemory with
                member x.Name = x.name
                member x.Dispose() =
                    x.view.Dispose()
                    x.file.Dispose()
                member x.Pointer = x.data
                member x.Size = x.size

        let create (name : string) (size : int64) =
            let file = MemoryMappedFile.CreateOrOpen(name, size)
            let view = file.CreateViewAccessor()

            {
                name = name
                file = file
                view = view
                size = size
                data = view.SafeMemoryMappedViewHandle.DangerousGetHandle()
            } :> ISharedMemory

    [<System.Diagnostics.CodeAnalysis.SuppressMessage("NameConventions", "")>]
    module private Posix =


        [<Flags>]
        type Protection =
            | Read = 0x01
            | Write = 0x02
            | Execute = 0x04

            | ReadWrite = 0x03
            | ReadExecute = 0x05
            | ReadWriteExecute = 0x07

        [<StructLayout(LayoutKind.Sequential); StructuredFormatDisplay("{AsString}")>]
        type FileHandle =
            struct
                val mutable public Id : int
                override x.ToString() = sprintf "f%d" x.Id
                member private x.AsString = x.ToString()
                member x.IsValid = x.Id >= 0
            end        

        [<StructLayout(LayoutKind.Sequential); StructuredFormatDisplay("{AsString}")>]
        type Permission =
            struct
                val mutable public Mask : uint16

                member x.Owner
                    with get() = 
                        (x.Mask >>> 6) &&& 7us |> int |> unbox<Protection>
                    and set (v : Protection) =  
                        x.Mask <- (x.Mask &&& 0xFE3Fus) ||| ((uint16 v &&& 7us) <<< 6)

                member x.Group
                    with get() = 
                        (x.Mask >>> 3) &&& 7us |> int |> unbox<Protection>
                    and set (v : Protection) =  
                        x.Mask <- (x.Mask &&& 0xFFC7us) ||| ((uint16 v &&& 7us) <<< 3)

                member x.Other
                    with get() = 
                        (x.Mask) &&& 7us |> int |> unbox<Protection>
                    and set (v : Protection) =  
                        x.Mask <- (x.Mask &&& 0xFFF8us) ||| (uint16 v &&& 7us)


                member private x.AsString = x.ToString()
                override x.ToString() =
                    let u = x.Owner
                    let g = x.Group
                    let o = x.Other

                    let inline str (p : Protection) =
                        (if p.HasFlag Protection.Execute then "x" else "-") +
                        (if p.HasFlag Protection.Write then "w" else "-") +
                        (if p.HasFlag Protection.Read then "r" else "-")

                    str u + str g + str o

                new(u : Protection, g : Protection, o : Protection) =
                    {
                        Mask = ((uint16 u &&& 7us) <<< 6) ||| ((uint16 g &&& 7us) <<< 3) ||| (uint16 o &&& 7us)
                    }

            end




        [<System.Diagnostics.CodeAnalysis.SuppressMessage("NameConventions", "")>]
        module Mac =
            [<Flags>]        
            type MapFlags =    
                | Shared = 0x0001
                | Private = 0x0002
                | Fixed = 0x0010
                | Rename = 0x0020
                | NoReserve = 0x0040
                | NoExtend = 0x0100
                | HasSemaphore = 0x0200
                | NoCache = 0x0400
                | Jit = 0x0800
                | Anonymous = 0x1000 

            [<Flags>] 
            type SharedMemoryFlags =
                | SharedLock = 0x0010
                | ExclusiveLock = 0x0020
                | Async = 0x0040
                | NoFollow = 0x0100
                | Create = 0x0200
                | Truncate = 0x0400
                | Exclusive = 0x0800
                | NonBlocking = 0x0004
                | Append = 0x0008        

                | ReadOnly = 0x0000
                | WriteOnly = 0x0001
                | ReadWrite = 0x0002

            [<DllImport("libc", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl, SetLastError=true, EntryPoint="shm_open")>]
            extern int shmopen(string name, SharedMemoryFlags oflag, Permission mode)
            
            // see https://github.com/dotnet/runtime/issues/48752
            [<DllImport("libc", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl, SetLastError=true, EntryPoint="shm_open")>]
            extern int shmopenArm64(string name, SharedMemoryFlags oflag, int c, int d, int e, int f, int g, int h, Permission mode)
            
            [<DllImport("libc", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl, SetLastError=true)>]
            extern nativeint mmap(nativeint addr, unativeint size, Protection prot, MapFlags flags, int fd, unativeint offset)

            [<DllImport("libc", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl, SetLastError=true)>]
            extern int munmap(nativeint ptr, unativeint size)

            [<DllImport("libc", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl, SetLastError=true, EntryPoint="shm_unlink")>]
            extern int shmunlink(string name)

            [<DllImport("libc", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl, SetLastError=true)>]
            extern int ftruncate(int fd, unativeint size)

            [<DllImport("libc", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl, SetLastError=true)>]
            extern int close(int fd)
            
            [<DllImport("libc", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl, SetLastError=true, EntryPoint="strerror")>]
            extern nativeint strerrorInternal(int code)

            let inline strerror (code : int) =
                strerrorInternal code |> Marshal.PtrToStringAnsi


            let create (name : string) (size : int64) =
                // open the shared memory (or create if not existing)
                let mapName = name
                //let mapName = "" + name
                shmunlink(mapName) |> ignore
                
                let flags = SharedMemoryFlags.Truncate ||| SharedMemoryFlags.Create ||| SharedMemoryFlags.ReadWrite
                let perm = Permission(Protection.ReadWriteExecute, Protection.ReadWriteExecute, Protection.ReadWriteExecute)

                let fd = 
                    if RuntimeInformation.ProcessArchitecture = Architecture.Arm64 then
                        // see https://github.com/dotnet/runtime/issues/48752
                        shmopenArm64(mapName, flags, 0, 0, 0, 0, 0, 0, perm)
                    else 
                        shmopen(mapName, flags, perm)
                        
                if fd <= 0 then 
                    let err = Marshal.GetLastWin32Error() |> strerror
                    failwithf "[SharedMemory] could not open \"%s\" (ERROR: %s)" name err

                // set the size
                if ftruncate(fd, unativeint size) <> 0 then 
                    let err = Marshal.GetLastWin32Error() |> strerror
                    shmunlink(mapName) |> ignore
                    failwithf "[SharedMemory] could resize \"%s\" to %d bytes (ERROR: %s)" name size err

                // map the memory into our memory
                let ptr = mmap(0n, unativeint size, Protection.ReadWrite, MapFlags.Shared, fd, 0un)
                if ptr = -1n then 
                    let err = Marshal.GetLastWin32Error() |> strerror
                    shmunlink(mapName) |> ignore
                    failwithf "[SharedMemory] could not map \"%s\" (ERROR: %s)" name err

                { new ISharedMemory with
                    member x.Name = mapName
                    member x.Pointer = ptr
                    member x.Size = size
                    member x.Dispose() =
                        let err = munmap(ptr, unativeint size)
                        if err <> 0 then
                            let err = Marshal.GetLastWin32Error() |> strerror
                            close(fd) |> ignore
                            shmunlink(mapName) |> ignore
                            failwithf "[SharedMemory] could not unmap \"%s\" (ERROR: %s)" name err

                        if close(fd) <> 0 then
                            let err = Marshal.GetLastWin32Error() |> strerror
                            shmunlink(mapName) |> ignore
                            failwithf "[SharedMemory] could not close \"%s\" (ERROR: %s)" name err

                        let err = shmunlink(mapName)
                        if err <> 0 then
                            let err = Marshal.GetLastWin32Error() |> strerror
                            failwithf "[SharedMemory] could not unlink %s (ERROR: %s)" name err
                }


        [<System.Diagnostics.CodeAnalysis.SuppressMessage("NameConventions", "")>]
        module Linux =
            [<Flags>]
            type MapFlags =
                | Shared = 0x1
                | Private = 0x2
                | Fixed = 0x10

            [<Flags>]
            type SharedMemoryFlags =
                | Create = 0x40
                | Truncate = 0x200
                | Exclusive = 0x80
                | ReadOnly = 0x0
                | WriteOnly = 0x1
                | ReadWrite = 0x2

            [<DllImport("librt.so.1", CharSet = CharSet.Ansi, SetLastError=true, EntryPoint="shm_open")>]
            extern FileHandle shmopen(string name, SharedMemoryFlags oflag, Permission mode)
            
            [<DllImport("libc", CharSet = CharSet.Ansi, SetLastError=true)>]
            extern nativeint mmap(nativeint addr, unativeint size, Protection prot, MapFlags flags, FileHandle fd, unativeint offset)

            [<DllImport("libc", CharSet = CharSet.Ansi, SetLastError=true)>]
            extern int munmap(nativeint ptr, unativeint size)

            [<DllImport("librt.so.1", CharSet = CharSet.Ansi, SetLastError=true, EntryPoint="shm_unlink")>]
            extern int shmunlink(string name)

            [<DllImport("libc", CharSet = CharSet.Ansi, SetLastError=true)>]
            extern int ftruncate(FileHandle fd, unativeint size)

            [<DllImport("libc", CharSet = CharSet.Ansi, SetLastError=true)>]
            extern int close(FileHandle fd)
            
            [<DllImport("libc", CharSet = CharSet.Ansi, SetLastError=true, EntryPoint="strerror")>]
            extern nativeint strerrorInternal(int code)

            let inline strerror (code : int) =
                strerrorInternal code |> Marshal.PtrToStringAnsi


            let create (name : string) (size : int64) =
                // open the shared memory (or create if not existing)
                let mapName = "/" + name;
                shmunlink(mapName) |> ignore
                
                let flags = SharedMemoryFlags.Truncate ||| SharedMemoryFlags.Create ||| SharedMemoryFlags.ReadWrite
                let perm = Permission(Protection.ReadWriteExecute, Protection.ReadWriteExecute, Protection.ReadWriteExecute)

                let fd = shmopen(mapName, flags, perm)
                if not fd.IsValid then 
                    let err = Marshal.GetLastWin32Error() |> strerror
                    failwithf "[SharedMemory] could not open \"%s\" (ERROR: %s)" name err

                // set the size
                if ftruncate(fd, unativeint size) <> 0 then 
                    let err = Marshal.GetLastWin32Error() |> strerror
                    shmunlink(mapName) |> ignore
                    failwithf "[SharedMemory] could resize \"%s\" to %d bytes (ERROR: %s)" name size err

                // map the memory into our memory
                let ptr = mmap(0n, unativeint size, Protection.ReadWrite, MapFlags.Shared, fd, 0un)
                if ptr = -1n then 
                    let err = Marshal.GetLastWin32Error() |> strerror
                    shmunlink(mapName) |> ignore
                    failwithf "[SharedMemory] could not map \"%s\" (ERROR: %s)" name err

                { new ISharedMemory with
                    member x.Name = name
                    member x.Pointer = ptr
                    member x.Size = size
                    member x.Dispose() =
                        let err = munmap(ptr, unativeint size)
                        if err <> 0 then
                            let err = Marshal.GetLastWin32Error() |> strerror
                            close(fd) |> ignore
                            shmunlink(mapName) |> ignore
                            failwithf "[SharedMemory] could not unmap \"%s\" (ERROR: %s)" name err

                        if close(fd) <> 0 then
                            let err = Marshal.GetLastWin32Error() |> strerror
                            shmunlink(mapName) |> ignore
                            failwithf "[SharedMemory] could not close \"%s\" (ERROR: %s)" name err

                        let err = shmunlink(mapName)
                        if err <> 0 then
                            let err = Marshal.GetLastWin32Error() |> strerror
                            failwithf "[SharedMemory] could not unlink %s (ERROR: %s)" name err
                }

    let create (name : string) (size : int64) =
        if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then 
            Windows.create name size
        elif RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then
            Posix.Mac.create name size        
        elif RuntimeInformation.IsOSPlatform(OSPlatform.Linux) then
            Posix.Linux.create name size
        else
            failwith "[SharedMemory] unknown platform"