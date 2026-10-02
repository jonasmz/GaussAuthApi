FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/GaussAuth.Api/GaussAuth.Api.csproj \
    && dotnet publish src/GaussAuth.Api/GaussAuth.Api.csproj -c Release -o /app/publish --no-restore \
    && dotnet tool restore \
    && mkdir -p /app/db \
    && dotnet ef migrations script --idempotent --project src/GaussAuth.Infrastructure --startup-project src/GaussAuth.Api --output /app/db/gaussauth-schema.sql

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /app/db /app/db
RUN mkdir -p /data/profile-images /data/keys && chown -R app:app /data
USER app
ENV ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
VOLUME ["/data/profile-images", "/data/keys"]
ENTRYPOINT ["dotnet", "GaussAuth.Api.dll"]
