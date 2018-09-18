namespace MarineDebrisReporting

open System
open Xamarin.Essentials
open Fabulous.DynamicViews.MapsExtension
open Xamarin.Forms.Maps

module Model =

    type FileSystemUrl = string

    type DebrisWeightT = 
        | Light
        | Totable
        | Liftable
        | Heavy
        | MachineOnly

    type DebrisSizeT = 
        | TrashElement
        | TrashPile 
        | DebrisElement
        | DebrisPile 
        | Line
        | Other 

    type MaterialT =
        | Plastic
        | Wood
        | Metal
        | Fiberglas 
        | FishingGear 
        | Amalgam
        | Other

    type Report =
        {
            Timestamp : DateTime
            Location : Option<Location>
            Size : Option<DebrisSizeT>
            Material : Option<MaterialT>
            Weight : Option<DebrisWeightT>
            Notes : Option<String> 
            Picture : Option<unit -> FileSystemUrl>
            MapRegion : MapSpan
        }
        

