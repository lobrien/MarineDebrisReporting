namespace MarineDebrisReporting

open Fabulous.Core
open Fabulous.DynamicViews
open Xamarin.Forms
open Xamarin.Forms.Maps
open Xamarin.Essentials
open System
open Plugin.Media
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Blob
open Microsoft.WindowsAzure.Storage.Table

open Model 
open ImageCircle

module App = 

    type Msg = 
        | DefaultReport 
        | LocationFound of Location
        | WeightPicked of DebrisWeightT
        | SizePicked of DebrisSizeT
        | MaterialPicked of DebrisMaterialT
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


    // Put directly in Azure blob storage 
    let photoSubmissionAsync cloudStorageAccount imageType (maybePhoto : IO.Stream option) imageName = 
        async {
            match maybePhoto with 
            | Some byteStream -> 
                let containerUrl = "https://hackthesea.blob.core.windows.net/marinedebrispix"
                let containerName = "marinedebrispix"
                let csa = CloudStorageAccount.Parse "***CONNECTION STRING***"
                let ctb = csa.CreateCloudBlobClient()
                let container = ctb.GetContainerReference containerName
                let blob = container.GetBlockBlobReference(imageName) //|> Async.AwaitTask
                blob.Properties.ContentType <- imageType
                do! blob.UploadFromStreamAsync(byteStream) |> Async.AwaitTask
                return true
               | None -> return false
        }

    // Put directly in Azure table storage
    let reportSubmissionAsync (cloudStorageAccount : CloudStorageAccount) report photoName = 
        async {
            let ctc = cloudStorageAccount.CreateCloudTableClient() 
            let table = ctc.GetTableReference("MarineDebris")

            let record = new ReportStorage(report)
            let insertOperation = record |> TableOperation.Insert
            let! tr = table.ExecuteAsync(insertOperation) |> Async.AwaitTask
            return tr.Etag |> Some
        } 

    let SubmitReportAsync reportOption = 
        async {
            match reportOption with 
            | Some report -> 
                let photoName = sprintf "%s.jpg" <| report.Timestamp.ToString("o")
                let csa = CloudStorageAccount.Parse("***CONNECTION STRING***")
                let! photoResult = photoSubmissionAsync csa "image/jpeg" report.Photo photoName 
                let! submissionResult = reportSubmissionAsync csa report photoName
                return SubmissionResult (submissionResult.ToString())
            | None -> 
                return Error "Choose data to make report" 
        } |> Cmd.ofAsyncMsg

    let update msg model =
        let oldReport = match model.Report with 
                        | Some r -> r
                        | None -> defaultReport
        match msg with
        | DefaultReport -> { model with Report = Some defaultReport }, Cmd.none
        | LocationFound loc -> 
            let report = { oldReport with Location = Some loc }
            let newRegion = new MapSpan(new Position(loc.Latitude, loc.Longitude), 0.025, 0.025)
            { model with MapRegion = newRegion; Report = Some report }, Cmd.none
        | TakePhoto -> model, PhotoCaptureAsync
        | PhotoTaken photo -> 
            { model with Report = Some { oldReport with Photo = photo }}, Cmd.none
        | MakeReport -> 
            model, SubmitReportAsync(model.Report)
        | SubmissionResult s -> 
            { model with Error = Some s; Report = Some defaultReport }, Cmd.none
        | WeightPicked w -> { model with Report = Some { oldReport with Weight = Some w } }, Cmd.none
        | MaterialPicked m -> { model with Report = Some { oldReport with Material = Some m} }, Cmd.none
        | SizePicked s -> { model with Report = Some { oldReport with Size = Some s} }, Cmd.none
        | Reset -> init()
        | Error s -> { model with Error = Some s }, Cmd.none

    let view model dispatch =

        let buildProgressPanel = 
            let panel = View.FlexLayout(padding = 10.0, direction = FlexDirection.Row, children = [ 
                View.CircleImage("albie.jpg")
                View.CircleImage("p1.png")
            ])
            panel

        let carouselItems = 
            [
               View.CircleImage(fname = "p1.png")
               View.CircleImage(fname = "p2.png"); 
               View.CircleImage "p3.png"
            ]

        let carouselTemplate = new DataTemplate(typedefof<Label>)
            (*
    new DataTemplate(fun () -> 
                                                    let l = new Label()
                                                    l.SetBinding(Label.TextProperty, "Text")
                                                    l :> obj *)
//                                                    let vc = new ViewCell()
//                                                    vc.View <- l
//                                                    vc :> obj
//                                                )

        let errorMsg, errorVisible = match model.Error with 
                                     | Some e -> e, true
                                     | None -> "", false
        View.ContentPage(
          content = View.FlexLayout(padding = 20.0, direction = FlexDirection.Column, verticalOptions = LayoutOptions.Center,
            children = [
                View.Map(heightRequest = 320., widthRequest = 320., horizontalOptions = LayoutOptions.Center, requestedRegion = model.MapRegion )
                View.Picker(title = "Weight", itemsSource = weightPickerSource, selectedIndexChanged = (fun (ix, _) -> enum<DebrisWeightT>(ix) |> WeightPicked |> dispatch), horizontalOptions = LayoutOptions.Center)
                View.Picker(title = "Size", itemsSource = sizePickerSource, selectedIndexChanged = (fun (ix, _) -> enum<DebrisSizeT>(ix) |> SizePicked |> dispatch ), horizontalOptions = LayoutOptions.Center)
                View.Picker(title = "Material", itemsSource = materialPickerSource, selectedIndexChanged = (fun (ix, _) -> enum<DebrisMaterialT>(ix) |> MaterialPicked |> dispatch), horizontalOptions = LayoutOptions.Center)
                View.Button(text = "Take photo", command = (fun () -> dispatch TakePhoto), horizontalOptions = LayoutOptions.Center)
                View.Button(text = "Report it!", command = (fun () -> dispatch MakeReport), horizontalOptions = LayoutOptions.Center)
                View.Button(text = "Reset", horizontalOptions = LayoutOptions.Center, command = (fun () -> dispatch Reset))
                View.Label(text = errorMsg, isVisible = errorVisible, horizontalOptions = LayoutOptions.Center)
                buildProgressPanel
                View.CarouselView(items = carouselItems, horizontalOptions = LayoutOptions.Center, verticalOptions = LayoutOptions.Center, heightRequest = float 150)
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


