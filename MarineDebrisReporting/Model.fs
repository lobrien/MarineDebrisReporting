namespace MarineDebrisReporting

open System
open Xamarin.Essentials
open Fabulous.DynamicViews.MapsExtension
open Xamarin.Forms.Maps
open Microsoft.WindowsAzure.Storage.Table

module Model =

    // These need to be enums to match up with index in picker (could use `Dictionary<string,DebrisWeightT>` I suppose) 
    type DebrisWeightT = 
    | Light = 0
    | Totable = 1
    | Liftable = 2
    | CouplePeople = 3
    | MachineOnly = 4

    type DebrisSizeT = 
    | TrashElement = 0
    | TrashPile = 1
    | TrashField = 2
    | DebrisElement = 3
    | DebrisPile = 4
    | Line = 5
    | Other = 6

    type DebrisMaterialT =
    | Net = 0
    | Rope = 1
    | Mono = 2
    | BuildingMaterial = 3
    | Cloth = 4
    | Sheeting = 5
    | Floats = 6
    | Amalgam = 7
    | Other = 8

    type BiotaT = 
    | Fish = 0
    | Crustaceans = 1
    | Encrusting = 2
    | None = 3
    | Other = 4

    type Report =
        {
            Timestamp : DateTime
            Location : Option<Location>
            Size : Option<DebrisSizeT>
            Material : Option<DebrisMaterialT>
            Weight : Option<DebrisWeightT>
            Photo : Option<IO.Stream>
            Biota : List<BiotaT>
            Notes : Option<String> 
        }

    let toJson report = 
        let json = Newtonsoft.Json.JsonConvert.SerializeObject(report)
        json


    type ReportStorage(report : Report) = 
        inherit TableEntity( "MainPartition", report.Timestamp.ToString("o"))

        member val public Report = report |> toJson with get, set

    type Model = 
        {
            MapRegion : MapSpan
            Report : Option<Report>
            Error : Option<string>
        }
        

