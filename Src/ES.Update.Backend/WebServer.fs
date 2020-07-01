namespace ES.Update.Backend

open System
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
    let _logger = 
        let tmp = new WebServerLogger()
        logProvider.AddLogSourceToLoggers(tmp)
        tmp

    let getFileFullPath(fileName: String) (ctx: HttpContext) = async {
        match _updateService.GetFilePath(fileName) with
        | Some filePath -> 
            let newState = ctx.userState.Add("file", filePath).Add("name", fileName)
            return Some {ctx with userState = newState}
        | None ->
            _logger.FileNotFound(fileName)
            return None
    }
        
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
        OK this.IndexPage ctx

    let latest(ctx: HttpContext) =
        match ctx.request.queryParam "project"  with
        | Choice1Of2 projectName ->
            match _updateService.GetLatestVersion(projectName) with
            | Some version -> OK version ctx
            | None -> OK Entities.DefaultVersion ctx
        | _ -> 
            OK Entities.DefaultVersion ctx
        
    let updates(ctx: HttpContext) =
        let inputVersion = ref(new Version())
        match tryGetPostParameters(["version"; "project"], ctx) with
        | Some values 
            when 
                Version.TryParse(values.["version"], inputVersion) 
                && _updateService.IsValidProject(values.["project"]) 
            ->
            
            let projectName = values.["project"]
            let catalog = _updateService.GetCatalog(!inputVersion, projectName)
            OK catalog ctx
        | _ -> 
            BAD_REQUEST "Missing parameters" ctx

    let downloadFile(ctx: HttpContext) =
        let file = ctx.userState.["file"] :?> String
        let name = ctx.userState.["name"] :?> String
        addHeader "Content-Type" "application/octet-stream"
        >=> addHeader "Content-Disposition" ("inline; filename=\"" + name + "\"")
        >=> sendFile file true
        <| ctx

    let authorize (webPart: WebPart) (ctx: HttpContext) =
        if this.Authenticate(ctx)
        then webPart ctx
        else FORBIDDEN "Forbidden" ctx

    let pathNotFound(p: String)(ctx: HttpContext) =
        NOT_FOUND "Path not valid" ctx

    let buildCfg(uri: Uri) = 
        { defaultConfig with
            bindings = [HttpBinding.create HTTP (IPAddress.Parse uri.Host) (uint16 uri.Port)]
            listenTimeout = TimeSpan.FromMilliseconds (float 10000)
            cancellationToken = _shutdownToken.Token
        }

    do
        _logger.SettingInfo("Workspace Directory", workspaceDirectory)
        _logger.SettingInfo("Binding Uri", binding.ToString())

    new (binding: Uri, workspaceDirectory: String, privateKey: Byte array) = new WebServer(binding, workspaceDirectory, privateKey, LogProvider.GetDefault())

    member val IndexPage = "-=[ Enkomio Updater Server ]=-" with get, set

    /// This parameter can specify a uri path prefix to use when invoking endpoints
    member val PathPrefix = String.Empty with get, set

    /// The path where the installer program is stored. If this path exists an Installer will be pushed in the update package
    member val InstallerPath = String.Empty with get, set

    abstract GetRoutes: unit -> WebPart list
    default this.GetRoutes() = [
        GET >=> preFilter >=> choose [ 
            path (this.PathPrefix + "/") >=> index          
            path (this.PathPrefix + "/latest") >=> latest     
            pathScan "/%s" pathNotFound
        ] >=> postFilter

        POST >=> preFilter >=> choose [ 
            // get the catalog for the specified version
            path (this.PathPrefix + "/updates") >=> authorize updates

            // get the specified file
            pathScan (PrintfFormat<_, _, _, _, String>(this.PathPrefix + "/file/%s")) getFileFullPath >=> authorize downloadFile
            pathScan "/%s" pathNotFound
        ] >=> postFilter
    ]

    abstract Authenticate: HttpContext -> Boolean
    default this.Authenticate(ctx: HttpContext) =
        true
        
    member this.Start() =
        logProvider.AddLogSourceToLoggers(_logger)
        
        _updateService.GetAvailableVersions()
        |> Array.iter(fun (prj, ver) -> _logger.VersionInfo(prj, ver))

        // start web server
        let cfg = buildCfg(binding)
        let routes = this.GetRoutes() |> choose
        startWebServer cfg routes

    member this.Stop() =
        _shutdownToken.Cancel()


