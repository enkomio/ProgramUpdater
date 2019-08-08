﻿namespace Updater

open System
open System.Reflection
open System.IO

module internal Utility =
    let readCurrentVersion() =
        let versionFile = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "version.txt")
        let currentVersion = ref(new Version())
        if File.Exists(versionFile) then
            Version.TryParse(File.ReadAllText(versionFile), currentVersion) |> ignore
        !currentVersion

