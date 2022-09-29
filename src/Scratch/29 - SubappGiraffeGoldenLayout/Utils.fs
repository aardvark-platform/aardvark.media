namespace Test.UI

open System
open Adaptify
open Adaptify.FSharp.Core
open FSharp.Data.Adaptive
open Aardvark.Base
open Aardvark.UI
open Aardvark.UI.Primitives
   

module Style =
    let styles lst =
        style (String.concat ";" lst)

    let marginBottomRight =
        String.concat ";" [
            "margin-bottom: 0.5rem"
            "margin-right: 0.5rem"
        ]
        
    let centreContent =
        String.concat ";" [
            "display: flex"
            "align-items: center"
            "justify-content: center"
        ] 

    let roundedBorder =
        String.concat ";" [
                "border-radius: 0.28571429rem"
                "border: 1px solid rgba(255, 255, 255, 0.15)"
            ]        

    let withRoundedBorder otherStyles =
        String.concat ";" [
                otherStyles
                roundedBorder
            ]
            
    let floating = 
        "float: left"
            
    let withFloating otherStyles =
        String.concat ";" [
                otherStyles
                floating
            ]

    let defaultBg =
        "background: rgb(27,28,29)"

    let withDefaultBg otherStyles =
        String.concat ";" [
                otherStyles
                defaultBg
        ]

module To =
    open Style    

    /// creates an incremental div from the content
    let divA content =
        Incremental.div AttributeMap.empty content

    /// creates an incremental div from the content
    let divAWithClass classString content =
        Incremental.div ([clazz classString] |> AttributeMap.ofList) content

   
    /// creates an incremental div from the content
    let divAval content =
        alist {
            let! content = content
            yield content
        } |> divA

    /// creates an incremental div from the content
    let divAvalWithClass classString content =
        alist {
            let! content = content
            yield content
        } |> divAWithClass classString
                        

    /// creates an incremental div from the content using the given mapping
    /// function mapper
    let divAMap (value : aval<'a>) (mapper : 'a -> DomNode<'msg>) =
        alist {
            let! value = value
            mapper value
        } |> divA

    /// creates an incremental div that contains ifSome if the option 
    /// is Some and isNone if the option is None
    let optDivA (opt    : aval<option<'a>>) 
                (ifSome : 'a -> DomNode<'msg>) 
                (ifNone : DomNode<'msg>) =
        let content = 
            alist {
                let! opt = opt
                match opt with
                | Some opt -> 
                    yield ifSome opt
                | none -> 
                    yield ifNone
            }
        divA content

    /// creates an incremental div that contains ifSome if the 
    /// AdaptiveOptionCase is Some and isNone if the option is None
    let optDivAoc (opt    : aval<AdaptiveOptionCase<'value, 'adaptiveValue, 'adaptiveValue>>) 
                  (ifSome : 'adaptiveValue -> DomNode<'msg>) 
                  (ifNone : DomNode<'msg>) =
        let content = 
            alist {
                let! opt = opt
                match opt with
                | AdaptiveSome value -> 
                    ifSome value
                | AdaptiveNone -> 
                    ifNone
            }
        divA content


        

    let segment content = 
        // style "padding: 0.0rem"
         div [clazz "ui inverted attached segment"] [
            content
         ]

    let segmentWithoutPadding content = 
        // style "padding: 0.0rem"
         div [clazz "ui inverted attached segment";style "padding: 0.0rem"] [
            content
         ]

    /// adaptive compact segment
    let segmentCompactA content = 
        Incremental.div ([clazz "ui compact inverted segment"] 
                            |> AttributeMap.ofList) 
                        content 

    
    let segmentCompact content = 
        div [clazz "ui compact inverted segment"] content 

    let placeholderSegment (iconString : string) (infoText : string) 
                           (content : list<DomNode<'msg>>) =
        div [clazz "ui placeholder segment"] [
            div [clazz "ui icon header"] [
                i [clazz iconString] []
                text infoText
            ]
                //button [clazz "ui primary button"; onMouseClick (fun _ -> LoadData)]
                //       [text "Get Data"]
            div [clazz "inline"] content
        ]

    let labeledColumn label content =
        div [clazz "ui column"; styles [roundedBorder;marginBottomRight]] [
            div [clazz "ui basic inverted top attached label"] [text label]
            content
        ] 

    /// adaptive horizontal menu
    let horizontalMenuA (content : alist<DomNode<'a>>) =
        let items =
            alist {
                for c in content do
                    yield div [clazz "item"] [ c ]
            }
            
        Incremental.div ([clazz "ui inverted menu";
                            style "width: 100%"] |> AttributeMap.ofList)
                        // [div [clazz "ui basic inverted top attached label"] [text "Vehicle Selection"]]@
                        items

    let horizontalMenu (content : list<DomNode<'a>>) =
        let items = 
            seq {
                    for c in content do
                        yield div [clazz "item"] [ c ]
                } |> Seq.toList
        div [clazz "ui inverted menu";style "width: 100%"] 
            items

    /// adaptive menu
    let menuA (content : alist<DomNode<'a>>) (label : string) =
        let items =
            alist {
                yield div [clazz "header item"] [text label]
                for c in content do
                    yield div [clazz "item"] [ c ]
            }
            
        Incremental.div ([clazz "ui secondary vertical inverted menu";
                            style "width: 100%"] |> AttributeMap.ofList)
                        // [div [clazz "ui basic inverted top attached label"] [text "Vehicle Selection"]]@
                        items

    let menu  (label : string) (content : list<DomNode<'a>>)=
        let items = 
            seq {
                    for c in content do
                        yield div [clazz "item"] [ c ]
                } |> Seq.toList
        let label =
            if label.Length <= 0 then
                []
            else 
                [div [clazz "header item"]  [text label]]
        div [clazz "ui secondary vertical inverted menu";style "width: 100%"] 
            (label@items)
              
    /// adaptive labeled input
    let labeledInputA ((labelBefore) : option<aval<string> * Attribute<'a>>)
                             (labelAfter  : option<aval<string>  * Attribute<'a>>) 
                             (numeric) =
        let content = 
            seq {
                if labelBefore.IsSome then
                    let labelBefore, labelStyle = labelBefore.Value
                    yield div [clazz "ui basic inverted label";labelStyle] 
                              [Incremental.text labelBefore]
                yield numeric
                if labelAfter.IsSome then
                    let labelAfter, labelStyle = labelAfter.Value
                    yield div [clazz "ui basic inverted label"; labelStyle]
                              [Incremental.text labelAfter]
            }
            
        div [clazz "ui inverted labeled input"] 
            (content |> List.ofSeq)

    let labeledInput (labelBefore : option<string * Attribute<'a>>)
                     (labelAfter  : option<string * Attribute<'a>>)
                     (numeric) =
        let content =
            seq {
                    if labelBefore.IsSome then
                        let labelBefore, labelStyle = labelBefore.Value
                        yield div [clazz "ui basic inverted label";labelStyle] 
                                  [text labelBefore]
                    yield numeric
                    if labelAfter.IsSome then
                        let labelAfter, labelStyle = labelAfter.Value
                        yield div [clazz "ui basic inverted label";labelStyle] 
                                  [text labelAfter]
                }
            
        div [clazz "ui inverted labeled input"]
            (content |> List.ofSeq)
                
        
module Insert =
    let labelSimple txt = // TODO refactor simple/normal
        div [clazz "ui large label"] 
            [text txt]

    let labelSimpleA txt =
        div [clazz "ui large label"] 
            [Incremental.text txt]

    let bigLabelSimple txt =
        div [clazz "ui big label"] 
            [text txt]

    let label txt = 
        div [clazz "ui basic inverted label"] 
            [text txt]

    let labelWithStyle txt st =
        div [clazz "ui basic inverted label";st] 
            [text txt]

    /// create a header with the given icon string (e.g. "settings icon")
    /// and text (e.g. "Settings")
    let header icon headerText =
        h3 [clazz "ui inverted top attached header"] [
                    i [clazz icon] []
                    div [clazz  "content"] [text headerText]
        ]

    /// create a header with the given text
    let placeHolderText txt =
        div [clazz "ui inverted message"] [
                    div [clazz "header"] [
                        text txt
                    ]
        ]

    /// creates a dropdown menu from an enum
    /// 'T is the enum type
    /// 'M is the type of the message
    let dropdownEnum<'T, 'M> (selected : aval<'T>) (msg : 'T -> 'M) = 
        let enumValues = 
            AMap.ofArray((Enum.GetValues typeof<'T> :?> ('T [])) 
                            |> Array.map (fun c -> (c, (text (Enum.GetName(typeof<'T>, c)) ))))
        dropdownUnClearable [ clazz "ui inverted selection dropdown" ] 
                            (enumValues) selected msg

    let loader (isLoading : aval<bool>) (label : string) =
        alist {
            let! isLoading = isLoading
            if isLoading then 
                div [clazz "ui active dimmer"] [
                    div [clazz "ui large slow text loader"; style "background-image: none"] [
                        text label
                    ]
                ]
            else 
                div [clazz "ui inactive dimmer"] [
                    div [clazz "ui large slow text loader";style "background-image: none"] [
                        text label
                    ]
                ]
        }