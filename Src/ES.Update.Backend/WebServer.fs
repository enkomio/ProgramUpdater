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
open ES.Fslog

type WebServer(binding: Uri, workspaceDirectory: String, privateKey: Byte array, logProvider: ILogProvider) as this =
    let _shutdownToken = new CancellationTokenSource()
    let _updateService = new UpdateService(workspaceDirectory, privateKey)
    let _logger = new WebServerLogger()
        
    let preFilter (oldCtx: HttpContext) = async {   
        let! ctx = addHeader "X-Xss-Protection" "1; mode=block" oldCtx
        let! ctx = addHeader "Content-Security-Policy" "script-src 'self' 'unsafe-inline' 'unsafe-eval'" ctx.Value
        let! ctx = addHeader "X-Frame-Options" "SAMEORIGIN" ctx.Value            
        let! ctx = addHeader "X-Content-Type-Options" "nosniff" ctx.Value
        return Some ctx.Value
    }

    let postFilter (ctx: HttpContext) = async {                
        _logger.LogRequest(ctx)
        return (Some ctx)
    }

    let index(ctx: HttpContext) =
        OK "-=[ Enkomio Updater Server ]=-" ctx

    let latest(ctx: HttpContext) =
        match ctx.request.queryParam "project"  with
        | Choice1Of2 projectName ->
            match _updateService.GetLatestVersion(projectName) with
            | Some version -> OK version ctx
            | None -> OK "0" ctx
        | _ -> 
            OK "0" ctx
        
    let updates(ctx: HttpContext) =
        let inputVersion = ref(new Version())
        match tryGetPostParameters(["version"; "project"], ctx) with
        | Some values 
            when 
                Version.TryParse(values.["version"], inputVersion) 
                && _updateService.IsValidProject(values.["project"]) 
            ->
            
            let signedZip = _updateService.GetUpdates(!inputVersion, values.["project"])

            // send the update zip file
            addHeader "Content-Type" "application/octet-stream"
            >=> addHeader "Content-Disposition" ("inline; filename=\"update.zip\"")
            >=> sendFile signedZip false
            <| ctx
        | _ -> 
            BAD_REQUEST String.Empty ctx

    let authorize (webPart: WebPart) (ctx: HttpContext) =
        if this.Authenticate(ctx)
        then webPart ctx
        else FORBIDDEN "Forbidden" ctx

    let buildCfg(uri: Uri) = 
        { defaultConfig with
            bindings = [HttpBinding.create HTTP (IPAddress.Parse uri.Host) (uint16 uri.Port)]
            listenTimeout = TimeSpan.FromMilliseconds (float 10000)
            cancellationToken = _shutdownToken.Token
        }

    new (binding: Uri, workspaceDirectory: String, privateKey: Byte array) = new WebServer(binding, workspaceDirectory, privateKey, new LogProvider())

    abstract GetRoutes: String -> WebPart list
    default this.GetRoutes(prefix: String) = [
        GET >=> preFilter >=> choose [ 
            path (prefix + "/") >=> index          
            path (prefix + "/latest") >=> authorize latest
        ] >=> postFilter

        POST >=> preFilter >=> choose [ 
            path (prefix + "/updates") >=> authorize updates
        ] >=> postFilter
    ]

    abstract Authenticate: HttpContext -> Boolean
    default this.Authenticate(ctx: HttpContext) =
        true
        
    member this.Start() =
        logProvider.AddLogSourceToLoggers(_logger)

        // start web server
        let cfg = buildCfg(binding)
        let routes = this.GetRoutes(String.Empty) |> choose
        startWebServer cfg routes

    member this.Stop() =
        _shutdownToken.Cancel()


