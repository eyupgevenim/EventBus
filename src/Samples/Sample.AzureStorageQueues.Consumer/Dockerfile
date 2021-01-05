#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

#Depending on the operating system of the host machines(s) that will build or run the containers, the image specified in the FROM statement may need to be changed.
#For more information, please see https://aka.ms/containercompat

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-nanoserver-1903 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-nanoserver-1903 AS build
WORKDIR /src
COPY ["src/Samples/Sample.AzureStorageQueues.Consumer/Sample.AzureStorageQueues.Consumer.csproj", "src/Samples/Sample.AzureStorageQueues.Consumer/"]
COPY ["src/Samples/Sample.Shared/Sample.Shared.csproj", "src/Samples/Sample.Shared/"]
COPY ["src/EventBus/EventBus.csproj", "src/EventBus/"]
COPY ["src/EventBus.AzureStorageQueues/EventBus.AzureStorageQueues.csproj", "src/EventBus.AzureStorageQueues/"]
RUN dotnet restore "src/Samples/Sample.AzureStorageQueues.Consumer/Sample.AzureStorageQueues.Consumer.csproj"
COPY . .
WORKDIR "/src/src/Samples/Sample.AzureStorageQueues.Consumer"
RUN dotnet build "Sample.AzureStorageQueues.Consumer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Sample.AzureStorageQueues.Consumer.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Sample.AzureStorageQueues.Consumer.dll"]