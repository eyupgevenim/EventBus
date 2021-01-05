#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["src/Samples/Sample.ServiceBus.Producer/Sample.ServiceBus.Producer.csproj", "src/Samples/Sample.ServiceBus.Producer/"]
COPY ["src/Samples/Sample.Shared/Sample.Shared.csproj", "src/Samples/Sample.Shared/"]
COPY ["src/EventBus/EventBus.csproj", "src/EventBus/"]
COPY ["src/EventBus.ServiceBus/EventBus.ServiceBus.csproj", "src/EventBus.ServiceBus/"]
RUN dotnet restore "src/Samples/Sample.ServiceBus.Producer/Sample.ServiceBus.Producer.csproj"
COPY . .
WORKDIR "/src/src/Samples/Sample.ServiceBus.Producer"
RUN dotnet build "Sample.ServiceBus.Producer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Sample.ServiceBus.Producer.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Sample.ServiceBus.Producer.dll"]