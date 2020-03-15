#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["NatsNlogPublisherExample/NatsNlogPublisherExample.csproj", "NatsNlogPublisherExample/"]
COPY ["NatsNlogTargets/NatsNlogTargets.csproj", "NatsNlogTargets/"]
RUN dotnet restore "NatsNlogPublisherExample/NatsNlogPublisherExample.csproj"
COPY . .
WORKDIR "/src/NatsNlogPublisherExample"
RUN dotnet build "NatsNlogPublisherExample.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "NatsNlogPublisherExample.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NatsNlogPublisherExample.dll"]