namespace ES.Update.Backend

open System
open System.IO
open System.Reflection
open System.Net
open System.Threading
open Suave
open Suave.Successful
open Suave.Writers
open Suave.Operators
open Suave.RequestErrors
open Suave.Filters
open Suave.Files
open Entities

type Server(binding: String, workspaceDirectory: String) as this =
    let _shutdownToken = new CancellationTokenSource()
    let mutable _updateManager: UpdateManager option = None
        
    let getUpdateFileName(inputVersion: Version) =
        let correctInputVersion =
            match _updateManager.Value.GetApplication(inputVersion) with
            | Some application-> application.Version.ToString()
            | None -> "0"

        let latestVersion = _updateManager.Value.GetLatestVersion().Value.Version.ToString()
        String.Format("{0}-{1}.zip", correctInputVersion, latestVersion)
        
    let preFilter (oldCtx: HttpContext) = async {   
        let! ctx = addHeader "X-Xss-Protection" "1; mode=block" oldCtx
        let! ctx = addHeader "Content-Security-Policy" "script-src 'self' 'unsafe-inline' 'unsafe-eval'" ctx.Value
        let! ctx = addHeader "X-Frame-Options" "SAMEORIGIN" ctx.Value            
        let! ctx = addHeader "X-Content-Type-Options" "nosniff" ctx.Value
        return Some ctx.Value
    }

    let index(ctx: HttpContext) =
        OK "-=[ Enkomio Updater Server ]=-" ctx

    let latest(ctx: HttpContext) =
        match _updateManager.Value.GetLatestVersion() with
        | Some application -> OK (application.Version.ToString()) ctx
        | None -> OK "0" ctx

    let updates(ctx: HttpContext) =
        let inputVersion = ref(new Version())
        match ctx.request.queryParam "version" with
        | Choice1Of2 version when Version.TryParse(version, inputVersion) ->
            // compute zip filename
            let storageDirectory = Path.Combine(workspaceDirectory, "Binaries")
            let zipFile = Path.Combine(storageDirectory, getUpdateFileName(!inputVersion))

            // check if we already compute this update, if not create it
            if not(File.Exists(zipFile)) then
                // compute updates
                let updateFiles = _updateManager.Value.GetUpdates(!inputVersion)

                // create the zip file and store it in the appropriate directory            
                Directory.CreateDirectory(storageDirectory) |> ignore            
                createZipFile(zipFile, updateFiles)

            // send the update zip file
            addHeader "Content-Type" "application/octet-stream"
            >=> addHeader "Content-Disposition" ("inline; filename=\"" + Path.GetFileName(zipFile) + "\"")
            >=> sendFile zipFile false
            <| ctx
        | _ -> 
            BAD_REQUEST String.Empty ctx

    let authorize (webPart: WebPart) (ctx: HttpContext) =
        if this.Authenticate(ctx)
        then webPart ctx
        else FORBIDDEN "Forbidden" ctx

    let buildCfg(uriString: String) = 
        let uri = new UriBuilder(uriString)
        { defaultConfig with
            bindings = [HttpBinding.create HTTP (IPAddress.Parse uri.Host) (uint16 uri.Port)]
            listenTimeout = TimeSpan.FromMilliseconds (float 10000)
            cancellationToken = _shutdownToken.Token
        }

    let createUpdateManager() =
        Directory.CreateDirectory(workspaceDirectory) |> ignore
        new UpdateManager(workspaceDirectory)

    abstract GetRoutes: unit -> WebPart list
    default this.GetRoutes() = [
        GET >=> preFilter >=> choose [ 
            path "/" >=> index          
            path "/latest" >=> authorize latest
        ]

        POST >=> preFilter >=> choose [ 
            path "/updates" >=> authorize updates
        ]
    ] 

    abstract Authenticate: HttpContext -> Boolean
    default this.Authenticate(ctx: HttpContext) =
        true
        
    member this.Start() = 
        // setup the update manager
        _updateManager <- Some(createUpdateManager())        

        // start web server
        let cfg = buildCfg(binding)
        let routes = this.GetRoutes() |> choose
        startWebServer cfg routes

    member this.ShutDownServer() =
        _shutdownToken.Cancel()


