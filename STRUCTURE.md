# Solace Project Structure

Solace is a replacement server for Minecraft Earth™, based on Vienna. It is implemented as a distributed system with several specialized components.

## Core Components

- **Solace.ApiServer**: The main entry point for game clients. It handles HTTP APIs for authentication (PlayFab, Xbox Live), game logic (inventory, shop, buildplates, tappables), and map data.
- **Solace.EventBus.Server**: A custom high-performance message bus used for inter-service communication (e.g., requesting buildplate instances).
- **Solace.ObjectStore.Server**: A custom storage service for large binary blobs, such as buildplate previews and world data.
- **Solace.LauncherUI**: A Blazor-based web application that acts as an admin panel and server manager. It coordinates the startup and monitoring of all other services.
- **Solace.Buildplate (Launcher)**: Manages live Minecraft server instances (Fabric-based) for buildplates and encounters. It bridges the gap between the .NET backend and the Java-based Minecraft server.

## Libraries and Utilities

- **Solace.Common**: Shared data models, utilities, and constants used across multiple projects.
- **Solace.DB**: Data access layer, primarily using SQLite (`EarthDB`) for persistent player and game data.
- **Solace.StaticData**: Handles loading and accessing static game data (catalogs, levels, etc.) from the `staticdata` directory.
- **Solace.EventBus.Client** & **Solace.ObjectStore.Client**: Client libraries for communicating with their respective servers.
- **Solace.BuildplateRenderer** & **Solace.PreviewGenerator**: Tools for generating and rendering buildplate previews.
- **Solace.TappablesGenerator** & **Solace.TileRenderer**: Background services for generating map tappables and rendering map tiles.
- **Solace.KillHelper**: Utility for managing subprocess lifecycles.

## Data Storage

- **staticdata/**: Contains read-only game data, configuration, Minecraft JARs, and resource packs.
- **earth.db**: (SQLite) Persistent storage for player profiles, inventories, and buildplates.
- **live.db**: (SQLite) Temporary/session storage for live game state.
- **Object Store**: Storage for larger assets like buildplate previews.

## Workflow

1. **Startup**: `Solace.LauncherUI` is started by the user. It then launches the Event Bus, Object Store, Buildplate Launcher, API Server, and other background services.
2. **Client Connection**: The Minecraft Earth app connects to `Solace.ApiServer`.
3. **Session Management**: `ApiServer` handles authentication and retrieves player data from `EarthDB`.
4. **Buildplate Interaction**: When a player enters a buildplate, `ApiServer` sends a request via the Event Bus to `Solace.Buildplate` to start a Minecraft server instance.
5. **Map Interaction**: `TappablesGenerator` and `TileRenderer` provide the data and visuals for the in-game map.
