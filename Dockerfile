#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base

RUN mkdir -p /scripts
COPY change-time-zone.sh /scripts
WORKDIR /scripts
RUN chmod +x change-time-zone.sh
RUN ./change-time-zone.sh

WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["EcbRatesUpdateService.csproj", "."]
RUN dotnet restore "./EcbRatesUpdateService.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "EcbRatesUpdateService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "EcbRatesUpdateService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EcbRatesUpdateService.dll"]