namespace Aardvark.UI

type Unpersist<'model, 'mmodel> =
    {
        create : 'model -> 'mmodel
        update : 'mmodel -> 'model -> unit
    }

module Unpersist =
    let inline instance<'model, 'mmodel when 'mmodel : (static member Create : 'model -> 'mmodel)
                                         and 'mmodel : (member Update : 'model -> unit)> =
        {
            create = 'mmodel.Create
            update = _.Update
        }