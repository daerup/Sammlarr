FROM node:24-slim AS node

FROM mcr.microsoft.com/dotnet/sdk:11.0.100-rc.1 AS build
COPY --from=node /usr/local/bin/node /usr/local/bin/node
COPY --from=node /usr/local/lib/node_modules/npm /usr/local/lib/node_modules/npm
RUN ln -s /usr/local/lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm
ARG VERSION=0.0.0-local
ENV HUSKY=0
WORKDIR /src
COPY . .
RUN --mount=type=cache,id=sammlarr-nuget,target=/root/.nuget/packages \
    --mount=type=cache,id=sammlarr-npm,target=/root/.npm \
    dotnet publish Web/Web.csproj -c Release -r linux-x64 --self-contained false -o /app -p:MinVerVersionOverride=$VERSION -p:ExcludeStaging=true

FROM mcr.microsoft.com/dotnet/aspnet:11.0.0-rc.1
WORKDIR /app
COPY --from=build /app .
USER root
RUN mkdir -p /data && chown $APP_UID /data
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080 \
    Sammlarr__ConnectionString="Data Source=/data/sammlarr.db" \
    Sammlarr__DataProtection__KeyRingPath=/data/dp-keys
EXPOSE 8080
ENTRYPOINT ["dotnet", "Web.dll"]
