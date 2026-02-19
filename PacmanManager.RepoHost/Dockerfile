FROM archlinux/archlinux AS base
ARG APP_UID=1001
ARG APP_GID=1001
ARG APP_USER=app
ARG APP_HOME=/app
ENV DATA_DIR=/data
ENV ASPNETCORE_HTTP_PORTS=8080
RUN pacman -Syu --noconfirm && \
    pacman -S --noconfirm aspnet-runtime && \
    groupadd -g ${APP_GID} ${APP_USER} && \
    useradd --uid ${APP_UID} --gid ${APP_GID} --home ${APP_HOME} -m ${APP_USER} && \
    mkdir ${DATA_DIR} && \
    chown -R ${APP_USER}:${APP_USER} ${DATA_DIR} ${APP_HOME}
USER $APP_USER
WORKDIR ${APP_HOME}
VOLUME ${DATA_DIR}
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["LibAlpmSharp/LibAlpmSharp.csproj", "LibAlpmSharp/"]
COPY ["PacmanManager.CliTools/PacmanManager.CliTools.csproj", "PacmanManager.CliTools/"]
COPY ["PacmanManager.RepoHost/PacmanManager.RepoHost.csproj", "PacmanManager.RepoHost/"]
COPY ["PacmanManager.Entities/PacmanManager.Entities.csproj", "PacmanManager.Entities/"]
RUN dotnet restore "PacmanManager.RepoHost/PacmanManager.RepoHost.csproj"
COPY . .
WORKDIR "/src/PacmanManager.RepoHost"
RUN dotnet build "./PacmanManager.RepoHost.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./PacmanManager.RepoHost.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PacmanManager.RepoHost.dll"]
