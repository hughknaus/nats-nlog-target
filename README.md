# NATS NLog Target
**With .NET Core, NATS (server + streaming server), NLog, Docker**

This project creates a NATS target for NLog to stream log messages 

![Image of NATS Brokered Throughput](https://github.com/hughknaus/nats-nlog-example/blob/master/NATS_Brokered_Throughput.png)

Read about NATS here: https://nats.io/about/

Using:
	Run using latest Docker Images:
	  - Latest Docker Images for NATS Server and NATS Streaming Server
	  - Docker Swarm (NATS in "cluster" mode)
	Run as Windows services:
	  - NATS Streaming Server (binaries for v0.17.0-windows-386, includes license and README.md, ToDo: untilize package manager)
	  - Clustered mode or Fault Tolerant mode

Includes console application to provide a means for installing, configuring, and uninstalling NATS Streaming Server
	One of two configurations:
	  1. Cluster
		- 3 instances (node-A, node-B, node-C)
		- Runing on different local ports (ToDo: configurations for running on different machines)
			- Client ports: 4221, 4222, 4223
			- Cluster listening ports: 6221, 6222, 6223
		- HTTP server monitor (port: 8222)
	  2. Fault Tolerant
		- 2 instances (node-A, node-B)
		- Runing on different local ports (ToDo: configurations for running on different machines)
			- Client ports: 4221, 4222
			- HA listening ports: 6221, 6222
			- Shared file data store
		- HTTP server monitor (port: 8222)

Also, includes a docker-compose file of similar (but different) setup so that the above can be ran in containers.  
The major difference here is that a Docker Swarm was created that enlists NATS Servers along with the NATS Streaming Servers to provide communication amongst the cluster.
If you're going to use the Docker containers you'll need to ensure that you're using the correct settings for:
  - NatsNlogConsumer > appsettings.json (see comments in file)
  - NatsNlogExample > nlog.config (see comments in file)