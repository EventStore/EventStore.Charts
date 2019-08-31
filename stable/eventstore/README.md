# Event Store

[Event Store](https://eventstore.org/) is an open-source,
functional database with Complex Event Processing in JavaScript.

## TL;DR;

```shell
helm repo add eventstore https://eventstore.github.io/EventStore.Charts
helm repo update
```

> The default username and password for the admin interface
> is `admin:changeit`.

## Introduction

This chart bootstraps a [Event Store](https://hub.docker.com/r/eventstore/eventstore/)
deployment on a [Kubernetes](http://kubernetes.io) cluster
using the [Helm](https://helm.sh) package manager.

## Prerequisites

- Kubernetes 1.4+ with Beta APIs enabled
- PV provisioner support in the underlying infrastructure (Only when persisting data)

## Installing the Chart

Add the Event Store Charts repository.

```shell
helm repo add eventstore https://eventstore.github.io/EventStore.Charts
helm repo update
```

To install the Event Store chart with the release name `eventstore`:

```shell
helm install -n eventstore eventstore/eventstore
```

To install the Event Store chart with a custom admin password:

```shell
helm install -n eventstore eventstore/eventstore --set 'admin.password=<your admin password>'
```

This triggers Helm to run a post-install Job which resets the admin password using
the Event Store HTTP API. You can then use the username `admin` and the password set
in the above command to log into the admin interface.

The above commands install Event Store with the default configuration.
The [configuration](#configuration) section below lists the parameters
that can be configured during installation.

## Deleting the Chart

Delete the `eventstore` release.

```shell
helm delete eventstore --purge
```

This command removes all the Kubernetes components
associated with the chart and deletes the release.

## Configuration

The following table lists the configurable parameters of the Event Store chart and their default values.

| Parameter                            | Description                                                                   | Default                      |
| ------------------------------------ | ----------------------------------------------------------------------------- | ---------------------------- |
| `image`                              | Container image name                                                          | `eventstore/eventstore`      |
| `imageTag`                           | Container image tag                                                           | `release-4.1.1-hotfix1`      |
| `imagePullPolicy`                    | Container pull policy                                                         | `IfNotPresent`               |
| `imagePullSecrets`                   | Specify image pull secrets                                                    | `nil`                        |
| `clusterSize`                        | The number of nodes in the cluster                                            | `3`                          |
| `admin.jobImage`                     | Post install Job image with `curl` installed for setting admin password       | `tutum/curl`                 |
| `admin.jobImageTag`                  | Post install Job image tag                                                    | `latest`                     |
| `admin.password`                     | Custom password for admin interface (should be set in separate file)          | `nil`                        |
| `admin.serviceType`                  | Service type for the admin interface                                          | `ClusterIP`                  |
| `admin.proxyImage`                   | NGINX image for admin interface proxy                                         | `nginx`                      |
| `admin.proxyImageTag`                | NGINX image tag                                                               | `latest`                     |
| `podDisruptionBudget.enabled`        | Enable a pod disruption budget for nodes                                      | `false`                      |
| `podDisruptionBudget.minAvailable`   | Number of pods that must still be available after eviction                    | `2`                          |
| `podDisruptionBudget.maxUnavailable` | Number of pods that can be unavailable after eviction                         | `nil`                        |
| `extIp`                              | External IP address                                                           | `0.0.0.0`                    |
| `intHttpPort`                        | Internal HTTP port                                                            | `2112`                       |
| `extHttpPort`                        | External HTTP port                                                            | `2113`                       |
| `intTcpPort`                         | Internal TCP port                                                             | `1112`                       |
| `extTcpPort`                         | External TCP port                                                             | `1113`                       |
| `gossipAllowedDiffMs`                | The amount of drift, in ms, between clocks on nodes before gossip is rejected | `600000`                     |
| `eventStoreConfig`                   | Additional Event Store parameters                                             | `{}`                         |
| `scavenging.enabled`                 | Enable the scavenging CronJob for all nodes                                   | `false`                      |
| `scavenging.image`                   | The image to use for the scavenging CronJob                                   | `lachlanevenson/k8s-kubectl` |
| `scavenging.imageTag`                | The image tag use for the scavenging CronJob                                  | `latest`                     |
| `scavenging.schedule`                | The schedule to use for the scavenging CronJob                                | `0 2 * * *`                  |
| `persistence.enabled`                | Enable persistence using PVC                                                  | `false`                      |
| `persistence.existingClaim`          | Provide an existing PVC                                                       | `nil`                        |
| `persistence.accessMode`             | Access Mode for PVC                                                           | `ReadWriteOnce`              |
| `persistence.size`                   | Size of data volume                                                           | `8Gi`                        |
| `persistence.mountPath`              | Mount path of data volume                                                     | `/var/lib/eventstore`        |
| `persistence.annotations`            | Annotations for PVC                                                           | `{}`                         |
| `resources`                          | CPU/Memory resource request/limits                                            | Memory: `256Mi`, CPU: `100m` |
| `nodeSelector`                       | Node labels for pod assignment                                                | `{}`                         |
| `podAnnotations`                     | Pod annotations                                                               | `{}`                         |
| `tolerations`                        | Toleration labels for pod assignment                                          | `[]`                         |
| `affinity`                           | Affinity settings for pod assignment                                          | `{}`                         |

Specify each parameter using the `--set key=value[,key=value]` argument to `helm install`
or create a `values.yaml` file and use `helm install --values values.yaml`.

## Scaling Persistent Volume
After running Event Store for a while you may run into the situation where you have
outgrown the initial volume. Below will walk you through the steps to check the disk
usage and how to update if necessary.

You can check the disk usage of the StatefulSet pods using the `df` command:
```shell
kubectl exec eventstore-0 df
Filesystem     1K-blocks     Used Available Use% Mounted on
overlay         28056816 11530904  15326860  43% /
tmpfs              65536        0     65536   0% /dev
tmpfs            7976680        0   7976680   0% /sys/fs/cgroup
/dev/nvme0n1p9  28056816 11530904  15326860  43% /etc/hosts
shm                65536        4     65532   1% /dev/shm
/dev/nvme1n1     8191416  2602160   5572872  32% /var/lib/eventstore                        --> PVC usage
tmpfs            7976680       12   7976668   1% /run/secrets/kubernetes.io/serviceaccount
tmpfs            7976680        0   7976680   0% /sys/firmware
```
> If the `Use%` for `/var/lib/eventstore` mount is at an unacceptably high number, 
then follow one of the two options below depending on how your cluster is set up.

### __Option 1__: Resize PVC created with volume expansion enabled
You can check if volume expansion is enabled on the PVC StorageClass by running:
```shell
kubectl get storageclass <pvc storageclass> -o yaml
```
You should see the following in the output if volume expansion is enabled:
```yaml
apiVersion: storage.k8s.io/v1
kind: StorageClass
...
allowVolumeExpansion: true
...
```
1. First resize the PVC for each StatefulSet pod.
    ```shell
    kubectl edit pvc data-eventstore-0
    ```
    > This will open up the specification in your default text editor.
    ```yaml
    ...
    spec:
    accessModes:
    - ReadWriteOnce
    resources:
        requests:
        storage: 8Gi  --> change this to desired size
    ...
    ```
    > Save and close the file after editing. If you get the error 
    `only dynamically provisioned pvc can be resized and the storageclass that provisions the pvc must support resize` 
    then the storage class has not enabled volume expansion. No worries! Skip down to Option 2.
2. Delete the StatefulSet but keep the pods.
    ```shell
    kubectl delete sts --cascade=false eventstore
    ```
3. Update the chart values with the new storage request value that you edited in step (1). 
    ```shell
    helm upgrade eventstore eventstore/eventstore --set 'persistence.size=<value from step (1)>'
    ```

### __Option 2__: Resize PVC created without volume expansion enabled
This process is a bit involved but also a good exercise for backing up the database. We will
use AWS S3 as the storage backend but the process works just as well for other backends such as GCS.
1. Connect to one of the StatefulSet pods.
    ```shell
    kubectl exec -it eventstore-0 sh
    ```
2. Install Python (required for AWS CLI).
    ```shell
    apt-get update
    apt-get install python3
    export PATH="$PATH:/usr/local/bin"
    ```
    > ref: https://docs.aws.amazon.com/cli/latest/userguide/install-linux-python.html
3. Install pip.
    ```shell
    curl -O https://bootstrap.pypa.io/get-pip.py
    python3 get-pip.py
    ```
4. Install the AWS CLI.
    ```shell
    pip install awscli
    ```
    > ref: https://docs.aws.amazon.com/cli/latest/userguide/install-linux.html
5. Configure the AWS CLI.
    ```shell
    aws configure
    ```
    > Enter your credentials when prompted.
6. Dry run the copy.
    ```shell
    aws s3 cp --recursive /var/lib/eventstore/ s3://<bucket>/backup/eventstore/20190830/ --dryrun
    ```
    > Change the S3 path to your preferred destination. If the copy operation looks good, proceed to the next step.
7. Copy the files.
    ```shell
    aws s3 cp --recursive /var/lib/eventstore/ s3://<bucket>/backup/eventstore/20190830/
    ```
    > ref: https://eventstore.org/docs/server/database-backup/#backing-up-a-database
8. Create a new Event Store cluster with the new volume size. It is recommended to set `allowVolumeExpansion: true` 
on your cluster's StorageClass prior to creating the cluster. This will make it easier to resize in the future by
following the steps in Option 1 above.
See [the documentation](https://kubernetes.io/docs/concepts/storage/persistent-volumes/#expanding-persistent-volumes-claims)
for more details.
9. Repeat steps (1) through (5) on the new cluster StatefulSet pod.
10. Copy the backup files from S3.
    ```shell
    aws s3 cp s3://<bucket>/backup/eventstore/20190830/chaser.chk /var/lib/eventstore/truncate.chk
    aws s3 cp --recursive --exclude="truncate.chk" s3://<bucket>/backup/eventstore/20190830 /var/lib/eventstore
    ```
    > ref: https://eventstore.org/docs/server/database-backup/#restoring-a-database
11. Restart the StatefulSet pods.
    ```shell
    kubectl delete $(kubectl get pod -o name -l app.kubernetes.io/component=database,app.kubernetes.io/instance=eventstore)
    ```
12. Check the logs to ensure Event Store is processing the chunks.
    ```
    kubectl logs -f eventstore-0
    ...
    [00001,12,11:16:47.264] CACHED TFChunk #1-1 (chunk-000001.000000) in 00:00:00.0000495.
    [00001,12,11:17:07.681] Completing data chunk 1-1...
    ...
    ```

## Additional Resources

- [Event Store Docs](https://eventstore.org/docs/)
- [Event Store Parameters](https://eventstore.org/docs/server/command-line-arguments/index.html#parameter-list)
- [Event Store Docker Container](https://github.com/EventStore/eventstore-docker)
- [Chart Template Guide](https://github.com/helm/helm/tree/master/docs/chart_template_guide)
