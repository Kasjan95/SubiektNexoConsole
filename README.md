# SubiektNexoConnector

SubiektNexoConnector is a local REST adapter for InsERT nexo. It keeps the vendor-specific SDK and Sfera integration in .NET, while exposing a smaller HTTP contract for scripts, automation tools and applications that should not depend on the nexo object model directly.

The connector currently supports product, warehouse and party workflows. In addition to reads, it exposes selected write operations, nested party addresses and contacts, custom field values, custom field definitions and flags. Expensive product and party reads are served from a durable SQLite materialized read model maintained by background workers and write-through synchronization.

## Project Status

Pilot-stage integration software, not a production-qualified installer. The repository demonstrates both an HTTP boundary around a stateful vendor SDK and the Windows deployment lifecycle needed to operate it. Manual updates, repair and uninstall are implemented; live migration, failure recovery and unattended startup still require acceptance testing on supported Windows/nexo combinations.

## Key Capabilities

- Product listing, search, details, creation, partial updates and deletion.
- Warehouse listing and product stock details for a selected warehouse.
- Party listing, search, details, creation and partial updates.
- Address and contact management as nested party resources.
- Basic and advanced custom field values on products and parties.
- Discovery of basic field, advanced field, dictionary and flag definitions.
- Flag assignment and removal through product and party PATCH requests.
- Durable SQLite materialization of products, parties and advanced-field definitions.
- Policy-driven background refresh with product `hot`, `cold` and `new` workloads.
- Read-through refresh for individual resources and write-through synchronization after successful writes.
- Explicit freshness metadata on GET responses and deterministic `503` behavior before a collection is ready.
- API key authentication, RFC 7807-style problem responses and Swagger UI.
- Application and HTTP-level tests that do not require a live nexo database.
- WPF deployment wizard with external per-instance configuration, protected API-key export and Windows integration.
- Manual versioned updates, persisted operation state, repair and scoped uninstall.

## Why This Adapter Exists

InsERT nexo integrations are naturally built in C# through the nexo SDK and Sfera APIs. Many consumers, however, only need a stable local HTTP boundary and should not know how nexo sessions, business objects, locks, validation messages or custom field accessors work.

This project translates between those two worlds. Public DTOs stay independent of the vendor model, while the infrastructure layer owns session management, nexo mapping, object locking and persistence.

More detailed trade-offs are documented in Polish in [docs/decyzje-architektoniczne.md](docs/decyzje-architektoniczne.md).

## Engineering Focus

- **Vendor isolation:** HTTP DTOs and use cases do not expose nexo business objects.
- **Stateful SDK boundary:** serialized Sfera access, queue deadlines and explicit object-locking/persistence rules.
- **Materialized read model:** EF Core and SQLite provide fast, durable projections without becoming the business source of truth.
- **Operational lifecycle:** InsLauncher startup, password-logon scheduled tasks, scoped firewall rules and per-instance ACLs.
- **Failure visibility:** HTTP health is distinct from an authenticated Sfera test; updates also check the running binary's release metadata.
- **Test boundaries:** in-memory HTTP tests, deployment policy tests and opt-in Windows integration tests; live SDK acceptance is a separate responsibility.

## Current API Surface

### Products

| Method | Endpoint | Description |
| --- | --- | --- |
| `POST` | `/products` | Create a product, including advanced custom-field values. |
| `GET` | `/products` | List and search the complete materialized product collection with pagination. |
| `GET` | `/products/{sku}` | Get materialized product details, prices, stock, suppliers, flags and custom fields, with read-through refresh when required. |
| `PATCH` | `/products/{sku}` | Partially update product data, custom fields or flag assignment. |
| `DELETE` | `/products/{sku}` | Delete a product when nexo allows it. |

The product list accepts `search`, `page` and `pageSize` query parameters. Page numbering starts at `1`; invalid pagination values return `400`. The default and maximum page sizes are configured through `Api:Pagination`.

### Warehouses

| Method | Endpoint | Description |
| --- | --- | --- |
| `GET` | `/warehouses` | List warehouses. |
| `GET` | `/warehouses/{symbol}/products/{sku}` | Get stock information for a product in a selected warehouse. |

### Parties

| Method | Endpoint | Description |
| --- | --- | --- |
| `POST` | `/parties` | Create a party, optionally with addresses, contacts and advanced custom-field values. |
| `GET` | `/parties` | List and search the complete materialized party collection with filtering and pagination. |
| `GET` | `/parties/create-options` | Get party types and reference data required by create forms. |
| `GET` | `/parties/{partySignature}` | Get materialized party details, addresses, contacts, trade credit limits, flags and custom fields, with read-through refresh when required. |
| `PATCH` | `/parties/{partySignature}` | Partially update party data, custom fields or flag assignment. |

The party list accepts `customerStatus`, `type`, `search`, `page` and `pageSize` query parameters. Page numbering starts at `1`; invalid pagination values return `400`. The default and maximum page sizes are configured through `Api:Pagination`.

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

Custom field values are returned in product and party details. Product and party creation accepts `advancedFields`; PATCH accepts `basicFields` and `advancedFields`. A flag can be assigned, updated or removed through the PATCH `flag` property. Field definitions should be queried before constructing dynamic payloads because IDs, types, dictionaries and required fields differ between nexo installations.

See [docs/custom-fields.md](docs/custom-fields.md) for value formats and request examples.

## Materialized Read Model

Products, parties and advanced custom-field definitions are projected into a per-instance SQLite database. The database is a rebuildable read model, not an alternative source of business truth: nexo remains authoritative and every business write is performed through Sfera.

The API applies three consistency rules:

- Collection endpoints read only from SQLite. Until the first complete materialization cycle finishes, they return `503 Service Unavailable` instead of issuing an expensive full fallback query to nexo.
- Individual product and party endpoints use read-through behavior. Missing or stale data is refreshed from nexo, written to SQLite and then read back. If refresh fails but an older snapshot exists, the endpoint returns that snapshot as `stale`.
- Successful product and party writes update the read model on a best-effort basis. Nested party address and contact changes synchronize the complete returned party snapshot.

Successful GET responses expose materialization freshness through:

```text
Materialization-Status: fresh | refresh-due | stale | unavailable
Materialization-Age-Seconds: 42
Materialization-Last-Success-At: 2026-10-14T10:15:00.0000000Z
```

The SQLite schema is migrated automatically before the API starts accepting requests. Background services refresh data according to persisted policies and checkpoints while sharing the same serialized Sfera execution gate as HTTP requests.

See [docs/materialization.md](docs/materialization.md) for policy values, failure semantics, database locations and extension guidance.

## Business Write Idempotency

All controller `POST`, `PUT`, `PATCH` and `DELETE` endpoints automatically require an authenticated client and an
`Idempotency-Key` header (1–64 visible ASCII characters). Generate a key for each
new operation and reuse it for retries with the same method, URL and exact body
bytes. JSON whitespace changes also count as a different request. This includes
nested address/contact writes. Technical minimal endpoints, such as
`/maintenance/stop`, and reads are excluded. Middleware and Swagger use the same
`IdempotencyPolicy`; no per-action attribute is needed. New protected writes must
use `SferaExecutor` and finish within the HTTP request, not in background work.

Swagger UI exposes the header as optional and generates a random key when it is
left empty. The generated key appears in the curl command. Each execution with
an empty field gets a new key; enter a previous key explicitly to test a retry.
The backend still requires the header for protected writes.

- Before entering Sfera, the request is persisted as `Pending`; after connecting,
  `Executing` is persisted before the business callback can run.
- Success is buffered until `Completed` and the response are persisted. A retry
  replays the original status, body, content type and Location, with
  `Idempotency-Replayed: true`.
- A duplicate still in progress returns `409`, code
  `idempotency_operation_in_progress`, and `Retry-After: 5`. Reusing a key for a
  different request returns `409` with `idempotency_key_reused`.
- An exception after execution starts, or inability to persist its result,
  returns `409` with `idempotency_result_indeterminate`. The UI should tell the
  user to check the resource list for POST, the current resource data for PATCH/PUT,
  or whether the resource still exists for DELETE. Do not automatically retry with a new key:
  the Sfera write may already have succeeded.
- A failure before business execution releases the pending claim, allowing a
  same-key retry. At startup, interrupted `Executing` operations become
  `Indeterminate` and abandoned `Pending` claims are removed. A database-specific
  process lock prevents two adapter processes from using the same configured file.

SQLite and Sfera do not share an atomic transaction. Completed rows have a 72-hour
expiry timestamp, but automatic expiry cleanup is not implemented yet. With
`Auth:Mode=None`, protected write endpoints return `401`; enable API-key
authentication when testing them locally.

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
- `SubiektNexoConnector.Deployment` - WPF wizard, deployment state, InsERT packaging boundary and Windows lifecycle operations.
- `SubiektNexoConnector.Deployment.Tests` - packaging, configuration, update policy, failure handling and opt-in Windows tests.
- `tools/SubiektNexoConnector.Deployment.PayloadBuilder` - selects and archives the distributable adapter payload.
- `tools/SubiektNexoConnector.Deployment.UiProbe` - offline WPF rendering and read-only package contract checks.
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

The materialized read model path is configured independently from the idempotency store:

```json
"Materialization": {
  "DatabasePath": "data\\read-model.db"
}
```

Relative paths are resolved against the application base directory. The development configuration uses `data\read-model.dev.db`; a deployed instance uses the path from its external instance settings.

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

A timed-out request receives `503 Service Unavailable` with `Retry-After`. Structured logs distinguish queue wait, Sfera connection, SDK operation and handle disposal time, together with queue depth, operation name, execution origin and result. Materialization workers set `ExecutionOrigin=MaterializationWorker` and include the active policy ID.

## Running Locally

To use connection settings from `appsettings.json`, pass the `--config` flag:

```powershell
dotnet run --project src\SubiektNexoConnector.Api\SubiektNexoConnector.Api.csproj -- --config
```

Without `--config`, the connector uses the standard connection flow supplied by the nexo SDK.

The deployed API is started by InsLauncher with an explicit instance name:

```text
/UruchomionePrzezInsLauncher --instance nexoadapter-krakow-01
```

In this mode, the database connection is received through `DanePolaczenia.Odbierz()`, while
instance settings are loaded from:

```text
C:\ProgramData\SubiektNexoConnector\instances\nexoadapter-krakow-01\settings.json
```

The instance name may contain ASCII letters, digits, `-` and `_`. Local `appsettings.json`
and `appsettings.template.json` are excluded from publish output so that development
credentials cannot be included in the deployment package.

The instance configuration must explicitly define the HTTP endpoint and a non-default port:

```json
{
  "Urls": "http://0.0.0.0:15001"
}
```

Changing the port requires editing the instance `settings.json`, updating the Windows Firewall
rule and restarting the adapter through InsLauncher. It does not require publishing the adapter again.

The unauthenticated `GET /health` endpoint reports whether the HTTP process is ready. It does not
open a Sfera session or verify the connection to nexo, so it can be used by the installer and local monitoring.

## Building the Installer

From the repository root:

```powershell
.\scripts\Publish-Deployment.ps1
```

The script asks which version component to increment: **major release**, **new features**, **fixes** or **revision**. Lower components reset to zero. Confirming saves the four-part version in `Release.props`. Select **use current version** to retry a failed publish without another increment:

```powershell
.\scripts\Publish-Deployment.ps1 -VersionLevel Current
```

It runs tests, publishes the adapter, selects its runtime payload, extracts the official `PackageDeployer.dll` from the locally installed InsERT SDK and embeds both payloads in one framework-dependent Windows x64 executable:

```text
artifacts\deployment-release\SubiektNexoConnector.Setup.exe
```

Only this EXE is needed on the target computer. The adjacent `release.json` records build metadata and prevents overwriting the same/newer version in that output directory; `.sha256` allows file-integrity checks. They are not installer runtime dependencies.

The build machine needs the .NET SDK and `NEXO_SDK_PATH`. The client needs nexo, its applicable Sfera license, and the **.NET 8 Windows Desktop x64 runtime**, but not the SDK. Windows PowerShell 5.1 and PowerShell 7 are supported by the publish helpers. Git is optional for test builds; missing Git metadata is explicitly marked `nogit`. Production publishing requires a clean Git checkout and code-signing parameters; ordinary local publishing produces an unsigned test build.

The repository does not include vendor SDK binaries or customer configuration. Packaging rejects development `appsettings*.json` and selects dependencies from the published runtime metadata. Do not add vendor binaries or generated customer packages to source control.

## Installer Workflow

The wizard selects one instance and one nexo database, creates an MPKG and deploys it through the official InsERT component. The instance name determines the stable launch scenario, `--instance` argument and ProgramData configuration directory.

- Operator credentials and one API key are stored in protected external `settings.json`; an additional protected TXT export can be explicitly copied to the backend.
- Seq forwarding and the desktop shortcut are optional; local rolling logs remain available without Seq.
- The inbound TCP rule supports explicit backend IP/CIDR ranges and selected Windows network profiles. `LocalSubnet` is available for a controlled pilot LAN.
- The Windows account is selected through the native credential dialog. A full password-logon boot task uses that account's InsLauncher profile; a Windows Hello PIN is not a password.
- Administrative access is needed for firewall, task, ACL and machine-wide registration changes. The adapter account can read configuration and modify its log directory.
- Installation checks HTTP health and authenticated `GET /warehouses`, then tests startup through the scheduled task. Health alone does not prove Sfera access.
- An optional restart test records verification after boot. A SYSTEM verifier by itself does **not** prove the adapter was available before the first interactive login; that timing still needs independent acceptance verification.

Local logs are kept under:

```text
C:\ProgramData\SubiektNexoConnector\instances\<instance>\logs\adapter-YYYYMMDD.log
```

### Repair, update and uninstall

**Repair** reconciles the existing installation using its cached MPKG. **Update** uses a newer payload while retaining the original package family, instance, database, configuration and API key. A successful update must verify the running version and informational version, not just an open HTTP port. The target package and its hash are persisted for resuming an interrupted update. Downgrades and automatic rollback are not supported.

**Uninstall** targets the selected instance's local resources and nexo association; shared server package data is retained. Configuration/log deletion is a separate explicit action. Offline local removal is recorded as incomplete until the server association can be removed.

Legacy pilot import has known recovery and configuration-preservation gaps. Older adapters without maintenance endpoints may require manual process closure before updating. These are documented limitations, not a claim of seamless migration for every historical pilot.

### Security and release boundaries

This pilot serves HTTP inside a controlled LAN, not an Internet-facing HTTPS service. API keys and operator passwords in the protected configuration are **not encrypted at rest by the application**. An exported API-key TXT is a secret and must be handled as such. Windows account passwords are passed to Task Scheduler rather than written to connector files.

`GET /maintenance/info` requires an API key and reports configured instance/release metadata. Its company label is configuration, not independent proof of the live database identity. `POST /maintenance/stop` additionally requires a loopback connection.

See the [manual update and versioning guide](docs/manual-adapter-update.md), [production-readiness plan](docs/installer-production-readiness-plan.md) and [legacy migration review](docs/pilot-migration-review.md) for details and outstanding acceptance work. These documents are in Polish.

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

Deployment's Windows integration tests are opt-in, require an elevated PowerShell and previously built test binaries:

```powershell
.\scripts\Test-WindowsDeployment.ps1
```

They create temporary test resources in Windows; they are not run automatically by ordinary publish. Version-selection helper tests can be run separately with `scripts/Test-ReleaseVersion.ps1` and are also run by publish.

Some tests tagged `KnownMigrationDefect` characterize known incorrect behavior. A passing test count is therefore not a production-readiness certificate. Test live nexo deployment, two-instance isolation, password-logon startup and interruption recovery separately.

## Console Entry Point

The console project resolves the same infrastructure services and can be used for quick local checks:

```powershell
dotnet run --project src\SubiektNexoConnector.Console\SubiektNexoConnector.Console.csproj -- --config
```

It currently resolves warehouses and prints their symbols and names.

## Current Limitations

- The connector is Windows-only because the nexo SDK and Sfera runtime are Windows-specific.
- Materialization currently covers products, parties and advanced custom-field definitions. Warehouses, basic-field definitions and flag definitions still use live Sfera reads.
- Materialization policies are persisted in SQLite, but their available definitions and matching fields are still initialized from code; there is no policy-management API.
- SQLite and nexo do not share a transaction. A successful nexo write is authoritative even if the best-effort read-model synchronization fails; the background worker repairs the projection later.
- Workers and HTTP requests share the same serialized Sfera executor. There is no long-lived Sfera session or priority queue yet, so opening and disposing SDK handles remains a significant part of refresh cost.
- Repository operations are synchronous because they wrap synchronous vendor APIs.
- The public surface intentionally covers selected integration scenarios rather than the entire nexo model.
- The pilot boot task starts InsLauncher and requests Task Scheduler retries when its action reports failure.
  Whether a particular InsLauncher version remains attached to the adapter process after launch must still
  be verified during the crash/restart scenario; backend health monitoring remains the reliable outage signal.

## Roadmap

- Materialized document resources, beginning with document headers and their positions.
- A serialized priority queue that can reuse one Sfera session for multiple operations and yield to interactive API traffic.
- A policy configurator for refresh intervals, candidate conditions, priorities and batch sizes.
- Materialization of remaining reference data where measurements justify it.
- An AI-facing, read-only API layer with allowlisted, anonymized GET representations and a dedicated API-key scope restricted to that resource group. The scope is not treated as protection from a local process that can bypass the API: an AI agent must run under a separate, non-administrative operating-system account with no access to the nexo database, adapter credentials or materialized SQLite files. Running it on a separate physical or virtual machine is the preferred deployment boundary.
- Broader document-related resources, exports and asynchronous integration flows.
- More infrastructure-level verification against a controlled nexo test environment.
- Improved observability for materialization lag, queue pressure, locks and validation failures.

## License

Copyright (c) 2026 Kamil Kasjaniuk. All rights reserved.

This is proprietary software. No permission is granted to use, copy, modify,
distribute, or create derivative works without the copyright holder's prior
express written permission. See [LICENSE](LICENSE) for the complete terms.

Licensing inquiries: [github.com/Kasjan95](https://github.com/Kasjan95).
