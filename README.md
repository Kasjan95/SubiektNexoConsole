# SubiektNexoConnector

Local REST adapter for InsERT nexo. The project exposes selected nexo resources through an ASP.NET Core API so they can be consumed from tools and integrations that do not use the nexo SDK directly.

The connector is in active development. The current scope focuses on read scenarios around products, warehouses, stock and price data.

## Purpose

InsERT nexo integrations are naturally built in .NET/C# through the nexo SDK and Sfera APIs. This project keeps that integration layer in C#, then exposes a small local HTTP API for other runtimes, scripts, automation tools or local AI-assisted workflows.

## Architecture Notes

Architectural decisions, domain trade-offs and future directions are documented in Polish here:

- [docs/decyzje-architektoniczne.md](docs/decyzje-architektoniczne.md)

The document also includes a short English summary.

## Current API Surface

- `GET /products`
- `GET /products/{sku}`
- `GET /warehouses`
- `GET /warehouses/{symbol}/products/{sku}`

## Solution Structure

- `SubiektNexoConnector.Api` - ASP.NET Core REST API and Swagger UI.
- `SubiektNexoConnector.Core` - application handlers, repository interfaces and DTOs.
- `SubiektNexoConnector.Infrastructure` - nexo SDK/Sfera integration and configuration binding.
- `SubiektNexoConnector.Console` - additional local entry point for quick checks and a potential starting point for a future worker or message-driven process.

## Requirements

- Windows
- .NET 8 SDK
- InsERT nexo installed locally
- InsERT nexo SDK version compatible with the target nexo database

SDK packages are available from:

https://ftp.insertcdn.pl/pub/aktualizacje/InsERT_nexo/

## SDK Path

The projects read the nexo SDK path from the `NEXO_SDK_PATH` environment variable. Set it to the SDK root directory, not to the `Bin` directory.

Example:

```powershell
setx NEXO_SDK_PATH "C:\path\to\nexoSDK_59.1.1.9137"
```

After using `setx`, restart PowerShell or Visual Studio so the new environment variable is visible to build tools.

For a single build, you can also pass the path directly:

```powershell
dotnet build -p:NexoSdkPath="C:\path\to\nexoSDK_59.1.1.9137"
```

## Configuration

Copy the template configuration and fill in local nexo connection values:

```powershell
Copy-Item src\SubiektNexoConnector.Api\appsettings.template.json src\SubiektNexoConnector.Api\appsettings.json
```

`appsettings.json` is ignored by Git and should stay local, because it may contain database names and credentials.

The API uses API key authentication by default. Keep the key out of committed files and provide it with an environment variable:

```powershell
$env:SUBIEKT_NEXO_CONNECTOR_API_KEY = "replace-with-a-local-secret"
```

Requests must include the key in the configured header:

```http
X-Api-Key: replace-with-a-local-secret
```

For local development only, API authentication can be disabled with:

```json
"Auth": {
  "Mode": "None"
}
```

The template configuration also includes Serilog setup with console output and a Seq sink at `http://localhost:5341`.

## Running Locally

To run the API with connection settings from `appsettings.json`, pass the `--config` flag:

```powershell
dotnet run --project src\SubiektNexoConnector.Api\SubiektNexoConnector.Api.csproj -- --config
```

Swagger UI is available at:

```text
https://localhost:7214/swagger
```

or, depending on the selected launch profile:

```text
http://localhost:5151/swagger
```

Without `--config`, the connector uses the standard nexo SDK-provided connection flow.

## Console Entry Point

The console project is a small local entry point for quick checks against the same infrastructure layer:

```powershell
dotnet run --project src\SubiektNexoConnector.Console\SubiektNexoConnector.Console.csproj -- --config
```

At the moment it resolves warehouses and prints their symbols and names.

## Publishing And Installation

Running the API from source is intended for local development. Installing a packaged nexo solution requires the tooling and launcher integration described in the InsERT nexo SDK documentation. The published output must be prepared with the SDK tooling and added to the nexo launcher files according to that documentation.

## Roadmap

- Cache for expensive read scenarios and slower SDK-backed endpoints.
- Separate worker for refresh jobs, exports and future asynchronous flows.
- More resources and eventual write-oriented scenarios beyond the current `GET` endpoints.
- Better API shaping through filtering, pagination and lighter response models.
- Deeper observability around integration flow, including `live` vs `cache` responses.
- Concurrency checks around nexo/Sfera session usage if the SDK requires stricter serialization.
