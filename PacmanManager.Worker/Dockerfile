# The Worker runs repo-add and reads packages through libalpm, so it is built on Arch, like RepoHost,
# rather than on the Microsoft runtime image. It runs as the same uid as RepoHost, because the two
# share one DATA_DIR volume and each must be able to read and write what the other wrote there.
FROM archlinux/archlinux AS base
ARG APP_UID=1001
ARG APP_GID=1001
ARG APP_USER=app
ARG APP_HOME=/app
ENV DATA_DIR=/data
RUN pacman -Syu --noconfirm && \
    pacman -S --noconfirm dotnet-runtime && \
    groupadd -g ${APP_GID} ${APP_USER} && \
    useradd --uid ${APP_UID} --gid ${APP_GID} --home ${APP_HOME} -m ${APP_USER} && \
    mkdir ${DATA_DIR} && \
    chown -R ${APP_USER}:${APP_USER} ${DATA_DIR} ${APP_HOME}
USER $APP_USER
WORKDIR ${APP_HOME}
VOLUME ${DATA_DIR}

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["PacmanManager.Worker/PacmanManager.Worker.csproj", "PacmanManager.Worker/"]
RUN dotnet restore "PacmanManager.Worker/PacmanManager.Worker.csproj"
COPY . .
WORKDIR "/src/PacmanManager.Worker"
RUN dotnet build "./PacmanManager.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./PacmanManager.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PacmanManager.Worker.dll"]
