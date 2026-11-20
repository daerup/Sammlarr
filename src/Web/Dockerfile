FROM --platform=$BUILDPLATFORM node:24-slim@sha256:dc26ae92c9fb15cf6b1d63e20f0944b2efe0f115b20e00be48fb23fceb061c54 AS node

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:11.0@sha256:65f3c7c6a25a239ab3e6719f2896147e7b68242ff2d8818dd70498bde6ef8bea AS build
ARG TARGETARCH
ARG VERSION=0.0.0-local
ARG SOURCE_DATE_EPOCH
COPY --from=node /usr/local/bin/node /usr/local/bin/node
COPY --from=node /usr/local/lib/node_modules/npm /usr/local/lib/node_modules/npm
RUN ln -s /usr/local/lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm
ENV HUSKY=0
WORKDIR /src
COPY . .
RUN --mount=type=cache,id=sammlarr-nuget,target=/root/.nuget/packages,sharing=locked \
    --mount=type=cache,id=sammlarr-npm,target=/root/.npm,sharing=locked \
    dotnet restore src/Web/Web.csproj --locked-mode && \
    dotnet publish src/Web/Web.csproj -c Release -a $TARGETARCH --self-contained false --no-restore -o /app \
        -p:MinVerVersionOverride=$VERSION -p:ExcludeStaging=true -p:ContinuousIntegrationBuild=true -p:DebugType=none
RUN mkdir -p /data

FROM mcr.microsoft.com/dotnet/aspnet:11.0-resolute-chiseled-extra@sha256:cea9d5494f358fe43082300c24d1cd9f651066b6b7de1e5735bca8d3ade68649
WORKDIR /app
COPY --from=build /app .
COPY --from=build --chown=$APP_UID:$APP_UID /data /data
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=6769 \
    Sammlarr__ConnectionString="Data Source=/data/sammlarr.db" \
    Sammlarr__DataProtection__KeyRingPath=/data/dp-keys
EXPOSE 6769
HEALTHCHECK --start-period=60s CMD ["dotnet", "Web.dll", "healthcheck"]
ENTRYPOINT ["dotnet", "Web.dll"]
