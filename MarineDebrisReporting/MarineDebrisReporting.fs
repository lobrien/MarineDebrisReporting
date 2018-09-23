namespace MarineDebrisReporting

open Fabulous.Core
open Fabulous.DynamicViews
open Xamarin.Forms
open Xamarin.Forms.Maps
open Xamarin.Essentials
open System
open Plugin.Media
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Table
open System.IO

open Model 

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

    let connectionString = 
        let dataDir = Xamarin.Essentials.FileSystem.AppDataDirectory
        let connFile = Path.Combine(dataDir, "conn.txt")
        if File.Exists(connFile) = false then 
            File.WriteAllText (connFile, "***CONNECTION STRING***")

        let connString = File.ReadAllText(connFile)
        connString

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
    let photoSubmissionAsync (cloudStorageAccount : CloudStorageAccount) imageType (maybePhoto : IO.Stream option) imageName = 
        async {
            match maybePhoto with 
            | Some byteStream -> 
                let containerUrl = "https://hackthesea.blob.core.windows.net/marinedebrispix"
                let containerName = "marinedebrispix"
                let ctb = cloudStorageAccount.CreateCloudBlobClient()
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
                let csa = CloudStorageAccount.Parse connectionString
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

        let header = 
            View.Label(text = "Opala in Paradise", fontSize = 24, horizontalTextAlignment = TextAlignment.Center)

        let locationPage = 
            let locMsg = match model.Report |> Option.bind (fun r -> r.Location) with
                         | Some loc -> sprintf "Location: %f.3, %f.3" loc.Latitude loc.Longitude
                         | None -> "Location Unknown"

            View.FlexLayout(direction = FlexDirection.Column, alignItems = FlexAlignItems.Center, justifyContent = FlexJustify.SpaceEvenly, 
                children = [
                        View.Map(requestedRegion = model.MapRegion, minimumWidthRequest = 200.0, widthRequest = 400.0) |> flexGrow 1.0
                        View.Label(text = locMsg, verticalTextAlignment = TextAlignment.Center) |> flexGrow 1.0
                ])

        let namedCircleImage txt fname msg = 
            View.Frame(widthRequest = 100.0, heightRequest = 150.0, 
                content = View.FlexLayout(direction = FlexDirection.Column, alignItems = FlexAlignItems.Center, justifyContent = FlexJustify.SpaceEvenly, 
                    children = [
                        View.CircleImage(fname = fname, widthRequest = 75.0, heightRequest = 75.0)
                        View.Label(txt)
                    ]))
            |> gestureRecognizers [ View.TapGestureRecognizer(command=(fun () -> dispatch msg)) ]

        let debrisPage = 
            View.FlexLayout(direction = FlexDirection.Column, alignItems = FlexAlignItems.Center, justifyContent = FlexJustify.SpaceEvenly, 
                children = [
                    View.Label("Debris Type")
                    View.FlexLayout(wrap = FlexWrap.Wrap, justifyContent = FlexJustify.SpaceAround, 
                        children = [
                            namedCircleImage "Net" "debrist_net.jpg" <| MaterialPicked DebrisMaterialT.Net
                            namedCircleImage "Rope" "debrist_rope.jpg" <| MaterialPicked DebrisMaterialT.Rope
                            namedCircleImage "Mono" "debrist_mono.jpg" <| MaterialPicked DebrisMaterialT.Mono
                            namedCircleImage "Lumber/Bldg Material" "debrist_lumber.jpg" <| MaterialPicked DebrisMaterialT.BuildingMaterial
                            namedCircleImage "Cloth" "debrist_cloth.jpg" <| MaterialPicked DebrisMaterialT.Cloth
                            namedCircleImage "Plastic Sheeting" "debrist_sheeting.jpg" <| MaterialPicked DebrisMaterialT.Sheeting
                            namedCircleImage "Floats" "debrist_floats.jpg" <| MaterialPicked DebrisMaterialT.Floats 
                            namedCircleImage "Amalgam" "debrist_amalg.jpg" <| MaterialPicked DebrisMaterialT.Amalgam
                            namedCircleImage "Other/Unknown" "debrist_amalg.jpg" <| MaterialPicked DebrisMaterialT.Other

                        ]) |> flexGrow 1.0
                ])

        let biotaPage = 
            View.FlexLayout(direction = FlexDirection.Column, alignItems = FlexAlignItems.Center, justifyContent = FlexJustify.SpaceEvenly, 
                children = [
                    View.Label("Hitchhikers")
                    View.FlexLayout(wrap = FlexWrap.Wrap, justifyContent = FlexJustify.SpaceAround, 
                        children = [
                            namedCircleImage "Fish" "biotat_fish.jpg" <| BiotaPicked BiotaT.Fish
                            namedCircleImage "Crustaceans" "biotat_crustacean.jpg" <| BiotaPicked BiotaT.Crustaceans
                            namedCircleImage "Encrusting" "biotat_encrusting.jpg" <| BiotaPicked BiotaT.Encrusting
                            namedCircleImage "None" "biotat_none.jpg" <| BiotaPicked BiotaT.None
                            namedCircleImage "Other" "biotat_other.jpg" <| BiotaPicked BiotaT.Other

                        ]) |> flexGrow 1.0
                ])

        let photoPage = 
            View.FlexLayout(direction = FlexDirection.Column, alignItems = FlexAlignItems.Stretch, justifyContent = FlexJustify.SpaceEvenly,
                children = [
                    View.BoxView(Color.Gray) |> flexGrow 1.0
                ]) 

        let notesPage = 
            View.FlexLayout(direction = FlexDirection.Column, alignItems = FlexAlignItems.Stretch, justifyContent = FlexJustify.SpaceEvenly,
                children = [
                    View.BoxView(Color.Blue) |> flexGrow 1.0
                ]) 

        let gridDebrisPage = 
            let mkButton text command row column =
                 View.Button(text = text, command=(fun () -> dispatch command))
                     .GridRow(row)
                     .GridColumn(column)
                     .FontSize(36.0)
                     //.ButtonCornerRadius(0)

            let mkNumberButton number row column =
             (mkButton (string number) (MaterialPicked DebrisMaterialT.Amalgam) row column)
                 .BackgroundColor(Color.White)
                 .TextColor(Color.Black)

            let orange = Color.FromRgb(0xff, 0xa5, 0)
            let gray = Color.FromRgb(0x80, 0x80, 0x80)

            let mkOperatorButton text operator row column =
                (mkButton text (MaterialPicked DebrisMaterialT.Amalgam) row column)
                    .BackgroundColor(orange)
                    .TextColor(Color.Black)

            View.FlexLayout(direction = FlexDirection.Column, alignItems = FlexAlignItems.Stretch, justifyContent = FlexJustify.SpaceEvenly,
                children = [
                   View.Grid(rowdefs=[ "*"; "*"; "*"; "*" ], coldefs=[ "*"; "*"; "*" ],
                     children=[
                         View.Label(text = "Type", fontSize = 48.0, fontAttributes = FontAttributes.Bold, backgroundColor = Color.Black, textColor = Color.White, horizontalTextAlignment = TextAlignment.Center, verticalTextAlignment = TextAlignment.Center).GridColumnSpan(3)
                         mkNumberButton 7 1 0; mkNumberButton 8 1 1; mkNumberButton 9 1 2
                         mkNumberButton 4 2 0; mkNumberButton 5 2 1; mkNumberButton 6 2 2
                         mkNumberButton 1 3 0; mkNumberButton 2 3 1; mkNumberButton 3 3 2
                     ], rowSpacing = 1.0, columnSpacing = 1.0, backgroundColor = gray
                 )]) |> flexGrow 1.0

        // Bug: with my impl of `CarouselElement.OnBindingChanged`, this `T List` must be same concrete `T` (e.g., `FlexLayout` only not any `ViewElement`)
        let inputPages = [
            locationPage 
            debrisPage
            gridDebrisPage
            biotaPage
            photoPage
            notesPage
        ]

        let pages = [
                    View.CarouselView(items = inputPages) |> flexGrow 1.0
                    //View.BoxView(color = Color.White) |> flexBasis (new FlexBasis(50.0f,false)) |> flexOrder -1
                    //View.BoxView(color = Color.White) |> flexBasis (new FlexBasis(50.0f, false))
                    ]

        let content = 
            View.FlexLayout(children = pages) |> flexGrow 1.0

        let footer = 
            View.FlexLayout(direction = FlexDirection.Column, alignItems = FlexAlignItems.Center, justifyContent = FlexJustify.SpaceEvenly, 
                children = [
                    View.CircleImage(fname = "albie.jpg", widthRequest = 75.0, heightRequest = 75.0)
                    View.Button(text = "Report it!", fontSize = 24, isEnabled = model.Report.IsSome, command = fun () -> dispatch SubmitReport) 

                ])
                |> flexBasis (new FlexBasis(150.0f, false))
            

        let errorMsg, errorVisible = match model.Error with 
                                     | Some e -> e, true
                                     | None -> "", false

        View.ContentPage(
             content = View.FlexLayout(direction = FlexDirection.Column, alignItems = FlexAlignItems.Center, justifyContent = FlexJustify.SpaceEvenly, 
                children = [
                    header
                    content
                    footer
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


