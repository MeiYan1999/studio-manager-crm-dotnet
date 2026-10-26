FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY MayAlbum.csproj ./
RUN dotnet restore MayAlbum.csproj

COPY . ./
RUN dotnet publish MayAlbum.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://0.0.0.0:10000
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
EXPOSE 10000

ENTRYPOINT ["dotnet", "MayAlbum.dll"]
