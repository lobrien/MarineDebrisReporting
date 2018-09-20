namespace MarineDebrisReporting

open System.Diagnostics
open Fabulous.Core
open Fabulous.DynamicViews
open Fabulous.DynamicViews.MapsExtension
open Xamarin.Forms
open Xamarin.Forms.Maps
open Xamarin.Essentials
open Model 
open System
open Plugin.Media
open Plugin.Permissions
open System.Net

module App = 

    type Msg = 
        | DefaultReport 
        | LocationFound of Location
        | TakePhoto
        | PhotoTaken of Option<IO.Stream>
        | Error of string
        | MakeReport 
        | SubmissionResult of string
        | Reset


    let defaultReport : Report = {
        Timestamp = DateTime.Now;
        Location = None;
        Size = None;
        Material = None;
        Weight = None;
        Photo = None;
        Notes = None;
    }

    let weightPickerSource = 
        [| "Easy to pick up"; "One person could carry it a distance"; "One person could lift it"; "A couple people could carry it"; "Equipment necessary" |] 

    let sizePickerSource = 
        [| "Piece of trash"; "Pile of trash"; "Single large piece"; "Pile with large pieces"; "Ropey"; "Other / Unknown" |]

    let materialPickerSource = 
        [| "Plastic"; "Wood"; "Metal"; "Fiberglass"; "Fishing Gear"; "Assorted"; "Other / Unknown"|]

    let locationCmd = 
        async { 
            let! loc = Geolocation.GetLocationAsync() |> Async.AwaitTask
            return LocationFound loc
        } |> Cmd.ofAsyncMsg

    let init () = 

        let initModel = {
            MapRegion = new MapSpan(new Position(21.3, -157.9), 0.3, 0.3);
            Report = None
            Error = None
            }

        initModel, locationCmd


    let PhotoCaptureAsync = 
        async {
            let! successfulInit = CrossMedia.Current.Initialize() |> Async.AwaitTask
            match successfulInit, CrossMedia.Current.IsCameraAvailable, CrossMedia.Current.IsTakePhotoSupported with 
                | true, true, true  -> 
                    let options = new Plugin.Media.Abstractions.StoreCameraMediaOptions()
                    options.Name <- "MarineDebris.jpg"
                    options.Directory <- "HackTheSea"
                    let! file = CrossMedia.Current.TakePhotoAsync(options) |> Async.AwaitTask
                    match file <> null with 
                    | true -> 
                        return file.GetStreamWithImageRotatedForExternalStorage() |> Some |> PhotoTaken
                    | false -> 
                        return "Media capture did not work" |> Error
                | _ -> 
                    return "Media capture did not initialize" |> Error
        } |> Cmd.ofAsyncMsg

    let toJson report = 
        "{ }"

    let reportSubmissionAsync report = 
        async {
            let url = "https://127.0.0.1"
            let data = toJson(report)
            let uri = Uri(url)
            use webClient = new WebClient()
            let! result = webClient.UploadStringTaskAsync(uri, data) |> Async.AwaitTask
            return result
        }

    let SubmitReportAsync reportOption = 
        async {
            match reportOption with 
            | Some report -> 
                let! submissionResult = reportSubmissionAsync(report)
                return SubmissionResult submissionResult
            | None -> 
                return Error "Choose data to make report" 
        } |> Cmd.ofAsyncMsg

    let update msg model =
        match msg with
        | DefaultReport -> { model with Report = Some defaultReport }, Cmd.none
        | LocationFound loc -> 
            let report = match model.Report with 
                         | Some r -> { r with Location = Some loc }
                         | None -> { defaultReport with Location = Some loc }
            let newRegion = new MapSpan(new Position(loc.Latitude, loc.Longitude), 0.025, 0.025)
            { model with MapRegion = newRegion; Report = Some report }, Cmd.none
        | TakePhoto -> model, PhotoCaptureAsync
        | PhotoTaken photo -> 
            { model with Report = Some { model.Report.Value with Photo = photo }}, Cmd.none
        | MakeReport -> 
            model, SubmitReportAsync(model.Report)
        | SubmissionResult s -> { model with Error = Some s; Report = Some defaultReport }, Cmd.none
        | Reset -> init()
        | Error s -> { model with Error = Some s }, Cmd.none

    let view model dispatch =
        let errorMsg, errorVisible = match model.Error with 
                                     | Some e -> e, true
                                     | None -> "", false
        View.ContentPage(
          content = View.StackLayout(padding = 20.0, verticalOptions = LayoutOptions.Center,
            children = [
                View.Map(heightRequest = 320., widthRequest = 320., horizontalOptions = LayoutOptions.Center, requestedRegion = model.MapRegion )
                View.Picker(title = "Weight", itemsSource = weightPickerSource, horizontalOptions = LayoutOptions.Center)
                View.Picker(title = "Size", itemsSource = sizePickerSource, horizontalOptions = LayoutOptions.Center)
                View.Picker(title = "Material", itemsSource = materialPickerSource, horizontalOptions = LayoutOptions.Center)
                View.Button(text = "Take photo", command = (fun () -> dispatch TakePhoto), horizontalOptions = LayoutOptions.Center)
                View.Button(text = "Report it!", command = (fun () -> dispatch MakeReport), horizontalOptions = LayoutOptions.Center)
                View.Button(text = "Reset", horizontalOptions = LayoutOptions.Center, command = (fun () -> dispatch Reset))
                View.Label(text = errorMsg, isVisible = errorVisible, horizontalOptions = LayoutOptions.Center)
            ]))

    // Note, this declaration is needed if you enable LiveUpdate
    let program = Program.mkProgram init update view

type App () as app = 
    inherit Application ()

    let runner = 
        App.program
#if DEBUG
        |> Program.withConsoleTrace
#endif
        |> Program.runWithDynamicView app

#if DEBUG
    // Uncomment this line to enable live update in debug mode. 
    // See https://fsprojects.github.io/Fabulous/tools.html for further  instructions.
    //
    //do runner.EnableLiveUpdate()
#endif    

    // Uncomment this code to save the application state to app.Properties using Newtonsoft.Json
    // See https://fsprojects.github.io/Fabulous/models.html for further  instructions.
#if APPSAVE
    let modelId = "model"
    override __.OnSleep() = 

        let json = Newtonsoft.Json.JsonConvert.SerializeObject(runner.CurrentModel)
        Console.WriteLine("OnSleep: saving model into app.Properties, json = {0}", json)

        app.Properties.[modelId] <- json

    override __.OnResume() = 
        Console.WriteLine "OnResume: checking for model in app.Properties"
        try 
            match app.Properties.TryGetValue modelId with
            | true, (:? string as json) -> 

                Console.WriteLine("OnResume: restoring model from app.Properties, json = {0}", json)
                let model = Newtonsoft.Json.JsonConvert.DeserializeObject<App.Model>(json)

                Console.WriteLine("OnResume: restoring model from app.Properties, model = {0}", (sprintf "%0A" model))
                runner.SetCurrentModel (model, Cmd.none)

            | _ -> ()
        with ex -> 
            App.program.onError("Error while restoring model found in app.Properties", ex)

    override this.OnStart() = 
        Console.WriteLine "OnStart: using same logic as OnResume()"
        this.OnResume()
#endif


