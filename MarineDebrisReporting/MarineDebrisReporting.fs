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
        | BiotaPicked of BiotaT
        | TakePhoto
        | PhotoTaken of Option<IO.Stream>
        | Error of string
        | SubmitReport 
        | SubmissionResult of string
        | Reset


    let defaultReport : Report = {
        Timestamp = DateTime.Now;
        Location = None;
        Size = None;
        Material = None;
        Weight = None;
        Photo = None;
        Biota = [];
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
        | SubmitReport -> model, SubmitReportAsync(model.Report)
        | SubmissionResult s -> 
            { model with Error = Some s; Report = Some defaultReport }, Cmd.none
        | WeightPicked w -> { model with Report = Some { oldReport with Weight = Some w } }, Cmd.none
        | MaterialPicked m -> { model with Report = Some { oldReport with Material = Some m} }, Cmd.none
        | SizePicked s -> { model with Report = Some { oldReport with Size = Some s} }, Cmd.none
        | BiotaPicked b -> { model with Report = Some { oldReport with Biota = b :: oldReport.Biota}}, Cmd.none
        | Reset -> init()
        | Error s -> { model with Error = Some s }, Cmd.none

    let view model dispatch =

        let buildHeader = 
            View.Label(text = "Opala in Paradise", fontSize = 24, horizontalTextAlignment = TextAlignment.Center)

        let buildGriddedElement children = 
            View.FlexLayout(wrap = FlexWrap.Wrap, justifyContent = FlexJustify.SpaceAround, children = children)

        let buildPage1 = 
            let locMsg = match model.Report |> Option.bind (fun r -> r.Location) with
                         | Some loc -> sprintf "Location: %f.3, %f.3" loc.Latitude loc.Longitude
                         | None -> "Location Unknown"

            View.FlexLayout(direction = FlexDirection.Column, alignItems = FlexAlignItems.Center, justifyContent = FlexJustify.SpaceEvenly, children = [
                View.Map(requestedRegion = model.MapRegion, minimumWidthRequest = 200.0, widthRequest = 400.0) |> flexGrow 1.0
                View.Label(text = locMsg, verticalTextAlignment = TextAlignment.Center) |> flexGrow 1.0
            ])

        let buildNamedCircleImage txt fname msg = 
            View.Frame(widthRequest = 100.0, heightRequest = 150.0, content = View.FlexLayout(direction = FlexDirection.Column, alignItems = FlexAlignItems.Center, justifyContent = FlexJustify.SpaceEvenly, children = [
                View.CircleImage(fname = fname, widthRequest = 75.0, heightRequest = 75.0)
                View.Label(txt)
                ]))
            |> gestureRecognizers [ View.TapGestureRecognizer(command=(fun () -> dispatch msg)) ]

        let buildPage2 = 
            View.FlexLayout(direction = FlexDirection.Column, alignItems = FlexAlignItems.Center, justifyContent = FlexJustify.SpaceEvenly, children = [
                View.Label("Debris Type")
                View.FlexLayout(wrap = FlexWrap.Wrap, justifyContent = FlexJustify.SpaceAround, children = [
                    buildNamedCircleImage "Net" "debrist_net.jpg" <| MaterialPicked DebrisMaterialT.Net
                    buildNamedCircleImage "Rope" "debrist_rope.jpg" <| MaterialPicked DebrisMaterialT.Rope
                    buildNamedCircleImage "Mono" "debrist_mono.jpg" <| MaterialPicked DebrisMaterialT.Mono
                    buildNamedCircleImage "Lumber/Bldg Material" "debrist_lumber.jpg" <| MaterialPicked DebrisMaterialT.BuildingMaterial
                    buildNamedCircleImage "Cloth" "debrist_cloth.jpg" <| MaterialPicked DebrisMaterialT.Cloth
                    buildNamedCircleImage "Plastic Sheeting" "debrist_sheeting.jpg" <| MaterialPicked DebrisMaterialT.Sheeting
                    buildNamedCircleImage "Floats" "debrist_floats.jpg" <| MaterialPicked DebrisMaterialT.Floats 
                    buildNamedCircleImage "Amalgam" "debrist_amalg.jpg" <| MaterialPicked DebrisMaterialT.Amalgam
                    buildNamedCircleImage "Other/Unknown" "debrist_amalg.jpg" <| MaterialPicked DebrisMaterialT.Other
                 
                ]) |> flexGrow 1.0
            ])

        let buildPage3 = 
            View.FlexLayout(direction = FlexDirection.Column, alignItems = FlexAlignItems.Center, justifyContent = FlexJustify.SpaceEvenly, children = [
                View.Label("Hitchhikers")
                View.FlexLayout(wrap = FlexWrap.Wrap, justifyContent = FlexJustify.SpaceAround, children = [
                    buildNamedCircleImage "Fish" "biotat_fish.jpg" <| BiotaPicked BiotaT.Fish
                    buildNamedCircleImage "Crustaceans" "biotat_crustacean.jpg" <| BiotaPicked BiotaT.Crustaceans
                    buildNamedCircleImage "Encrusting" "biotat_encrusting.jpg" <| BiotaPicked BiotaT.Encrusting
                    buildNamedCircleImage "None" "biotat_none.jpg" <| BiotaPicked BiotaT.None
                    buildNamedCircleImage "Other" "biotat_other.jpg" <| BiotaPicked BiotaT.Other

                ]) |> flexGrow 1.0
            ])
        

        let buildInputPages = [
            buildPage1 
            buildPage2
            buildPage3
        ]

        let buildPages = [
                    View.CarouselView(items = buildInputPages) |> flexGrow 1.0
                    //View.BoxView(color = Color.White) |> flexBasis (new FlexBasis(50.0f,false)) |> flexOrder -1
                    //View.BoxView(color = Color.White) |> flexBasis (new FlexBasis(50.0f, false))
                    ]

        let buildContent = 
            View.FlexLayout(children = buildPages) |> flexGrow 1.0

        let buildFooter = 
            View.Button(text = "Report it!", fontSize = 24, isEnabled = model.Report.IsSome, command = fun () -> dispatch SubmitReport) 
            |> direction FlexDirection.Column 
            |> flexBasis (new FlexBasis(100.0f, false))

        let errorMsg, errorVisible = match model.Error with 
                                     | Some e -> e, true
                                     | None -> "", false

        View.ContentPage(content = View.FlexLayout(direction = FlexDirection.Column, alignItems = FlexAlignItems.Center, justifyContent = FlexJustify.SpaceEvenly, 
                children = 
                        [
                            buildHeader
                            buildContent
                            buildFooter
                        ])

    )

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


