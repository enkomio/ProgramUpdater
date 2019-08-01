namespace ES.Update

open System
open System.Net

type Updater(serverUri: Uri, currentVersion: Version) =
    let contactServer(path: String) =
        use webClient = new WebClient()
        webClient.DownloadString(new Uri(serverUri, path))

    member this.CheckForUpdates() =
        let latestVersion = contactServer("/latest") |> Version.Parse
        latestVersion > currentVersion


    interface IUpdater with
        member this.CheckForUpdates() =
            this.CheckForUpdates()