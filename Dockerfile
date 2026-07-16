FROM node:22-alpine AS node-build
WORKDIR /App/ClientApp
COPY ClientApp/package*.json ./
RUN npm ci
COPY ClientApp/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /App
COPY . ./
COPY --from=node-build /App/wwwroot ./wwwroot
RUN dotnet restore
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /App
COPY --from=build-env /App/out .
ENTRYPOINT ["dotnet", "MapaMensal.dll"]
