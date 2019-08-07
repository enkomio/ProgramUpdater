﻿namespace ES.Update.Backend

open System
open System.Text
open ES.Fslog
open Suave

type WebServerLogger() =
    inherit LogSource("WebServer")

    [<Log(1, Message = "{0} - {1} {2}{3} =>", Level = LogLevel.Informational)>]
    member this.LogRequestStart(ctx: HttpContext, ?logData: Boolean) =         
        let ip = ctx.clientIpTrustProxy.ToString()
        let httpMethod = ctx.request.``method``
        let path = ctx.request.url.PathAndQuery
                
        let data = 
            let tmpData = " - Data: " + Encoding.Default.GetString(ctx.request.rawForm)
            match (logData, httpMethod) with
            | (None, HttpMethod.POST) -> tmpData
            | (Some printData, HttpMethod.POST) when printData -> tmpData              
            | _  -> String.Empty
        base.WriteLog(1, ip, httpMethod, path, data)

    [<Log(2, Message = "{0}", Level = LogLevel.Informational)>]
    member this.LogRequestEnd(ctx: HttpContext) =
        base.WriteLog(2, ctx.response.status.code)