FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["support/HttpProxy/HttpProxy.csproj", "support/HttpProxy/"]
COPY ["Directory.Packages.props", "."]
RUN dotnet restore "support/HttpProxy/HttpProxy.csproj"
COPY . .
WORKDIR "/src/support/HttpProxy"
RUN dotnet build "./HttpProxy.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./HttpProxy.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ChaosWorkshop.HttpProxy.dll"]
