#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["src/Samples/Sample.GoogleCloudPubSub.Consumer/Sample.GoogleCloudPubSub.Consumer.csproj", "src/Samples/Sample.GoogleCloudPubSub.Consumer/"]
COPY ["src/Samples/Sample.Shared/Sample.Shared.csproj", "src/Samples/Sample.Shared/"]
COPY ["src/EventBus/EventBus.csproj", "src/EventBus/"]
COPY ["src/EventBus.GoogleCloudPubSub/EventBus.GoogleCloudPubSub.csproj", "src/EventBus.GoogleCloudPubSub/"]
RUN dotnet restore "src/Samples/Sample.GoogleCloudPubSub.Consumer/Sample.GoogleCloudPubSub.Consumer.csproj"
COPY . .
WORKDIR "/src/src/Samples/Sample.GoogleCloudPubSub.Consumer"
RUN dotnet build "Sample.GoogleCloudPubSub.Consumer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Sample.GoogleCloudPubSub.Consumer.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Sample.GoogleCloudPubSub.Consumer.dll"]