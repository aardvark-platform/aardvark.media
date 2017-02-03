namespace Aardvark.Cef.WinForms


type Content =
    | Css of string
    | Javascript of string
    | Html of string
    | Binary of byte[]
    | Error


module Bootstrap =
    
    let private bootCode =
        System.IO.File.ReadAllText @"boot.js"

    let style (u : Map<string, string>) =
        Css """

        body {
            width: 100%;
            height: 100%;
            margin: 0px;
            padding: 0px;
            border: 0px;
        }

        div.aardvark {
            
        }

        canvas {
            cursor: default;
        }

        canvas:focus {
            
            outline: none;
        }

        """

    let boot (u : Map<string, string>) =
        Javascript bootCode
        