# 3. Understanding the Guild Service

## 3.1. Conceptual Overview of the Guild Service

In the context of game development, a Guild Service is responsible for managing player groups,
commonly known as "guilds". These guilds allow players to team up for cooperative gameplay, 
create a sense of community within the game, and compete together in multiplayer games.

## 3.2. Use Case for Guild Service

To make this tutorial more realistic and practical, 
we have chosen the Guild Service as the specific use case. This is because guild services are commonly used in online games, making it a relatable and realistic example.

The Guild Service we're going to build will include the following key features:

- Guild achievements and progress tracking

## 3.3. Guild Service Architecture

Our Guild Service will be designed as a microservice and will include:

- A gRPC server to handle client requests
- A gRPC-Gateway as a RESTful gateway to expose the underlying gRPC server.
- AccelByte CloudSave service integration for data persistence

Now that we've laid the groundwork for our Guild Service, the next chapter will guide you 
through the installation and setup of the necessary tools and environment to start building.