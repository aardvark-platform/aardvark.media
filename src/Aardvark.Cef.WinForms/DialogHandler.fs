namespace Aardvark.Cef.WinForms

open System
open System.Collections.Generic
open System.Windows.Forms

type AardvarkDialogHandler(parent: Control) =
    static let javascript =
        """
            if (!document.aardvark) document.aardvark = {};

            document.aardvark.openFileDialog = async function (config, callback) {
                await CefSharp.BindObjectAsync("aardvarkDialogHandler");

                if (config.mode === "file") {
                    const filePaths = await aardvarkDialogHandler.showOpenFileDialog(config.title, config.allowMultiple, config.filters);
                    if (Array.isArray(filePaths) && filePaths.length > 0) callback(filePaths);
                } else {
                    const folderPath = await aardvarkDialogHandler.showOpenFolderDialog(config.title);
                    if (folderPath) callback([folderPath]);
                }
            }
        """

    static member internal Javascript = javascript

    member _.ShowOpenFolderDialog(title: string) =
        use dialog = new FolderBrowserDialog(Description = title)
#if NET8_0_OR_GREATER
        dialog.UseDescriptionForTitle <- true
#endif
        let mutable result = null

        parent.Invoke(Action (fun _ ->
            match dialog.ShowDialog(parent) with
            | DialogResult.OK -> result <- dialog.SelectedPath
            | _ -> ()
        )) |> ignore

        result

    member _.ShowOpenFileDialog(title: string, allowMultiple: bool, filters: IList<obj>) =
        use dialog = new OpenFileDialog(Title = title, Multiselect = allowMultiple)
        dialog.Filter <- "File|" + (filters |> Seq.map unbox<string> |> String.concat ";")

        let mutable result = Array.empty

        parent.Invoke(Action (fun _ ->
            match dialog.ShowDialog(parent) with
            | DialogResult.OK -> result <- dialog.FileNames
            | _ -> ()
        )) |> ignore

        result