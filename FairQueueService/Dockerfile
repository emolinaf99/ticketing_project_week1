FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/FairQueueService.Domain/FairQueueService.Domain.csproj",          "src/FairQueueService.Domain/"]
COPY ["src/FairQueueService.Application/FairQueueService.Application.csproj", "src/FairQueueService.Application/"]
COPY ["src/FairQueueService.Infrastructure/FairQueueService.Infrastructure.csproj", "src/FairQueueService.Infrastructure/"]
COPY ["src/FairQueueService.Api/FairQueueService.Api.csproj",                 "src/FairQueueService.Api/"]
RUN dotnet restore "src/FairQueueService.Api/FairQueueService.Api.csproj"

COPY . .
WORKDIR "/src/src/FairQueueService.Api"
RUN dotnet build "FairQueueService.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FairQueueService.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FairQueueService.Api.dll"]
