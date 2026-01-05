# Development container for AccelByte Extend Service Extension
FROM ubuntu:22.04

# Avoid warnings by switching to noninteractive
ENV DEBIAN_FRONTEND=noninteractive

ARG PROTOC_VERSION=21.9
ARG DOTNET_VERSION=8.0
ARG GO_VERSION=1.24.10
ARG EXTEND_HELPER_CLI_VERSION=0.0.9

# Configure apt and install packages
RUN apt-get update \
    && apt-get -y install --no-install-recommends \
    #
    # Install essential development tools
    build-essential \
    ca-certificates \
    git \
    sudo \
    unzip \
    wget \
    #
    # Install .NET SDK
    && wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && rm packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get -y install --no-install-recommends dotnet-sdk-${DOTNET_VERSION} \
    #
    # Install Protocol Buffers compiler
    && wget -O protoc.zip https://github.com/protocolbuffers/protobuf/releases/download/v${PROTOC_VERSION}/protoc-${PROTOC_VERSION}-linux-x86_64.zip \
    && unzip protoc.zip -d /usr/local \
    && rm protoc.zip \
    && chmod +x /usr/local/bin/protoc \
    #
    # Install Go 1.24
    && wget -O go.tar.gz https://go.dev/dl/go${GO_VERSION}.linux-amd64.tar.gz \
    && tar -C /usr/local -xzf go.tar.gz \
    && rm go.tar.gz \
    #
    # Install Extend Helper CLI
    && wget -O /usr/local/bin/extend-helper-cli https://github.com/AccelByte/extend-helper-cli/releases/download/v${EXTEND_HELPER_CLI_VERSION}/extend-helper-cli-linux_amd64 \
    && chmod +x /usr/local/bin/extend-helper-cli \
    #
    # Clean up
    && apt-get autoremove -y \
    && apt-get clean -y \
    && rm -rf /var/lib/apt/lists/*

# Set up .NET environment
ENV DOTNET_ROOT=/usr/share/dotnet
ENV PATH=$DOTNET_ROOT:$DOTNET_ROOT/bin:$PATH

# Set up Go environment
ENV GOROOT=/usr/local/go
ENV GOPATH=/go
ENV PATH=$GOPATH/bin:$GOROOT/bin:$PATH

# Create a non-root user
ARG USERNAME=vscode
ARG USER_UID=1000
ARG USER_GID=$USER_UID

# Create user group, user account, and configure passwordless sudo access
RUN groupadd --gid $USER_GID $USERNAME \
    && useradd --uid $USER_UID --gid $USER_GID -m $USERNAME \
    && echo $USERNAME ALL=\(root\) NOPASSWD:ALL > /etc/sudoers.d/$USERNAME \
    && chmod 0440 /etc/sudoers.d/$USERNAME

# Create workspace and GOPATH directories
RUN mkdir -p /workspace && chown $USERNAME:$USERNAME /workspace \
    && mkdir -p /go && chown $USERNAME:$USERNAME /go

# Switch to non-root user
USER $USERNAME

# Install Go Language Server (required by Go extension)
RUN go install -v golang.org/x/tools/gopls@latest

# Install Go Debugger
RUN go install -v github.com/go-delve/delve/cmd/dlv@latest

# Install protoc Go tools and plugins
RUN go install -v google.golang.org/protobuf/cmd/protoc-gen-go@latest \
    && go install -v google.golang.org/grpc/cmd/protoc-gen-go-grpc@latest \
    && go install -v github.com/grpc-ecosystem/grpc-gateway/v2/protoc-gen-grpc-gateway@latest \
    && go install -v github.com/grpc-ecosystem/grpc-gateway/v2/protoc-gen-openapiv2@latest

# Switch back to dialog for any ad-hoc use of apt-get
ENV DEBIAN_FRONTEND=dialog

WORKDIR /workspace
