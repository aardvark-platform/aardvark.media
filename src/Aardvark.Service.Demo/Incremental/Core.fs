namespace Aardvark.Base


type Monoid<'ops> =
    {
        misEmpty    : 'ops -> bool
        mempty      : 'ops
        mappend     : 'ops -> 'ops -> 'ops
    }

type Traceable<'s, 'ops> =
    {
        ops         : Monoid<'ops>
        empty       : 's
        apply       : 's -> 'ops -> 's * 'ops
        compute     : 's -> 's -> 'ops
        collapse    : 's -> int -> bool
    }
