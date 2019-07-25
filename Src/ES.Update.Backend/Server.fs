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
open ES.Fslog

type Server(binding: String, logProvider: ILogProvider) as this =
    let _shutdownToken = new CancellationTokenSource()
    let mutable _updateManager: UpdateManager option = None
        
    let postFilter (ctx: HttpContext) = async {
        return Some ctx
    }            

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
        OK (_updateManager.Value.GetLatestVersion()) ctx

    let updates(ctx: HttpContext) =
        let updates = _updateManager.Value.GetUpdates()
        // TODO serialize updates to a ZIP file
        OK "" ctx

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
        let workspaceDir = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "Workspace")
        Directory.CreateDirectory(workspaceDir) |> ignore
        new UpdateManager(workspaceDir)

    abstract GetRoutes: unit -> WebPart list
    default this.GetRoutes() = [
        GET >=> preFilter >=> choose [ 
            path "/" >=> index          
            path "/latest" >=> authorize latest
        ] >=> postFilter

        POST >=> preFilter >=> choose [ 
            path "/updates" >=> authorize updates
        ] >=> postFilter
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


