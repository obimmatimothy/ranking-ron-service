# ranking-ron-service

RankingRon is a .NET 10 console application that ranks makelaars (real estate agents) by the number of properties they have listed for sale on [funda.nl](https://www.funda.nl). It produces two tables:

1. Top 10 makelaars in Amsterdam: all listings for sale (`type=koop`, `zo=/amsterdam`)
2. Top 10 makelaars in Amsterdam for listings with a garden (`type=koop`, `zo=/amsterdam/tuin`)

## Contents

- [How to run](#how-to-run)
- [Sample output](#sample-output)
- [Architecture](#architecture)
- [How it works](#how-it-works)
- [Fewer API calls](#fewer-api-calls)
- [Rate limiting approach](#rate-limiting-approach)
- [The 401 discovery](#the-401-discovery)
- [Error handling](#error-handling)
- [Handling ties](#handling-ties)
- [Logger output vs console output](#logger-output-vs-console-output)
- [Configuration](#configuration)
- [Testing](#testing)
- [AI use](#ai-use)

---

## How to run

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A Funda partner API key

### Run

The API key is not stored in the repository. `appsettings.json` only contains a placeholder. Supply the key with an environment variable or replace the value in `appSettings.json`:

You can also open `ranking-ron-service.sln` in Visual Studio or your preferred IDE, set the `Funda__ApiKey` environment variable in the launch profile, and run `RankingRon.Application`.

> A full run takes some time. The client stays below the limit of 100 requests per minute on purpose (see [Rate limiting approach](#rate-limiting-approach)).

---

## Sample output

```
Top 10 makelaars in Amsterdam

| #   | Makelaar     | Listings |
|-----|--------------|----------|
| 1   | Makelaar A   |      120 |
| 2   | Makelaar B   |       98 |
| ... | ...          |      ... |

Top 10 makelaars in Amsterdam (with tuin)

| #   | Makelaar     | Listings |
|-----|--------------|----------|
| 1   | Makelaar C   |       40 |
| ... | ...          |      ... |
```

---

## Architecture

The solution uses a **Ports & Adapters (hexagonal)** architecture. The main aim is to achieve a clean separation of concern by isolating the core (or domain) part of the application from external inferences and creating loosely coupled components that can be easily exchanged with little to no effect on other parts of the application.
 
Take RankingRon as an example...the current presentation layer is a console application but if its decide to change to/add an API, the domain/adapters do not require any changes

```
┌───────────────────────────────┐
│  RankingRon.Application       │  Composition root + console presentation
│  Program, Application         │
└──────────────┬────────────────┘
               │ IMakelaarUseCase
┌──────────────▼────────────────┐
│  RankingRon (domain)          │  Use cases, models, ports
│  MakelaarUseCase              │
└──────────────┬────────────────┘
               │ IListingStore (port)
┌──────────────▼────────────────┐
│  RankingRon.Adapters.Funda    │  HTTP, paging, rate limiting,
│  FundaListingStore            │  retries, DTO → domain mapping
└───────────────────────────────┘
```

| Project | Responsibility |
|---|---|
| `src/RankingRon` | Domain core. Contains `MakelaarUseCase` (grouping, counting, ordering) and the ports `IListingStore` and `IMakelaarUseCase`. |
| `src/Adapters/RankingRon.Adapters.Funda` | Funda adapter. `FundaListingStore` implements `IListingStore`. It calls the Funda partner API, fetches every page, and maps the DTOs to domain models. This project also contains `RateLimitingHandler` and the retry policy |
| `src/RankingRon.Application` | Entry point. Sets up dependency injection (`Program.cs`), defines the rankings to produce, and prints them as tables (`Application.cs`). |
| `test/RankingRon.Tests` | Unit tests for the domain. |
| `test/RankingRon.Adapters.Funda.Tests` | Unit tests for the Funda adapter. |

Each layer registers its own services through a `Bootstrapper` extension method. This keeps `Program.cs` small, and each project owns its own wiring.
Of course since we dont want to expose components beyond their boundaries, besides the domain project, all components of the adapters are limited to an `internal` visibility

Benefits:

- **Separation of concerns.** Ranking logic, data access, and presentation each live in their own project.
- **Testability.** The use case can be tested with a mocked `IListingStore`, and the adapter can be tested with a mocked `HttpMessageHandler`.
- **Replaceability.** Another data source or front end (for example, a Web API) can be added without changing the domain.

---

## How it works

1. `Application` loops over the configured rankings. Each ranking is described by a `SearchCriteria` (`City`, `ListingType`, and optional `AdditionalFilters` such as `tuin`).
2. `MakelaarUseCase.GetTopMakelaars` asks the `IListingStore` port for all listing/makelaar pairs that match the criteria.
3. `FundaListingStore` converts the criteria into a Funda query:
   - `type` is the lowercase `ListingType` (`koop`).
   - `zo` is `/{city}/{filter1}/...`, for example `/amsterdam/tuin`.
   - It calls the JSON endpoint: `/feeds/Aanbod.svc/json/{key}/?type=...&zo=...&page=N&pagesize=100`.
   - It reads `Paging.AantalPaginas` (total pages) from each response and continues until every page has been fetched.
4. The use case groups the results by `MakelaarId`, counts the listings per makelaar, sorts them, takes the top 10, and assigns positions.
5. `Application` prints each result as a table.

Grouping uses `MakelaarId` and not the name. IDs are stable. Names are used only for display and for breaking ties.

---

## Fewer API calls

The number of requests is the main cost, because the API allows only about 100 requests per minute. To keep that number low:

- **`pagesize=100`.** The default page size I was given in the api documentation is 25. I noticed I could increase this number so I just went with 100. Requesting 100 objects per page reduces the number of requests by roughly a factor of four. In the end, this number should be adjusted toas high as possible that is acceptable by the Funda api team.
- **Stop at the last page.** The total page count comes from the response (`Paging.AantalPaginas`), so the loop never requests empty pages.
- **Sequential paging.** Pages are fetched one after another. The rate limit caps throughput anyway, so parallel requests would add complexity without a real speed gain.

---

## Rate limiting approach

The goal is to **avoid** rejected requests in the first place and to **recover** if one happens anyway.

### Prevention: client-side sliding-window rate limiter

`RateLimitingHandler` is a `DelegatingHandler` in the `HttpClient` pipeline. It uses `System.Threading.RateLimiting.SlidingWindowRateLimiter`:

- `PermitLimit = 99` requests per `Window = 1 minute`. This stays just below the 100-request limit.
- `SegmentsPerWindow = 6`. The window moves forward every 10 seconds. A fixed window can allow up to twice the limit around the window boundary, and a sliding window prevents this.
- Extra requests are **queued** (`QueueLimit = 50`, oldest first) and are not failed. The paging loop simply waits for a permit.
- If the queue is full, the handler returns a local `429 Too Many Requests` and does not call the API.

The limiter is registered as a **singleton**, so every request in the process shares the same budget.

### Recovery: retries with exponential back-off

A resilience handler (`Microsoft.Extensions.Http.Resilience`, built on Polly) adds a retry strategy:

- It retries transient failures: 5xx, 408, 429, network errors and timeouts.
- It also retries `401 Unauthorized` (see [The 401 discovery](#the-401-discovery)).
- It uses exponential back-off with jitter, starting at `RetryDelayInMilliseconds`, for up to `RetryCount` attempts.
- Each retry is logged as a warning.

Pipeline order, from outer to inner: **Retry → Rate limiter → HTTP**. The rate limiter is inside the retry handler, so retries also use permits and cannot push the client over the limit.

---

## The 401 discovery

The assignment says the API rejects requests above about 100 per minute. I expected a `429 Too Many Requests`. When I tested this on purpose, the Funda API returned **`401 Unauthorized`** instead.

The standard transient-error check does not treat 401 as transient, so without a change these responses would fail immediately. The retry predicate therefore treats a 401 as retryable:

```csharp
ShouldHandle = args => ValueTask.FromResult(
    HttpClientResiliencePredicates.IsTransient(args.Outcome) ||
    args.Outcome.Result?.StatusCode == HttpStatusCode.Unauthorized)
```

Trade-off: a truly invalid API key is also retried a few times before it fails. This costs a few extra seconds, which is acceptable here. The rate limiter should make the throttling 401 rare, so this retry is a safety net.

---

## Error handling

- When paging still fails after all retries, `FundaListingStore` logs the error and throws the domain exception `UnableToRetrieveDataException`. Callers do not depend on HTTP-specific exceptions.
- `Application` catches errors per ranking. If one ranking fails, it prints a clear message and the next ranking still runs.
- Constructors use guard clauses (`ArgumentNullException`). Missing `Funda` configuration fails fast at startup with a clear message.

---

## Handling ties

Makelaars with the same number of listings are sorted **alphabetically by name**:

```csharp
.OrderByDescending(rm => rm.ListingCount)
.ThenBy(rm => rm.MakelaarName)
```

This makes the output deterministic. Running the program twice on the same data gives the same table, and the tests can assert an exact order. Positions are numbered 1 to 10 in this order. Tied makelaars do not share a position, which keeps the table at exactly 10 rows.

---

## Logger output vs console output

The application uses two separate output channels on purpose:

| Channel | Used for | Audience |
|---|---|---|
| `Console.WriteLine` | Result tables and short, user-friendly error messages (`Application`) | The person running the program |
| `ILogger` | Diagnostics: retries, rate-limit warnings, exceptions with stack traces | Developers and operators |

Reasons:

- The result is the product. It must look the same no matter how logging is configured.
- Logs can be filtered, redirected, or sent to another sink (for example, Application Insights or Seq) without changing the presentation.
- In `appsettings.json`, `System.Net.Http.HttpClient` and `Polly` are set to `Warning` so request noise does not hide the tables. Retries and failures are still visible.

Only the presentation layer (`Application`) writes to the console. The domain and adapter layers use only `ILogger`.

---

## Configuration

Configuration comes from `src/RankingRon.Application/appsettings.json`, environment variables, and command-line arguments (standard .NET Generic Host).

```json
{
  "Funda": {
    "ApiUrl": "http://partnerapi.funda.nl",
    "ApiKey": "<your-api-key>",
    "TimeoutInMilliseconds": 10000,
    "RetryCount": 3,
    "RetryDelayInMilliseconds": 1000,
    "RateLimiter": {
      "PermitLimit": 99,
      "Window": "00:01:00",
      "SegmentsPerWindow": 6,
      "QueueLimit": 50
    }
  }
}
```

| Key | Default | Description |
|---|---|---|
| `Funda:ApiUrl` | – | Base URL of the Funda partner API (required) |
| `Funda:ApiKey` | – | Funda API key (required; keep it out of source control) |
| `Funda:TimeoutInMilliseconds` | `10000` | `HttpClient` timeout |
| `Funda:RetryCount` | `3` | Maximum number of retry attempts |
| `Funda:RetryDelayInMilliseconds` | `1000` | Base delay for exponential back-off |
| `Funda:RateLimiter:PermitLimit` | `99` | Requests allowed per window |
| `Funda:RateLimiter:Window` | `00:01:00` | Length of the sliding window |
| `Funda:RateLimiter:SegmentsPerWindow` | `6` | Number of segments in the window |
| `Funda:RateLimiter:QueueLimit` | `50` | Requests that can wait for a permit |

---

## Testing
The unit tests can be run from your IDE or by running the command:
```powershell
dotnet test
```

Adapter types are `internal`. They are exposed to the test project (and to Moq's `DynamicProxyGenAssembly2`) with `InternalsVisibleTo`.

---

## AI use

- GitHub Copilot was used as an assistant for:
  - Creating boilerplate code and more specifically, the client-side rate limiter that I implemented. Had a rough idea of where I wanted to go but could find an elegat solution out of the box so I used some help here
  - Drafting unit tests based on my scenarion using the format **`GivenABC_WhenPerformingXYZ_ShouldReturn123`**
  - Drafting this README
- Intellisense was used for code completion