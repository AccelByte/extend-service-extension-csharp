# Dev Container

The easiest way to get started is using the provided Dev Container configuration, which includes all necessary tools and dependencies pre-configured.

**Requirements:**
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) 4.30+ or Docker Engine v23.0+
- [Visual Studio Code](https://code.visualstudio.com/) with the [Dev Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers)

**Quick Start:**
1. Open the project in VS Code
2. Click "Reopen in Container" when prompted (or use Command Palette: "Dev Containers: Reopen in Container")
3. Wait for the container to build and dependencies to install automatically
4. Start developing!

> :bulb: The Dev Container automatically installs .NET 8 SDK, Go 1.24, and all required dependencies.

## GitHub Codespaces

You can also use this Dev Container configuration with [GitHub Codespaces](https://github.com/features/codespaces), which provides a cloud-based development environment without requiring Docker Desktop or local setup.

**Benefits:**
- No local Docker installation required
- Access your development environment from any device with a web browser
- Pre-configured environment ready in minutes
- Same Dev Container configuration works seamlessly

**Quick Start with GitHub Codespaces:**
1. Navigate to your repository on GitHub
2. Click the green "Code" button
3. Select the "Codespaces" tab
4. Click "Create codespace on main" (or select your branch)
5. Wait for the codespace to initialize and build
6. Start developing in your browser or VS Code!

> :bulb: GitHub Codespaces uses the same Dev Container configuration, so you'll get .NET 8 SDK, Go 1.24, and all dependencies automatically installed.
