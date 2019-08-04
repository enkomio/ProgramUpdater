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

type Server(binding: String, workspaceDirectory: String, privateKey: String) as this =
    let _shutdownToken = new CancellationTokenSource()
    let _updateService = new UpdateService(workspaceDirectory, privateKey)
        
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
        | Choice1Of2 projectName ->
            match _updateService.GetLatestVersion(projectName) with
            | Some version -> OK version ctx
            | None -> OK "0" ctx
        | _ -> 
            OK "0" ctx
        
    let updates(ctx: HttpContext) =
        let inputVersion = ref(new Version())
        match tryGetPostParameters(["version"; "key"; "iv"; "project"], ctx) with
        | Some values when Version.TryParse(values.["version"], inputVersion) ->
            let signedZip = _updateService.GetUpdates(!inputVersion, values.["project"], values.["key"], values.["iv"])

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

    let buildCfg(uriString: String) = 
        let uri = new UriBuilder(uriString)
        { defaultConfig with
            bindings = [HttpBinding.create HTTP (IPAddress.Parse uri.Host) (uint16 uri.Port)]
            listenTimeout = TimeSpan.FromMilliseconds (float 10000)
            cancellationToken = _shutdownToken.Token
        }

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
        // start web server
        let cfg = buildCfg(binding)
        let routes = this.GetRoutes(String.Empty) |> choose
        startWebServer cfg routes

    member this.Stop() =
        _shutdownToken.Cancel()


