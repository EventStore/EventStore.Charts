// installs the FAKE dependencies
#r "paket:
nuget FSharp.Data
nuget Fake.IO.FileSystem
nuget Fake.Core.Target //"
// loads the intellisense script for IDE support
#load "./.fake/e2e-docker-desktop.fsx/intellisense.fsx"

open Fake.Core
open Fake.IO
open FSharp.Data
open System.Text.RegularExpressions

type DockerInspectProvider = JsonProvider<"""
[
    {
        "Id": "0bf71edcb5d7bd179d3ef8800128bc379e8527e9bba203a551d4b44b7b716650",
        "Created": "2019-01-24T13:33:58.8443439Z",
        "Path": "kube-apiserver",
        "Args": [
            "--advertise-address=192.168.65.3",
            "--secure-port=6443"
        ]
    }
]
""">

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
    let processResult =
        CreateProcess.fromRawCommand commandPath args
        |> CreateProcess.redirectOutput
        |> Proc.run
    if processResult.ExitCode <> 0 then
        failwithf "exit code: %d; error: %s" processResult.ExitCode processResult.Result.Error
    processResult.Result.Output
    |> String.trim

let docker (args:seq<string>) = execRaw "docker.exe" args

Target.create "Default" (fun _ ->
    Trace.trace "EventStore.Charts")

Target.create "Create Test Container" (fun _ ->
    docker
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
    |> String.trim
    |> FakeVar.set "containerId")

Target.createFinal "Remove Test Container" (fun _ ->
    let containerId = FakeVar.getOrFail "containerId"
    docker
        [ "container"
          "rm"
          "--force"
          containerId ]
    |> ignore)

Target.create "Get API Server" (fun _ ->
    let apiServerContainerId = 
        docker
            [ "container"
              "list"
              "--filter" 
              "name=k8s_kube-apiserver"
              "--format"
              "{{ .ID }}" ]
    if apiServerContainerId = "" then
        failwith "ERROR: API-Server container not found. Make sure 'Show system containers' is enabled in Docker Desktop 'Preferences'!"
    apiServerContainerId
    |> FakeVar.set "apiServerContainerId")

let getApiServerArg apiServerContainerId arg =
    let getArg arg input =
        let pattern = sprintf "%s=(.*)" arg
        let m = Regex.Match(input, pattern)
        if m.Success then m.Groups.[1].Value
        else ""
    let argValue =
        docker        
            [ "container"
              "inspect"
              apiServerContainerId ]
        |> DockerInspectProvider.Parse
        |> Array.head
        |> fun result -> result.Args
        |> String.concat "\n"
        |> getArg arg
    if argValue = "" then
        failwithf "could not find match for %s" arg
    argValue

Target.create "Configure kubectl" (fun _ ->
    let containerId = FakeVar.getOrFail "containerId"
    let apiServerContainerId = FakeVar.getOrFail "apiServerContainerId"
    let ipAddress = getApiServerArg apiServerContainerId "--advertise-address"
    let port = getApiServerArg apiServerContainerId "--secure-port"
    docker
        [ "cp"
          sprintf "%s/.kube" homeDir 
          sprintf "%s:/root/.kube" containerId ]
    |> ignore
    
    docker
        [ "exec"
          containerId
          "kubectl"
          "config"
          "set-cluster"
          "docker-for-desktop-cluster"
          sprintf "--server=https://%s:%s" ipAddress port ]
    |> ignore

    docker
        [ "exec"
          containerId
          "kubectl"
          "config"
          "set-cluster"
          "docker-for-desktop-cluster"
          "--insecure-skip-tls-verify=true" ]
    |> ignore

    docker
        [ "exec"
          containerId
          "kubectl"
          "config"
          "use-context"
          "docker-for-desktop" ]
    |> ignore)

Target.create "Run Tillerless" (fun _ ->
    let containerId = FakeVar.getOrFail "containerId"
    docker
        [ "exec"
          containerId
          "apk"
          "add"
          "bash" ]
    |> ignore

    docker
        [ "exec"
          containerId
          "helm"
          "init"
          "--client-only" ]
    |> ignore

    docker
        [ "exec"
          containerId
          "helm"
          "plugin"
          "install"
          "https://github.com/rimusz/helm-tiller" ]
    |> ignore

    docker
        [ "exec"
          containerId
          "bash"
          "-c"
          "helm tiller start-ci >/dev/null 2>&1 &" ]
    |> ignore

    docker
        [ "exec"
          containerId
          "bash"
          "-c"
          "while ! nc -z localhost 44134; do sleep 1; done" ]
    |> ignore)


Target.create "Test" (fun _ ->
    let containerId = FakeVar.getOrFail "containerId"
    docker
        [ "exec"
          "-e"
          "HELM_HOST=127.0.0.1:44134"
          "-e"
          "HELM_TILLER_SILENT=true"
          containerId
          "ct"
          "install"
          "--config"
          "/workdir/test/ct.yaml" ]
    |> ignore
)

open Fake.Core.TargetOperators

Target.activateFinal "Remove Test Container"

"Default"
 ==> "Create Test Container"
 ==> "Get API Server"
 ==> "Configure kubectl"
 ==> "Run Tillerless"
 ==> "Test"

Target.runOrDefault "Default"
