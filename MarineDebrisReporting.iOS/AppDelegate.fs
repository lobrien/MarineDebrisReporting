// Copyright 2018 Fabulous contributors. See LICENSE.md for license.
namespace MarineDebrisReporting.iOS

open System
open UIKit
open Foundation
open Xamarin.Forms
open Xamarin.Forms.Platform.iOS

[<Register ("AppDelegate")>]
type AppDelegate () =
    inherit FormsApplicationDelegate ()

    let iosPictureFn  = fun () ->
            let imagePicker = new UIImagePickerController()
            imagePicker.SourceType <- UIImagePickerControllerSourceType.PhotoLibrary
            imagePicker.MediaTypes <- UIImagePickerController.AvailableMediaTypes(UIImagePickerControllerSourceType.PhotoLibrary)

            imagePicker.FinishedPickingMedia.Add(fun e -> ignore() )

            imagePicker.Canceled.Add( fun e -> ignore())

            let window = UIApplication.SharedApplication.KeyWindow
            let vc = window.RootViewController
            vc.PresentModalViewController(imagePicker, true)

    override this.FinishedLaunching (app, options) =
        Forms.Init()
        Xamarin.FormsMaps.Init()
        let appcore = new MarineDebrisReporting.App()
        this.LoadApplication (appcore)
        base.FinishedLaunching(app, options)

module Main =
    [<EntryPoint>]
    let main args =
        UIApplication.Main(args, null, "AppDelegate")
        0

