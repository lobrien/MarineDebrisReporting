namespace MarineDebrisReporting.iOS

open System
open Xamarin.Forms
open MarineDebrisReporting.Model

type IosPictureService() =
    interface IPictureService with
        member this.PictureFn = fun () -> raise <| new NotImplementedException()

[<assembly: Dependency(typeof<IosPictureService>)>] do ()
