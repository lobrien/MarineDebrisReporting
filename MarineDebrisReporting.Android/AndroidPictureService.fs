namespace MarineDebrisReporting.Android

open System
open Xamarin.Forms
open MarineDebrisReporting.Model

type AndroidPictureService() =
    interface IPictureService with
        member this.PictureFn = fun () -> raise <| new NotImplementedException()


[<assembly: Dependency(typeof<AndroidPictureService>)>] do ()


