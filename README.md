# Principium

**Adaptive Parallel Processing Engine with Intelligent Deduplication, Caching, and Last-Write-Wins Semantics**

Principium is a high-performance .NET library that automatically selects the optimal execution strategy for processing large collections. It analyzes duplicate ratios, applies intelligent caching, and performs parallel execution when beneficial.

## Features

* Adaptive execution planning
* Parallel processing
* Intelligent duplicate detection
* Fingerprint-based cache validation
* Last-Write-Wins (LWW) correctness
* Configurable TTL cache
* Sharded LRU cache implementation
* Thread-safe architecture
* Generic API
* Zero external dependencies

---

# Installation

Add the Principium source code to your project or build it as a separate library.

Supported platforms:

* .NET 6+
* .NET 7+
* .NET 8+
* .NET 9+

---

# Quick Start

```csharp
using Principium;

var data = Enumerable.Range(1, 10000);

var results = Paralleling.ForEach(
    source: data,
    keySelector: x => x,
    payloadSelector: x => x,
    work: x =>
    {
        Thread.Sleep(10);
        return x * x;
    });

Console.WriteLine(results[100]);
```

---

# Basic Example

```csharp
using Principium;

record User(int Id, string Name);

var users = new[]
{
    new User(1, "John"),
    new User(2, "Kate"),
    new User(3, "Mike")
};

var results = Paralleling.ForEach(
    users,
    keySelector: u => u.Id,
    payloadSelector: u => u.Name,
    work: name => name.ToUpperInvariant());

foreach (var item in results)
{
    Console.WriteLine($"{item.Key}: {item.Value}");
}
```

---

# Duplicate Processing Example

```csharp
using Principium;

record Order(int Id, decimal Amount);

var orders = new[]
{
    new Order(1, 100),
    new Order(1, 150),
    new Order(1, 200),
    new Order(2, 300)
};

var results = Paralleling.ForEach(
    orders,
    keySelector: o => o.Id,
    payloadSelector: o => o.Amount,
    work: amount =>
    {
        Console.WriteLine($"Processing {amount}");
        return amount * 1.2m;
    });

Console.WriteLine(results[1]);
```

With default settings, Principium preserves Last-Write-Wins semantics.

---

# Custom Configuration

```csharp
var options = new PrincipiumOptions
{
    SampleSize = 2048,
    LowDupThreshold = 0.05,
    HighDupThreshold = 0.80,
    RequireLww = true,
    Ttl = TimeSpan.FromMinutes(30),
    CacheCapacity = 50000,
    HammingThreshold = 0
};

var result = Paralleling.ForEach(
    data,
    keySelector: x => x.Id,
    payloadSelector: x => x.Payload,
    work: Process,
    options: options);
```

---

# Adaptive Engine Reuse

```csharp
var result = Paralleling.ForEach(
    source,
    keySelector: x => x.Id,
    payloadSelector: x => x.Payload,
    work: Process,
    adaptiveKey: "OrdersPipeline");
```

Using the same adaptive key allows Principium to reuse internal engine instances and caches across executions.

---

# Execution Modes

Principium automatically chooses one of three execution plans:

### ParallelOnly

Used when duplicate ratio is very low.

* Maximum throughput
* Full parallel execution
* No cache lookups

### CacheOnly

Used when duplicate ratio is moderate.

* Cache-first processing
* Sequential LWW-safe execution

### CoalesceAndCache

Used when duplicate ratio is high.

* Duplicate collapsing
* Cache reuse
* Parallel computation of unique items
* Maximum efficiency

---

# Licensing

## Free Usage

Principium may be used free of charge for:

* Personal projects
* Educational projects
* Research
* Evaluation and testing
* Proof-of-concept implementations
* Non-commercial open-source projects

## Commercial Usage

Commercial use requires a paid license.

License fee:

**1,000 USD equivalent in USDT per year**

Payment wallet:

USDT (TRC20)

TNSGpeVzNJcEA6MyXP9PmgmFaZk5zaascV

The transaction receipt (payment confirmation) serves as proof of license ownership.

The license period starts from the transaction date recorded on the blockchain.

---

# Support

Telegram:

@vipvodu

---

# Disclaimer

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.

THE AUTHORS SHALL NOT BE LIABLE FOR ANY CLAIM, DAMAGES, OR OTHER LIABILITY ARISING FROM THE USE OF THE SOFTWARE.
