# Changelog 
# 
## 0.2.3: May 24, 2019
- Add serviceName to the eventstore statefulset
- 
## 0.2.2: Apirl 17, 2019
- Fix port-forward by only advertising address for multiple nodes

## 0.2.1: March 27, 2019
- Make dns resolver configurable
 
## 0.2.0: February 28, 2019
- Add manifests that allow the user to schedule a scavenging CronJob

## 0.1.5: February 20, 2019
- Make pod annotations configurable

## 0.1.4: February 16, 2019
- execute post-install script also on post-upgrade
 
## 0.1.3: February 14, 2019
- set EVENTSTORE_EXT_IP to '0.0.0.0' (fixes `kubectl port-forward`)

## 0.1.2: February 9, 2019
- Increase deadline for post install job

## 0.1.1: February 7, 2019
- Replace int-http-port with ext-tcp-port in eventstore-service

## 0.1.0
- Initial release
