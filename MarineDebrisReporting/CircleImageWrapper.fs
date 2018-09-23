namespace MarineDebrisReporting

[<AutoOpen>]
module CircleImage =

    open Fabulous.DynamicViews
    open Xamarin.Forms
    open ImageCircle.Forms.Plugin.Abstractions

    let FileNameAttribKey = AttributeKey<string> "FileName"

    // Extend `View` (fully-qualified to avoid collisions) to use James Montemagno's nuget 
    type Fabulous.DynamicViews.View with
         static member inline CircleImage
             (fname,
             // inherited attributes common to all views
              ?horizontalOptions, ?verticalOptions, ?margin, ?gestureRecognizers, ?anchorX, ?anchorY, ?backgroundColor, ?heightRequest,
              ?inputTransparent, ?isEnabled, ?isVisible, ?minimumHeightRequest, ?minimumWidthRequest, ?opacity,
              ?rotation, ?rotationX, ?rotationY, ?scale, ?style, ?translationX, ?translationY, ?widthRequest,
              ?resources, ?styles, ?styleSheets, ?classId, ?styleId, ?automationId
             ) =

            // 1 new attribute (not optional, so hard-coded)
            let additionalAttributeCount = 1 // fname 
            let attribs =
                            View.BuildView(additionalAttributeCount, ?horizontalOptions=horizontalOptions, ?verticalOptions=verticalOptions,
                                           ?margin=margin, ?gestureRecognizers=gestureRecognizers, ?anchorX=anchorX, ?anchorY=anchorY,
                                           ?backgroundColor=backgroundColor, ?heightRequest=heightRequest, ?inputTransparent=inputTransparent,
                                           ?isEnabled=isEnabled, ?isVisible=isVisible, ?minimumHeightRequest=minimumHeightRequest,
                                           ?minimumWidthRequest=minimumWidthRequest, ?opacity=opacity, ?rotation=rotation,
                                           ?rotationX=rotationX, ?rotationY=rotationY, ?scale=scale, ?style=style,
                                           ?translationX=translationX, ?translationY=translationY, ?widthRequest=widthRequest,
                                           ?resources=resources, ?styles=styles, ?styleSheets=styleSheets, ?classId=classId, ?styleId=styleId, ?automationId=automationId)

            attribs.Add(FileNameAttribKey, fname)
            // The creation method
            // TODO: Expose these hard-coded values as attributes 
            let create () = 
                let img = new CircleImage()
                img.BorderColor <- Color.White
                img.BorderThickness <- float32 3
                img.HeightRequest <- match heightRequest with Some h -> h | None -> float 75
                img.WidthRequest <- match widthRequest with Some w -> w | None -> float 75
                img.Aspect <- Aspect.AspectFill
                img.HorizontalOptions <- match horizontalOptions with Some o -> o | None -> LayoutOptions.Center
                img.VerticalOptions <- match verticalOptions with Some o -> o | None -> LayoutOptions.Center
                img.Source <- FileImageSource.FromFile fname
                img

            // The incremental update method
            let update (prev: ViewElement voption) (source: ViewElement) (target: CircleImage) =
                View.UpdateView (prev, source, target)
                source.UpdatePrimitive (prev, target, FileNameAttribKey, (fun target f -> target.Source <- FileImageSource.FromFile f))

            ViewElement.Create<CircleImage>(create, update, attribs)
