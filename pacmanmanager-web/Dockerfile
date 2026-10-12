# The build context is this directory, so the image never sees the .NET sources.
# See docs/web-ui-architecture/web-ui-architecture.md, "Hosting".
FROM node:lts AS build
WORKDIR /src
# The manifests on their own first, so a source change reuses the installed dependencies.
COPY ["package.json", "package-lock.json", "./"]
RUN npm ci
COPY . .
# prebuild runs `generate`, so no generated file needs to be in the context.
RUN npm run build

# SpaHost serves wwwroot, falls back to index.html for unknown paths, and does sign-in and the API
# proxy. Its configuration is runtime environment; compose.yaml sets it for local development.
FROM paultrampert/spahost:0.8.60 AS final
COPY --from=build /src/dist/ /app/wwwroot/
