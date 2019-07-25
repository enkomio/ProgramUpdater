namespace ES.Update

open System
open System.Net
open Entities

type Updater(serverUri: Uri, currentVersion: Version) =
    let contactServer(path: String) =
        use webClient = new WebClient()
        webClient.DownloadString(new Uri(serverUri, path))

    member this.CheckForUpdates() =
        let latestVersion = contactServer("/latest") |> Version.Parse
        latestVersion > currentVersion

    member this.GetUpdate() =
        {Files = Array.empty}

    interface IUpdater with
        member this.CheckForUpdates() =
            this.CheckForUpdates()

        member this.GetUpdate() =
            this.GetUpdate()