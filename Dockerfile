FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Pdmt.Api/Pdmt.Api.csproj Pdmt.Api/
RUN dotnet restore Pdmt.Api/Pdmt.Api.csproj
COPY Pdmt.Api/ Pdmt.Api/
RUN dotnet publish Pdmt.Api/Pdmt.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Pdmt.Api.dll"]
