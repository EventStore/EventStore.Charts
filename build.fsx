// installs the FAKE dependencies
#r "paket:
nuget Fake.Core.Target //"
// loads the intellisense script for IDE support
#load "./.fake/build.fsx/intellisense.fsx"

open Fake.Core

Target.create "Default" (fun _ ->
    Trace.trace "EventStore.Charts")

Target.runOrDefault "Default"
