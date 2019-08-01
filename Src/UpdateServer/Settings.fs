namespace UpdateServer

open System
open System.Collections.Generic
open System.IO
open Newtonsoft.Json
open System.Reflection

type Settings() =
    member val WorkspaceDirectory = String.Empty with get, set
    member val BindingAddress = String.Empty with get, set
    member val PrivateKey = String.Empty with get, set

    static member Read() =
        let configFile = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "configuration.json")
        if File.Exists(configFile) then
            (File.ReadAllText >> JsonConvert.DeserializeObject<Settings>)(configFile)
        else
            new Settings()