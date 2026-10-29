FROM --platform=$BUILDPLATFORM node:24-slim@sha256:a5f4366e834695d5f363b920c104953a535d3a9864d1bdf74566a0ebe01daee5 AS node

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:11.0@sha256:3a9175b6c54b2f076817af1360f691c9994447b192faa48a1a1f112a516844c7 AS build
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

FROM mcr.microsoft.com/dotnet/aspnet:11.0-resolute-chiseled-extra@sha256:4a02e6f650d442ef3acf66932e74a2f63f3585b44e1990c3c8edd2c010298ff0
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
