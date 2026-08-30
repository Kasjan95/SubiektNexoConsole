# SubiektNexoConnector

SubiektNexoConnector is a local REST adapter for InsERT nexo. It keeps the vendor-specific SDK and Sfera integration in .NET, while exposing a smaller HTTP contract for scripts, automation tools and applications that should not depend on the nexo object model directly.

The connector currently supports product, warehouse and party workflows. In addition to reads, it exposes selected write operations, nested party addresses and contacts, custom field values, custom field definitions and flags.

## Key Capabilities

- Product listing, search, details, creation, partial updates and deletion.
- Warehouse listing and product stock details for a selected warehouse.
- Party listing, search, details, creation and partial updates.
- Address and contact management as nested party resources.
- Basic and advanced custom field values on products and parties.
- Discovery of basic field, advanced field, dictionary and flag definitions.
- Flag assignment and removal through product and party PATCH requests.
- API key authentication, RFC 7807-style problem responses and Swagger UI.
- Application and HTTP-level tests that do not require a live nexo database.

## Why This Adapter Exists

InsERT nexo integrations are naturally built in C# through the nexo SDK and Sfera APIs. Many consumers, however, only need a stable local HTTP boundary and should not know how nexo sessions, business objects, locks, validation messages or custom field accessors work.

This project translates between those two worlds. Public DTOs stay independent of the vendor model, while the infrastructure layer owns session management, nexo mapping, object locking and persistence.

More detailed trade-offs are documented in Polish in [docs/decyzje-architektoniczne.md](docs/decyzje-architektoniczne.md).

## Current API Surface

### Products

| Method | Endpoint | Description |
| --- | --- | --- |
| `POST` | `/products` | Create a product. |
| `GET` | `/products` | List and search products with pagination. |
| `GET` | `/products/{sku}` | Get product details, prices, stock, suppliers, flags and custom fields. |
| `PATCH` | `/products/{sku}` | Partially update product data, custom fields or flag assignment. |
| `DELETE` | `/products/{sku}` | Delete a product when nexo allows it. |

The product list accepts `search`, `page` and `pageSize` query parameters.

### Warehouses

| Method | Endpoint | Description |
| --- | --- | --- |
| `GET` | `/warehouses` | List warehouses. |
| `GET` | `/warehouses/{symbol}/products/{sku}` | Get stock information for a product in a selected warehouse. |

### Parties

| Method | Endpoint | Description |
| --- | --- | --- |
| `POST` | `/parties` | Create a party, optionally with addresses and contacts. |
| `GET` | `/parties` | List and search parties with filtering and pagination. |
| `GET` | `/parties/create-options` | Get party types and reference data required by create forms. |
| `GET` | `/parties/{partySignature}` | Get party details, addresses, contacts, trade credit limits, flags and custom fields. |
| `PATCH` | `/parties/{partySignature}` | Partially update party data, custom fields or flag assignment. |

The party list accepts `customerStatus`, `type`, `search`, `page` and `pageSize` query parameters.

### Party Addresses

| Method | Endpoint | Description |
| --- | --- | --- |
| `POST` | `/parties/{partySignature}/addresses` | Add an address or update the primary address. |
| `PATCH` | `/parties/{partySignature}/addresses/{addressId}` | Partially update an address. |
| `DELETE` | `/parties/{partySignature}/addresses/{addressId}` | Delete an address when nexo allows it. |

### Party Contacts

| Method | Endpoint | Description |
| --- | --- | --- |
| `POST` | `/parties/{partySignature}/contacts` | Add a contact. |
| `PATCH` | `/parties/{partySignature}/contacts/{contactId}` | Partially update a contact. |
| `DELETE` | `/parties/{partySignature}/contacts/{contactId}` | Delete a contact. |

### Custom Field and Flag Definitions

| Method | Endpoint | Description |
| --- | --- | --- |
| `GET` | `/Additional-field-definitions/basic?target={target}` | Get basic custom field definitions for `product` or `party`. |
| `GET` | `/Additional-field-definitions/advanced?target={target}` | Get advanced field definitions, groups and supported dictionary values. |
| `GET` | `/Additional-field-definitions/flags` | Get flag definitions grouped by domain. An optional `domain` query parameter narrows the result. |

Custom field values are returned in product and party details. They can be updated by sending `basicFields` or `advancedFields` in the corresponding PATCH request. A flag can be assigned, updated or removed through the `flag` property.

## PATCH Semantics

PATCH request DTOs distinguish an omitted property from an explicitly supplied `null` value:

- omitted property: keep the current value,
- non-null value: update the field,
- explicit `null`: clear a nullable field,
- empty PATCH body: return `400 Bad Request`.

Text values and identifier collections are normalized and validated before the infrastructure repository is called. Errors are returned as JSON problem details.

## Solution Structure

- `SubiektNexoConnector.Api` - ASP.NET Core controllers, authentication, error handling and Swagger configuration.
- `SubiektNexoConnector.Core` - use-case handlers, repository interfaces, commands, queries and public DTOs.
- `SubiektNexoConnector.Infrastructure` - nexo SDK/Sfera integration, mapping, locking, persistence and configuration binding.
- `SubiektNexoConnector.Console` - a small local entry point for quick SDK-backed checks.
- `SubiektNexoConnector.Core.Tests` - application-layer unit tests.
- `SubiektNexoConnector.Api.Tests` - controller and HTTP contract tests based on `WebApplicationFactory` and substituted repositories.

## Requirements

- Windows
- .NET 8 SDK or newer
- InsERT nexo installed locally
- InsERT nexo SDK compatible with the target nexo database

SDK packages are available from [the official InsERT nexo download directory](https://ftp.insertcdn.pl/pub/aktualizacje/InsERT_nexo/).

## SDK Path

The projects read the nexo SDK path from the `NEXO_SDK_PATH` environment variable. Set it to the SDK root directory, not to its `Bin` directory.

```powershell
setx NEXO_SDK_PATH "C:\path\to\nexoSDK"
```

After using `setx`, restart PowerShell or Visual Studio so the variable is available to build tools.

For a single build, the path can also be supplied directly:

```powershell
dotnet build -p:NexoSdkPath="C:\path\to\nexoSDK"
```

## Configuration

Copy the template configuration and fill in local nexo connection values:

```powershell
Copy-Item src\SubiektNexoConnector.Api\appsettings.template.json src\SubiektNexoConnector.Api\appsettings.json
```

`appsettings.json` is ignored by Git and should stay local because it may contain database names and credentials.

The API uses API key authentication by default. Keep the key out of committed files and provide it through an environment variable:

```powershell
$env:SUBIEKT_NEXO_CONNECTOR_API_KEY = "replace-with-a-local-secret"
```

Requests must include the key in the configured header:

```http
X-Api-Key: replace-with-a-local-secret
```

For local development only, authentication can be disabled with:

```json
"Auth": {
  "Mode": "None"
}
```

The template also configures Serilog console logging and an optional Seq sink at `http://localhost:5341`.

Set `Observability:AdapterInstance` and `Observability:NexoCompany` uniquely for every deployed adapter. Every Seq event is enriched with `Service`, `Environment`, `AdapterInstance`, `NexoCompany`, and `MachineName`.

Every response includes `X-Correlation-Id`. A client may provide a UUID in that request header to preserve one identifier across its backend and this adapter; otherwise the adapter generates one. The identifier is also added to request logs, Sfera logs, and Problem Details responses.

### Sfera concurrency

The connector serializes SDK access inside one API process. The optional `Nexo:SferaExecution` configuration controls how long a request can wait in that queue and the suggested retry interval:

```json
"SferaExecution": {
  "QueueTimeoutSeconds": 30,
  "RetryAfterSeconds": 5
}
```

A timed-out request receives `503 Service Unavailable` with `Retry-After`. Structured logs include the queue wait, Sfera execution time, queue depth, operation name and result.

## Running Locally

To use connection settings from `appsettings.json`, pass the `--config` flag:

```powershell
dotnet run --project src\SubiektNexoConnector.Api\SubiektNexoConnector.Api.csproj -- --config
```

Without `--config`, the connector uses the standard connection flow supplied by the nexo SDK.

In the Development environment, Swagger UI is available at one of the URLs configured by the selected launch profile, for example:

```text
https://localhost:7214/swagger
http://localhost:5151/swagger
```

## Testing

Run application-layer tests without a live nexo database:

```powershell
dotnet test tests\SubiektNexoConnector.Core.Tests\SubiektNexoConnector.Core.Tests.csproj
```

With `NEXO_SDK_PATH` configured, run the complete solution test suite:

```powershell
dotnet test SubiektNexoConnector.sln
```

The API tests start the ASP.NET Core pipeline in memory and replace infrastructure repositories with test doubles. They verify routing, serialization, status codes, validation and problem responses without modifying nexo data.

## Console Entry Point

The console project resolves the same infrastructure services and can be used for quick local checks:

```powershell
dotnet run --project src\SubiektNexoConnector.Console\SubiektNexoConnector.Console.csproj -- --config
```

It currently resolves warehouses and prints their symbols and names.

## Current Limitations

- The connector is Windows-only because the nexo SDK and Sfera runtime are Windows-specific.
- Requests currently use live SDK sessions; there is no cache layer yet.
- Repository operations are synchronous because they wrap synchronous vendor APIs.
- The public surface intentionally covers selected integration scenarios rather than the entire nexo model.
- Packaging and launcher integration still require the tooling described in the InsERT nexo SDK documentation.

## Roadmap

- Cache for expensive read scenarios and slower SDK-backed endpoints.
- A worker for refresh jobs, exports and asynchronous integration flows.
- Broader reference-data and document-related resources.
- More infrastructure-level verification against a controlled nexo test environment.
- Improved observability for live SDK calls, locks and validation failures.
