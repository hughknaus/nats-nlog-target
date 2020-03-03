Using:
- NATS Streaming Server (binaries for v0.17.0-windows-386, includes license and README.md, ToDo: untilize package manager)

This console application provides a means for installing, configuring, and uninstalling NATS Streaming Server
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