namespace Aardvark.UI

open MBrace.FsPickler.Json

module Pickler =
    let json = FsPickler.CreateJsonSerializer(false, true)
    let unpickleOfJson<'T> (str : string) : 'T = json.UnPickleOfString<'T> str
    let jsonToString<'T> (value : 'T) : string = json.PickleToString value