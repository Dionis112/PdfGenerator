FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src
COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages --mount=type=cache,target=/root/.m2 --mount=type=cache,target=/ikvm-cache \
    export IkvmCacheDir=/ikvm-cache && mkdir -p artifacts/packages \
    && dotnet test PdfGenerator.UnitTests -c Release \
    && dotnet test PdfGenerator.IntegrationTests -c Release \
    && dotnet pack PdfGenerator.Domain -c Release -o artifacts/packages \
    && dotnet pack PdfGenerator.Application -c Release -o artifacts/packages \
    && dotnet pack PdfGenerator.Infrastructure -c Release -o artifacts/packages \
    && dotnet restore PdfGenerator.Demo --configfile eng/NuGet.docker.Config -r linux-x64 --packages /consumer-packages --force \
    && dotnet publish PdfGenerator.Demo -c Release -r linux-x64 --self-contained false --no-restore -o /publish
RUN --mount=type=cache,target=/root/.nuget/packages --mount=type=cache,target=/root/.m2 --mount=type=cache,target=/ikvm-cache \
    export IkvmCacheDir=/ikvm-cache \
    && dotnet run --project eng/PdfGenerator.Documentation -c Release -- --check . \
    && dotnet publish eng/PdfGenerator.Benchmarks -c Release -r linux-x64 --self-contained false -o /benchmark

FROM mcr.microsoft.com/dotnet/runtime:8.0-bookworm-slim
WORKDIR /app
COPY --from=build /publish .
COPY --from=build /benchmark /benchmark
COPY test-data /app/test-data
USER $APP_UID
ENTRYPOINT ["dotnet", "PdfGenerator.Demo.dll"]
CMD ["--self-test", "--pdf"]
