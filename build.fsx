// installs the FAKE dependencies
#r "paket:
nuget Fake.Core.Target //"
// loads the intellisense script for IDE support
#load "./.fake/build.fsx/intellisense.fsx"

open Fake.Core

module Path =
    let toPosix path =
        path
        |> String.replace "\\" "/"

let buildDir = __SOURCE_DIRECTORY__ |> Path.toPosix
let lintImage = "quay.io/helmpack/chart-testing"
let lintImageTag = "latest"

let exec command args =
    let exitCode = Shell.Exec(command, args)
    if exitCode <> 0 then failwithf "%s %s failed" command args

Target.create "Default" (fun _ ->
    Trace.trace "EventStore.Charts")

Target.create "Lint" (fun _ ->
    Trace.trace "Linting chart..."
    let runArgs = sprintf "--rm -v \"%s:/workdir\"" buildDir
    let imageArg = sprintf "%s:%s" lintImage lintImageTag
    let lintCommand = "ct lint --charts /workdir/eventstore --validate-maintainers=false"
    let dockerArgs = sprintf "run %s %s %s" runArgs imageArg lintCommand
    exec "docker" dockerArgs)

open Fake.Core.TargetOperators

"Default"
 ==> "Lint"

Target.runOrDefault "Default"
