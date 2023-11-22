#Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 as build
WORKDIR /app
COPY src/AspireSample.FileProcessorWorker ./
RUN dotnet restore
RUN dotnet publish -c Release -o out
#Runtime
FROM mcr.microsoft.com/dotnet/runtime:8.0 
WORKDIR /app
COPY --from=build /app/out ./
ENTRYPOINT [ "dotnet", "AspireSample.FileProcessorWorker.dll" ]