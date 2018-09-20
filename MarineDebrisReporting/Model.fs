namespace MarineDebrisReporting

open System
open Xamarin.Essentials
open Fabulous.DynamicViews.MapsExtension
open Xamarin.Forms.Maps

module Model =

    type FileSystemUrl = string

    type IPictureService  =
        abstract member PictureFn : (unit -> System.Threading.Tasks.Task<FileSystemUrl>)

    type DebrisWeightT = 
        | Light
        | Totable
        | Liftable
        | CouplePeople
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
            Photo : Option<IO.Stream>
            Notes : Option<String> 
        }

    type Model = 
        {
            MapRegion : MapSpan
            Report : Option<Report>
            Error : Option<string>
        }
        

