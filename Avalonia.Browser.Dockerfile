FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

RUN apt update && apt install python3 which -y
RUN dotnet workload install wasm-tools wasm-experimental

COPY Directory.Packages.props Directory.Packages.props
COPY GameEngine.Runner.Avalonia/GameEngine.Runner.Avalonia.Browser GameEngine.Runner.Avalonia/GameEngine.Runner.Avalonia.Browser
COPY GameEngine.Runner.Avalonia/GameEngine.Runner.Avalonia GameEngine.Runner.Avalonia/GameEngine.Runner.Avalonia
COPY GameEngine.Core GameEngine.Core
COPY GameEngine.Demo GameEngine.Demo

RUN dotnet build GameEngine.Runner.Avalonia/GameEngine.Runner.Avalonia.Browser/GameEngine.Runner.Avalonia.Browser.csproj -c Release
RUN dotnet publish GameEngine.Runner.Avalonia/GameEngine.Runner.Avalonia.Browser/GameEngine.Runner.Avalonia.Browser.csproj -c Release -o /app/publish 

FROM nginx:alpine AS runtime
RUN rm -rf /usr/share/nginx/html/*
COPY --from=build /app/publish/wwwroot/ /usr/share/nginx/html/
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
