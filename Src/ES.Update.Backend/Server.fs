namespace ES.Update.Backend

open System
open System.IO
open System.Collections.Generic
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

type Server(binding: String, workspaceDirectory: String, privatekey: String) as this =
    let _shutdownToken = new CancellationTokenSource()
    let _lock = new Object()
    let _updateManagers = new Dictionary<String, UpdateManager>()

    let getUpdateManager(projectName: String) =
        _updateManagers.[projectName]

    let createUpdateManagers() =
        Directory.GetDirectories(workspaceDirectory)
        |> Array.map(fun directory -> Path.GetFileName(directory))
        |> Array.iter(fun projectName -> 
            let projectDirectory = Path.Combine(workspaceDirectory, projectName)
            Directory.CreateDirectory(projectDirectory) |> ignore
            _updateManagers.[projectName] <- new UpdateManager(projectDirectory)
        )
        
    let getUpdateFileName(inputVersion: Version, updateManager: UpdateManager) =
        let correctInputVersion =
            match updateManager.GetApplication(inputVersion) with
            | Some application-> application.Version.ToString()
            | None -> "0"

        let latestVersion = updateManager.GetLatestVersion().Value.Version.ToString()
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
        match ctx.request.queryParam "project"  with
        | Choice1Of2 projectName when _updateManagers.ContainsKey(projectName) ->
            match getUpdateManager(projectName).GetLatestVersion() with
            | Some application -> OK (application.Version.ToString()) ctx
            | None -> OK "0" ctx
        | _ -> 
            OK "0" ctx
        
    let updates(ctx: HttpContext) =
        let inputVersion = ref(new Version())
        match tryGetPostParameters(["version"; "key"; "iv"; "project"], ctx) with
        | Some values 
            when 
                Version.TryParse(values.["version"], inputVersion) 
                && _updateManagers.ContainsKey(values.["project"]) ->        

            let updateManager = getUpdateManager(values.["project"])
            
            // compute zip filename
            let storageDirectory = Path.Combine(workspaceDirectory, "Binaries")
            let zipFile = Path.Combine(storageDirectory, getUpdateFileName(!inputVersion, updateManager))

            // check if we already compute this update, if not create it
            lock _lock (fun _ ->
                if not(File.Exists(zipFile)) then
                    // compute updates
                    let updateFiles = updateManager.GetUpdates(!inputVersion)
                    let integrityInfo = updateManager.ComputeIntegrityInfo(updateFiles |> List.map(fst))
                
                    // create the zip file and store it in the appropriate directory            
                    Directory.CreateDirectory(storageDirectory) |> ignore            
                    createZipFile(zipFile, updateFiles, integrityInfo)
            )            

            // add signature to zip file
            removeOldBinaryFiles(this.CacheCleanupSecondsTimeout)
            let signedZip = addSignature(zipFile, values.["key"], values.["iv"], privatekey)            

            // send the update zip file
            addHeader "Content-Type" "application/octet-stream"
            >=> addHeader "Content-Disposition" ("inline; filename=\"" + Path.GetFileName(zipFile) + "\"")
            >=> sendFile signedZip false
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

    /// This timeout is used to clean the temporary update files that are generated
    /// during the update process.
    member val CacheCleanupSecondsTimeout = 24 * 60 * 60 with get, set

    abstract GetRoutes: String -> WebPart list
    default this.GetRoutes(prefix: String) = [
        GET >=> preFilter >=> choose [ 
            path (prefix + "/") >=> index          
            path (prefix + "/latest") >=> authorize latest
        ]

        POST >=> preFilter >=> choose [ 
            path (prefix + "/updates") >=> authorize updates
        ]
    ] 

    abstract Authenticate: HttpContext -> Boolean
    default this.Authenticate(ctx: HttpContext) =
        true
        
    member this.Start() =
        // scan the workspace directory
        createUpdateManagers()

        // start web server
        let cfg = buildCfg(binding)
        let routes = this.GetRoutes(String.Empty) |> choose
        startWebServer cfg routes

    member this.Stop() =
        _shutdownToken.Cancel()


