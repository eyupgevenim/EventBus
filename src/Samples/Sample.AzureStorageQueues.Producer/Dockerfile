#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

#Depending on the operating system of the host machines(s) that will build or run the containers, the image specified in the FROM statement may need to be changed.
#For more information, please see https://aka.ms/containercompat

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-nanoserver-1903 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-nanoserver-1903 AS build
WORKDIR /src
COPY ["src/Samples/Sample.AzureStorageQueues.Producer/Sample.AzureStorageQueues.Producer.csproj", "src/Samples/Sample.AzureStorageQueues.Producer/"]
COPY ["src/Samples/Sample.Shared/Sample.Shared.csproj", "src/Samples/Sample.Shared/"]
COPY ["src/EventBus/EventBus.csproj", "src/EventBus/"]
COPY ["src/EventBus.AzureStorageQueues/EventBus.AzureStorageQueues.csproj", "src/EventBus.AzureStorageQueues/"]
RUN dotnet restore "src/Samples/Sample.AzureStorageQueues.Producer/Sample.AzureStorageQueues.Producer.csproj"
COPY . .
WORKDIR "/src/src/Samples/Sample.AzureStorageQueues.Producer"
RUN dotnet build "Sample.AzureStorageQueues.Producer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Sample.AzureStorageQueues.Producer.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Sample.AzureStorageQueues.Producer.dll"]