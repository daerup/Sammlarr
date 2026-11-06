FROM --platform=$BUILDPLATFORM node:24-slim@sha256:01d566e8d94d8c619591d78652babfccd3c6992a3dffa9ed5ab39e11cf6c70e9 AS node

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:11.0@sha256:59d092a889fbad09c1a0b0c5dd072c6315125b0294a5cddfa48ec892f63b2ab3 AS build
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

FROM mcr.microsoft.com/dotnet/aspnet:11.0-resolute-chiseled-extra@sha256:35fce970ba0a0d40f74878e4e93be9fd69f24e134623b5eea32721482f704b81
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
