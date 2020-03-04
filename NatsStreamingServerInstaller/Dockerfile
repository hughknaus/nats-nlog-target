#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["NatsStreamingServerInstaller/NatsStreamingServerInstaller.csproj", "NatsStreamingServerInstaller/"]
RUN dotnet restore "NatsStreamingServerInstaller/NatsStreamingServerInstaller.csproj"
COPY . .
WORKDIR "/src/NatsStreamingServerInstaller"
RUN dotnet build "NatsStreamingServerInstaller.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "NatsStreamingServerInstaller.csproj" -c Release -o /app/publish

FROM build AS clusterconf
COPY ClusterConfigs/ ClusterConfigs/
RUN ls -la ClusterConfigs/*

FROM build AS faulttolerantconf
COPY FaultTolerantConfigs/ FaultTolerantConfigs/
RUN ls -la FaultTolerantConfigs/*

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NatsStreamingServerInstaller.dll"]