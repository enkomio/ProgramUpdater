namespace ES.Update

open System
open ES.Update.Entities

type IUpdater =
    interface
        /// This function returns true if there is a more updated version
        abstract CheckForUpdates: unit -> Boolean

    end

