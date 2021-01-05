#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["src/Samples/Sample.IBMMQ.Consumer/Sample.IBMMQ.Consumer.csproj", "src/Samples/Sample.IBMMQ.Consumer/"]
COPY ["src/Samples/Sample.Shared/Sample.Shared.csproj", "src/Samples/Sample.Shared/"]
COPY ["src/EventBus/EventBus.csproj", "src/EventBus/"]
COPY ["src/EventBus.IBMMQ/EventBus.IBMMQ.csproj", "src/EventBus.IBMMQ/"]
RUN dotnet restore "src/Samples/Sample.IBMMQ.Consumer/Sample.IBMMQ.Consumer.csproj"
COPY . .
WORKDIR "/src/src/Samples/Sample.IBMMQ.Consumer"
RUN dotnet build "Sample.IBMMQ.Consumer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Sample.IBMMQ.Consumer.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Sample.IBMMQ.Consumer.dll"]