# Hacker News Best Stories API

## API

`GET /api/stories/best?n={positive integer}`

- **200 OK**: JSON array ordered by descending score (`title`, `uri`, `postedBy`, `time`, `score`, `commentCount`). Missing/deleted/dead items are skipped.
- **400 Bad Request**: Invalid `n` (`S101`), missing `n` (`S102`).
- **503 Service Unavailable**: Hacker News upstream failure or rate-limit exhaustion (`S201`).
- **500 Internal Server Error**: Unexpected errors (`S500`).

---

## Run locally

Prerequisite: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet restore HackerNews.sln
dotnet run --project HackerNews/HackerNews.csproj
```

- **Swagger UI**: [http://localhost:5110/swagger](http://localhost:5110/swagger)
- **Sample Request**: [http://localhost:5110/api/stories/best?n=5](http://localhost:5110/api/stories/best?n=5)
- **Health Check**: [http://localhost:5110/health](http://localhost:5110/health)

## Run with Docker Compose

```powershell
docker compose up --build
```

The API listens at `http://localhost:8080/swagger`.

---

## Design & Architecture

- **Clean Architecture & Roslyn Analyzer**: Strict layer boundaries enforced at compile time via custom analyzer `HackerNews.Analyzer` (`HN0001`).
- **Zero-Allocation Memoization**: `StoryMapper` uses `ConditionalWeakTable<HackerNewsItem, StoryResponse>`. ISO 8601 timestamps (`yyyy-MM-ddTHH:mm:sszzz`) and DTOs are instantiated strictly once per item lifetime, eliminating GC allocations during high-throughput reads.
- **Two-Tier Caching & Single-Flight**:
  - ASP.NET Core `OutputCache` caches HTTP responses per `n`.
  - In-memory `MemoryWithSemaphoreCache` (5-minute TTL) uses per-key semaphores to eliminate cache stampedes on upstream calls.
- **Upstream Resilience**:
  - Process-local Token Bucket rate limiter (30 req/s, burst 30, max wait 5s).
  - Outbound fan-out bounded to 20 concurrent operations.
  - Polly v8 resilience pipeline (retries with exponential backoff and jitter, circuit breaker).
- **Background Warmer**: `HackerNewsCacheWarmer` periodically refreshes top-200 stories in the background to ensure warm responses.

---

## Tests and CI

```powershell
dotnet test HackerNews.sln
```

45 automated tests execute in ~2 seconds without external dependencies (no Docker required):
- **Concurrency & Resilience**: Single-flight locks under concurrent misses (`Task.WhenAll`), rate-limiter permits, and retry pipelines.
- **Zero-Allocation Invariants**: Validates reference equality (`ReferenceEquals`) and string pointer reuse.
- **BDD Feature Tests**: Gherkin specifications executed via Reqnroll.
- **Endpoint Contracts**: Validates Minimal API status codes, error models (`S101`, `S102`, `S201`, `S500`), and exception handlers.

---

## Load-Test Snapshot

Observed run with Apache JMeter (hot response cache):

| Metric | Observed |
| --- | ---: |
| Samples | 1,026,892 |
| Throughput | **34,165.96 requests/sec** |
| Average Latency | **2 ms** |
| 95th Percentile | 5 ms |
| 99th Percentile | 20 ms |
| Errors | 0.000% |
| Response Size | 2,324.8 bytes |

*Note: Measures in-process output-cache performance; upstream latency and cold cache generation are bounded separately by the rate limiter.*

---

## Assumptions & Trade-Offs

- `beststories.json` supplies candidate IDs; all candidate details are fetched to guarantee accurate score ordering before taking `n`.
- Single-process architecture: Rate limiting and caching are process-local. Horizontal multi-replica deployments would share upstream budgets via distributed stores (e.g., Redis).
