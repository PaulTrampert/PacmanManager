FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
ENV ConnectionStrings__pacmanmanager="Host=postgres;Port=5432;User Id=pacmanmanager;Password=password;"
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY [".config/dotnet-tools.json", "./.config/"]
RUN dotnet tool restore
COPY ["PacmanManager.Migrations/PacmanManager.Migrations.csproj", "PacmanManager.Migrations/"]
COPY ["PacmanManager.Entities/PacmanManager.Entities.csproj", "PacmanManager.Entities/"]
RUN dotnet restore "PacmanManager.Migrations/PacmanManager.Migrations.csproj"
COPY . .
RUN dotnet build PacmanManager.Migrations/PacmanManager.Migrations.csproj
RUN dotnet ef migrations bundle --project PacmanManager.Migrations

FROM base AS final
WORKDIR /app
COPY --from=build /src/efbundle .
ENTRYPOINT ["./efbundle", "--connection", "$ConnectionStrings__pacmanmanager" ]
