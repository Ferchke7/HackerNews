FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["HackerNews.Common/HackerNews.Common.csproj", "HackerNews.Common/"]
COPY ["HackerNews.Analyzer/HackerNews.Analyzer.csproj", "HackerNews.Analyzer/"]
COPY ["HackerNews/HackerNews.csproj", "HackerNews/"]
RUN dotnet restore "HackerNews/HackerNews.csproj"
COPY . .
WORKDIR "/src/HackerNews"
RUN dotnet build "./HackerNews.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./HackerNews.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HackerNews.dll"]
