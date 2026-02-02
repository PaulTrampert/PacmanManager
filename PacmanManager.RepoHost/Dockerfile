FROM archlinux/archlinux AS base
ARG APP_UID=1001
ARG APP_GID=1001
ARG APP_USER=app
ARG APP_HOME=/home/${APP_USER}
ENV APP_DIR=${APP_HOME}/app/
ENV DATA_DIR=${APP_HOME}/data/
RUN pacman -Syu --noconfirm && \
    pacman -S --noconfirm aspnet-runtime && \
    groupadd -g ${APP_GID} ${APP_USER} && \
    useradd --uid ${APP_UID} --gid ${APP_GID} --home ${APP_HOME} -m ${APP_USER} 
USER $APP_USER
WORKDIR ${APP_DIR}
VOLUME ${DATA_DIR}
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["LibAlpmSharp/LibAlpmSharp.csproj", "LibAlpmSharp/"]
COPY ["PacmanManager.CliTools/PacmanManager.CliTools.csproj", "PacmanManager.CliTools/"]
COPY ["PacmanManager.RepoHost/PacmanManager.RepoHost.csproj", "PacmanManager.RepoHost/"]
RUN dotnet restore "PacmanManager.RepoHost/PacmanManager.RepoHost.csproj"
COPY . .
WORKDIR "/src/PacmanManager.RepoHost"
RUN dotnet build "./PacmanManager.RepoHost.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./PacmanManager.RepoHost.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR ${APP_DIR}
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PacmanManager.RepoHost.dll"]
