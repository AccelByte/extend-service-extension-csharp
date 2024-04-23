# 4. Installation and Setup

This chapter guides you through setting up your development environment. 
This involves installing required software, cloning the project repository, and 
setting up the project.

## 4.1. Software Installation

To get started, make sure you have the following software installed on your system:

1. [Bash](https://www.gnu.org/software/bash/): This is an sh-compatible shell that incorporates useful features from the Korn shell (ksh) and the C shell (csh).
2. [Docker](https://docs.docker.com/engine/install/): Docker is a platform for developers to develop, deploy, and run applications with containers.
3. [Make](https://www.gnu.org/software/make/): Make is a build automation tool.
4. [.NET SDK](https://dotnet.microsoft.com/en-us/download): Please refer to the official installation guide.

### 4.2. Cloning and Running the dependency repo (Optional)

This repository contains all dependencies that needed to be run for our service. 

So, by having these dependencies is up and running before you run the guild service you will be able to have your own observability stack in your local environment.

In our local observability stack we're using Grafana that will provide:

- Log
- Traces
- Metric

```bash
git clone https://github.com/AccelByte/grpc-plugin-dependencies.git
```

```bash
cd grpc-plugin-dependencies
```

and then do this

```bash
docker compose up

```
