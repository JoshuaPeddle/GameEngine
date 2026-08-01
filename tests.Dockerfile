FROM mcr.microsoft.com/dotnet/sdk:10.0
RUN apt-get update \
 && apt-get install -y --no-install-recommends libfontconfig1 \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /work
COPY . .
RUN dotnet test GameEngine.Core.Tests/GameEngine.Core.Tests.csproj -c Release --nologo
RUN dotnet test GameEngine.Demo.Tests/GameEngine.Demo.Tests.csproj -c Release --nologo
