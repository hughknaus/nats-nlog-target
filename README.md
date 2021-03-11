# NATS NLog Target
**With .NET Core, NATS (server + streaming server), NLog, Docker**

This solution creates a NATS target for NLog to stream and distribute log messages through NATS Streaming Server.  Why NATS?  NATS, is a lightweight, high performance messaging system and offers an at-most-once quality of service.  Simply put, NATS is about publishing and listening for messages and easy to use for developers and operators.  Additionally, it is:

  * Highly-Performant
  * Always on and available
  * Extremely lightweight
  * At Most Once and At Least Once Delivery
  * Support for Observable and Scalable Services and Event/Data Streams
  * Client support for over 30 different programming languages
  * Cloud Native, a CNCF project with Kubernetes and Prometheus integrations

See the troughput comparison alongisde other messaging systems below
![Image of NATS Brokered Throughput](https://github.com/hughknaus/nats-nlog-example/blob/master/NATS_Brokered_Throughput.png)

###### (Image sorce from:  https://nats.io/about/)

### Using:
1. Run using latest Docker Images:
  * Latest Docker Images for NATS Server and NATS Streaming Server
  * Docker Swarm (NATS in "cluster" mode)
  * NatsNlogPublisherExample
  * NatsNlogConsumerExample
2. Run non-containerized:
  * NATS Streaming Server (binaries for v0.17.0-windows-386, includes license and README.md, ToDo: untilize package manager)
  * Clustered mode or Fault Tolerant mode
  * NatsStreamingServerInstaller (setup NATS Streaming Server cluster)
  * NatsNlogPublisherExample
  * NatsNlogConsumerExample


![Image of NATS Brokered Throughput](https://github.com/hughknaus/nats-nlog-example/blob/master/nats-nlog-example.png)

### Includes console application to provide a means for installing, configuring, and uninstalling NATS Streaming Server
One of three configurations:
  1. NATS Streaming Server Cluster (non-containerized)
    * 3 instances (node-A, node-B, node-C)
    * Runing on different local ports (ToDo: configurations for running on different machines)
      * Client ports: 4221, 4222, 4223
      * Cluster listening ports: 6221, 6222, 6223
    * HTTP server monitor (port: 8222)
  2. NATS Streaming Server Fault Tolerant (non-containerized)
    * 2 instances (node-A, node-B)
    * Runing on different local ports (ToDo: configurations for running on different machines)
      * Client ports: 4221, 4222
      * HA listening ports: 6221, 6222
      * Shared file data store
    * HTTP server monitor (port: 8222)
  3. NATS Server Cluster with NATS Stream Server Cluster (Docker Swarm)
    * 6 Containers (node-cluster-A, node-cluster-B, node-cluster-C, node-streaming-A, node-streaming-B, node-streaming-C, )

Read about NATS here: https://nats.io/about/
