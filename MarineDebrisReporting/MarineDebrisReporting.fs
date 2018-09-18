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

module App = 

    type Msg = 
        | DefaultReport 
        | LocationFound of Location
        | Reset

    let newReport = { 
        Timestamp = DateTime.Now; 
        Location = None; 
        Size = None; 
        Material = None; 
        Weight = None; 
        Notes = None; 
        Picture = None; 
        MapRegion = new MapSpan(new Position(21.3, -157.9), 0.3, 0.3)  }

    let init () = newReport, Cmd.none

    let locationCmd = 
        async { 
            let! loc = Geolocation.GetLocationAsync() |> Async.AwaitTask
            return LocationFound loc
        } |> Cmd.ofAsyncMsg

    let update msg model =
        match msg with
        | DefaultReport -> newReport, locationCmd
        | LocationFound loc -> { model with Location = Some loc}, Cmd.none
        | Reset -> raise <| new NotImplementedException()


    let view (model: Report) dispatch =
        View.ContentPage(
          content = View.StackLayout(padding = 20.0, verticalOptions = LayoutOptions.Center,
            children = [
                View.Map(heightRequest = 320., widthRequest = 320., horizontalOptions = LayoutOptions.Center, backgroundColor = Color.AliceBlue, requestedRegion = model.MapRegion )
                View.Label(text = sprintf "%d" 123, horizontalOptions = LayoutOptions.Center, fontSize = "Large")
                View.Button(text = "Increment", command = (fun () -> ignore()), horizontalOptions = LayoutOptions.Center)
                View.Button(text = "Reset", horizontalOptions = LayoutOptions.Center, command = (fun () -> dispatch Reset))
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


