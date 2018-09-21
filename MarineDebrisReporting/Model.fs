﻿namespace MarineDebrisReporting

open System
open Xamarin.Essentials
open Fabulous.DynamicViews.MapsExtension
open Xamarin.Forms.Maps

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

    type Model = 
        {
            MapRegion : MapSpan
            Report : Option<Report>
            Error : Option<string>
        }
        

