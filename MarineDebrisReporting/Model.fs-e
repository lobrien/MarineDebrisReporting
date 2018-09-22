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
        | Plastic = 0
        | Glass = 1
        | Wood = 2
        | Metal = 3
        | Fiberglas = 4
        | FishingGear = 5
        | Amalgam = 6
        | Other = 7

    type Report =
        {
            Timestamp : DateTime
            Location : Option<Location>
            Size : Option<DebrisSizeT>
            Material : Option<DebrisMaterialT>
            Weight : Option<DebrisWeightT>
            Photo : Option<IO.Stream>
            Notes : Option<String> 
        }

    let toJson report = 
        let jsonify fld s = sprintf "\"%s\" : \"%A\"" fld s 
        let ts =jsonify "Timestamp" (report.Timestamp.ToString("o"))
        let loc = jsonify "Location" report.Location
        let size = jsonify "Size" report.Size
        let mat = jsonify "Material" report.Material
        let wt = jsonify "Weight" report.Weight
        // TODO: Have to escape Notes
        let combined = [ ts; loc; size; mat; wt ] |> fun s -> String.Join(",\n", s)
        let outerJsonWrapper = sprintf "{\n %s \n}" combined |> fun s -> s.Replace("\"\"", "\"")
        outerJsonWrapper


    type ReportStorage(report : Report) = 
        inherit TableEntity( "MainPartition", report.Timestamp.ToString("o"))

        member val public Report = report |> toJson with get, set

    type Model = 
        {
            MapRegion : MapSpan
            Report : Option<Report>
            Error : Option<string>
        }
        

