namespace Aardvark.UI

open System

type Dummy() = class end

module Icons =
    let private self = typeof<Dummy>.Assembly
    let aardvark =  
        use stream = self.GetManifestResourceStream "aardvark.ico"
        new System.Drawing.Icon(stream)