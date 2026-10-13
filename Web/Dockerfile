FROM --platform=$BUILDPLATFORM node:24-slim@sha256:d6aa754f16b3197301076f047b5def2f02ea1dbbc2ca920407d46d7ec7f87b20 AS node

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:11.0@sha256:d0a39ca4acefe992f26030f8fb8c60e1b14ac088822cb3fdfbe536ecf843a675 AS build
ARG TARGETARCH
ARG VERSION=0.0.0-local
ARG SOURCE_DATE_EPOCH
COPY --from=node /usr/local/bin/node /usr/local/bin/node
COPY --from=node /usr/local/lib/node_modules/npm /usr/local/lib/node_modules/npm
RUN ln -s /usr/local/lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm
ENV HUSKY=0
WORKDIR /src
COPY . .
RUN --mount=type=cache,id=sammlarr-nuget,target=/root/.nuget/packages \
    --mount=type=cache,id=sammlarr-npm,target=/root/.npm \
    dotnet publish Web/Web.csproj -c Release -a $TARGETARCH --self-contained false -o /app \
        -p:MinVerVersionOverride=$VERSION -p:ExcludeStaging=true -p:ContinuousIntegrationBuild=true -p:DebugType=none
RUN mkdir -p /data

FROM mcr.microsoft.com/dotnet/aspnet:11.0-resolute-chiseled-extra@sha256:5c0161c2b5eb6bbbb8c775d0ac1be38598396261f6c54edc79308ac30d8229c7
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
