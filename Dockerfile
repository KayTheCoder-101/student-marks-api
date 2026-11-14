FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/MarksApi/MarksApi.csproj src/MarksApi/
RUN dotnet restore src/MarksApi/MarksApi.csproj
COPY src/MarksApi/ src/MarksApi/
RUN dotnet publish src/MarksApi/MarksApi.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "MarksApi.dll"]
