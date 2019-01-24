// installs the FAKE dependencies
#r "paket:
nuget Fake.IO.FileSystem
nuget Fake.Core.Target //"
// loads the intellisense script for IDE support
#load "./.fake/e2e-docker-desktop.fsx/intellisense.fsx"

open Fake.Core
open Fake.IO

module Path =
    let toPosix path =
        path
        |> String.replace "\\" "/"

let sourceDir = __SOURCE_DIRECTORY__ |> Path.getDirectory |> Path.toPosix
let homeDir =
    let homeDrive = Environment.environVar "HOMEDRIVE"
    let homePath = Environment.environVar "HOMEPATH"
    sprintf "%s%s" homeDrive homePath
    |> Path.toPosix
let lintImage = "quay.io/helmpack/chart-testing"
let lintImageTag = "latest"

/// ref: https://fake.build/core-process.html
let execRaw commandPath args =
    CreateProcess.fromRawCommand commandPath args
    |> CreateProcess.ensureExitCode
    |> CreateProcess.redirectOutput
    |> Proc.run
    |> fun result -> result.Result.Output

let execRawLine commandPath args =
    CreateProcess.fromRawCommandLine commandPath args
    |> CreateProcess.ensureExitCode
    |> CreateProcess.redirectOutput
    |> Proc.run
    |> fun result -> result.Result.Output

let exec command args =
    let exitCode = Shell.Exec(command, args)
    if exitCode <> 0 then failwithf "%s %s failed" command args

let dockerExec = exec "docker exec"

let createTestContainer () =
    execRaw "docker.exe"
        [ "container"
          "run"
          "--interactive"
          "--tty"
          "--detach"
          "--volume"
          sprintf "%s:/workdir" sourceDir
          "--workdir" 
          "/workdir"
          sprintf "%s:%s" lintImage lintImageTag
          "cat" ]

let getApiServerContainerId () =
    let apiServerContainerId = 
        execRaw "docker.exe"
            [ "container"
              "list"
              "--filter" 
              "name=k8s_kube-apiserver"
              "--format" 
              "{{ .ID }}" ]
    if apiServerContainerId = "" then
        failwith "ERROR: API-Server container not found. Make sure 'Show system containers' is enabled in Docker Desktop 'Preferences'!"
    apiServerContainerId

let getApiServerArg apiServerContainerId arg =
    let arg = sprintf "container inspect %s | jq ." apiServerContainerId
    execRawLine "docker.exe" arg
    //   sprintf """ %s | jq -r ".[].Args[] | capture("%s=(?<arg>.*)") | .arg" """ apiServerContainerId arg ]

let configureKubectl home containerId apiServerId ipAddress port =
    sprintf "%s/.kube %s:/root/.kube" home containerId 
    |> exec "docker cp"

    sprintf """ "%s" kubectl config set-cluster docker-for-desktop-cluster "--server=https://%s:%s" """ 
        containerId 
        ipAddress 
        port
    |> dockerExec

    sprintf """ "%s" kubectl config set-cluster docker-for-desktop-cluster --insecure-skip-tls-verify=true """
        containerId
    |> dockerExec

    sprintf """ "%s" kubectl config use-context docker-for-desktop """
        containerId
    |> dockerExec

let runTillerless containerId =
    sprintf """ "%s" apk add bash """ 
        containerId
    |> dockerExec

    sprintf """ "%s" helm init --client-only """
        containerId
    |> dockerExec

    sprintf """ "%s" helm plugin install https://github.com/rimusz/helm-tiller """
        containerId
    |> dockerExec

    sprintf """ "%s" bash -c 'echo "Starting Tiller..."; helm tiller start-ci >/dev/null 2>&1 &' """
        containerId
    |> dockerExec

    sprintf """ "%s" bash -c 'echo "Waiting Tiller to launch on 44134..."; while ! nc -z localhost 44134; do sleep 1; done; echo "Tiller launched..."' """
        containerId
    |> dockerExec

let runTest containerId =
    sprintf """ -e HELM_HOST=127.0.0.1:44134 -e HELM_TILLER_SILENT=true "%s" ct install --config /workdir/test/ct.yaml """
        containerId
    |> dockerExec


Target.create "Default" (fun _ ->
    Trace.trace "EventStore.Charts")

Target.create "Test" (fun _ ->
    Trace.trace "Running e2e test..."
    let log label message = sprintf "%s: %s" label message |> Trace.trace
    let containerId = createTestContainer ()
    log "container id" containerId
    let apiServerId = getApiServerContainerId ()
    log "api server id" apiServerId
    let ipAddress = getApiServerArg containerId "--advertise-address"
    log "ip address" ipAddress
    let port = getApiServerArg containerId "--secure-port"
    log "port" port
    // configureKubectl homeDir containerId apiServerId ipAddress port
    // runTillerless containerId
    // runTest containerId
    Trace.trace "Success!")

open Fake.Core.TargetOperators

"Default"
 ==> "Test"

Target.runOrDefault "Default"
