namespace Aardvark.UI

type ReferenceKind =
    | Script
    | Stylesheet
    | Module

type Reference =
    { kind : ReferenceKind
      name : string
      url  : string }