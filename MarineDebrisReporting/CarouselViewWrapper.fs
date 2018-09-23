namespace MarineDebrisReporting

[<AutoOpen>]
module CarouselView =

    open Fabulous.DynamicViews
    open Xamarin.Forms
    open Xamarin.Forms
    open System.Collections.ObjectModel

    (* What follows is VERY INCOMPLETE based on the `ListView` implementation in Fabulous *)

    let ItemsAttribKey = AttributeKey<_> "Items"
//    let CurrentItemAttribKey = AttributeKey<_> "Item"
    let CurrentItemIndexAttribKey = AttributeKey<_> "Position"

    type CustomCarouselView() = 
        inherit CarouselView(ItemTemplate=DataTemplate(typeof<ViewElementCell>))

    let updateCarouselViewItems (prevCollOpt: seq<'T> voption) (collOpt: seq<'T> voption) (target: Xamarin.Forms.CarouselView) = 
        let targetColl = 
            match target.ItemsSource with 
            | :? ObservableCollection<ListElementData<'T>> as oc -> oc
            | _ -> 
                let oc = ObservableCollection<ListElementData<'T>>()
                target.ItemsSource <- oc
                oc
        updateCollectionGeneric (ValueOption.map seqToArray prevCollOpt) (ValueOption.map seqToArray collOpt) targetColl ListElementData (fun _ _ _ -> ()) (fun _ _ -> false) (fun _ _ _ -> failwith "no element reuse") 


    let updateCarouselView (prevOpt: ViewElement voption, curr: ViewElement, target: Xamarin.Forms.CarouselView) = 
        // update the inherited View element
        let baseElement = (if View.ProtoView.IsNone then View.ProtoView <- Some (View.View())); View.ProtoView.Value
        baseElement.UpdateInherited (prevOpt, curr, target)
        let mutable prevCarouselViewItemsOpt = ValueNone
        let mutable currCarouselViewItemsOpt = ValueNone
//        let mutable prevCarouselView_CurrentItemOpt = ValueNone
//        let mutable currCarouselView_CurrentItemOpt = ValueNone
        let mutable prevCarouselView_SelectedItemIndexOpt = ValueNone
        let mutable currCarouselView_SelectedItemIndexOpt = ValueNone
        for kvp in curr.AttributesKeyed do
            if kvp.Key = ItemsAttribKey.KeyValue then 
                currCarouselViewItemsOpt <- ValueSome (kvp.Value :?> seq<ViewElement>)
//            if kvp.Key = CurrentItemAttribKey.KeyValue then 
//                currCarouselView_CurrentItemOpt <- ValueSome (kvp.Value :?> System.EventHandler<Xamarin.Forms.SelectedItemChangedEventArgs>)
            if kvp.Key = CurrentItemIndexAttribKey.KeyValue then 
                currCarouselView_SelectedItemIndexOpt <- ValueSome (kvp.Value :?> int)
        match prevOpt with
        | ValueNone -> ()
        | ValueSome prev ->
            for kvp in prev.AttributesKeyed do
                if kvp.Key = ItemsAttribKey.KeyValue then 
                    prevCarouselViewItemsOpt <- ValueSome (kvp.Value :?> seq<ViewElement>)
//                if kvp.Key = CurrentItemAttribKey.KeyValue then
//                    prevCarouselView_CurrentItemOpt <- ValueSome (kvp.Value :?> System.EventHandler<Xamarin.Forms.SelectedItemChangedEventArgs>)
                if kvp.Key = CurrentItemIndexAttribKey.KeyValue then 
                    prevCarouselView_SelectedItemIndexOpt <- ValueSome (kvp.Value :?> int)

        updateCarouselViewItems prevCarouselViewItemsOpt currCarouselViewItemsOpt target
(*
TODO: I think you _ought_ to be able to assign to `Item` 
        match prevCarouselView_CurrentItemOpt, currCarouselView_CurrentItemOpt with
        | ValueSome prevValue, ValueSome currValue when prevValue = currValue -> ()
        | _, ValueSome currValue -> target.Item <- (function None -> null | Some i -> let items = target.ItemsSource :?> System.Collections.Generic.IList<ListElementData<ViewElement>> in if i >= 0 && i < items.Count then items.[i] else null)  currValue
        | ValueSome _, ValueNone -> target.Item <- null; 
        | ValueNone, ValueNone -> ()
*)
        match prevCarouselView_SelectedItemIndexOpt, currCarouselView_SelectedItemIndexOpt with 
        | ValueSome prevValue, ValueSome currValue when prevValue = currValue -> ()
        | _, ValueSome currValue -> target.Position <- currValue 
        | ValueSome _, ValueNone -> target.Position <- 0
        | ValueNone, ValueNone -> ()

    let createCarouselView () : Xamarin.Forms.CarouselView = 
               upcast (new CustomCarouselView())

    let createFuncCarouselView : (unit -> Xamarin.Forms.CarouselView) = (fun () -> createCarouselView())

    let updateFuncCarouselView = (fun (prevOpt: ViewElement voption) (curr: ViewElement) (target: Xamarin.Forms.CarouselView) -> updateCarouselView (prevOpt, curr, target)) 

    // Extend `View` (fully-qualified to avoid collisions) to use CarouselView 
    type Fabulous.DynamicViews.View with

        static member inline BuildCarouselView(attribCount: int, ?items: ViewElement list, ?selectedItem: int, ?horizontalOptions : Xamarin.Forms.LayoutOptions, ?verticalOptions : Xamarin.Forms.LayoutOptions, ?margin : obj, ?gestureRecognizers: ViewElement list, ?anchorX: double, ?anchorY: double, ?backgroundColor: Xamarin.Forms.Color, ?heightRequest: double, ?inputTransparent: bool, ?isEnabled: bool, ?isVisible: bool, ?minimumHeightRequest: double, ?minimumWidthRequest: double, ?opacity: double, ?rotation: double, ?rotationX: double, ?rotationY: double, ?scale: double, ?style: Xamarin.Forms.Style, ?translationX: double, ?translationY: double, ?widthRequest: double, ?resources: (string * obj) list, ?styles: Xamarin.Forms.Style list, ?styleSheets: Xamarin.Forms.StyleSheets.StyleSheet list, ?classId: string, ?styleId: string, ?automationId: string) = 
            let attribCount = match items with Some _ -> attribCount + 1 | None -> attribCount
            let attribCount = match selectedItem with Some _ -> attribCount + 1 | None -> attribCount

            let attribBuilder = View.BuildView(attribCount, ?margin=margin, ?gestureRecognizers=gestureRecognizers, ?anchorX=anchorX, ?anchorY=anchorY, ?backgroundColor=backgroundColor, ?heightRequest=heightRequest, ?inputTransparent=inputTransparent, ?isEnabled=isEnabled, ?isVisible=isVisible, ?minimumHeightRequest=minimumHeightRequest, ?minimumWidthRequest=minimumWidthRequest, ?opacity=opacity, ?rotation=rotation, ?rotationX=rotationX, ?rotationY=rotationY, ?scale=scale, ?style=style, ?translationX=translationX, ?translationY=translationY, ?widthRequest=widthRequest, ?resources=resources, ?styles=styles, ?styleSheets=styleSheets, ?classId=classId, ?styleId=styleId, ?automationId=automationId)
            match items with None -> () | Some v -> attribBuilder.Add(ItemsAttribKey, (v)) 
            match selectedItem with None -> () | Some v -> attribBuilder.Add(CurrentItemIndexAttribKey, (v)) 
            attribBuilder

        static member inline CarouselView
             (
              ?items, ?selectedItemIndex,
              // inherited attributes common to all views
              ?horizontalOptions, ?verticalOptions, ?margin, ?gestureRecognizers, ?anchorX, ?anchorY, ?backgroundColor, ?heightRequest,
              ?inputTransparent, ?isEnabled, ?isVisible, ?minimumHeightRequest, ?minimumWidthRequest, ?opacity,
              ?rotation, ?rotationX, ?rotationY, ?scale, ?style, ?translationX, ?translationY, ?widthRequest,
              ?resources, ?styles, ?styleSheets, ?classId, ?styleId, ?automationId
             ) =


                let attribBuilder = View.BuildCarouselView(0, ?items=items, ?selectedItem = selectedItemIndex, ?horizontalOptions=horizontalOptions, ?verticalOptions=verticalOptions, ?margin=margin, ?gestureRecognizers=gestureRecognizers, ?anchorX=anchorX, ?anchorY=anchorY, ?backgroundColor=backgroundColor, ?heightRequest=heightRequest, ?inputTransparent=inputTransparent, ?isEnabled=isEnabled, ?isVisible=isVisible, ?minimumHeightRequest=minimumHeightRequest, ?minimumWidthRequest=minimumWidthRequest, ?opacity=opacity, ?rotation=rotation, ?rotationX=rotationX, ?rotationY=rotationY, ?scale=scale, ?style=style, ?translationX=translationX, ?translationY=translationY, ?widthRequest=widthRequest, ?resources=resources, ?styles=styles, ?styleSheets=styleSheets, ?classId=classId, ?styleId=styleId, ?automationId=automationId)

                ViewElement.Create<Xamarin.Forms.CarouselView>(createFuncCarouselView, updateFuncCarouselView, attribBuilder)
