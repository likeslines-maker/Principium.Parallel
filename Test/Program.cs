using Principium;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TestConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Principium.Parallel Performance Test");
            Console.WriteLine("====================================\n");

            ThreadPool.SetMinThreads(Environment.ProcessorCount * 2, Environment.ProcessorCount * 2);
            Directory.CreateDirectory("TestResults");

            RunAllTests();
            GenerateComparisonChart();

            Console.WriteLine("\n✅ Все тесты завершены!");
            Console.WriteLine("Результаты сохранены в папке TestResults/");
        }

        static void GenerateComparisonChart()
        {
            var files = Directory.GetFiles("TestResults", "*_summary.txt");
            var scenarios = files
                .Select(Path.GetFileNameWithoutExtension)
                .Where(f => f.EndsWith("_summary"))
                .Select(f => f.Replace("_summary", ""))
                .ToList();

            var allResults = new Dictionary<string, Dictionary<string, double>>();

            foreach (var scenario in scenarios)
            {
                var results = ReadScenarioResults(scenario);
                allResults[scenario] = results;
            }

            // Генерируем сравнительную таблицу
            GenerateComparisonTable(allResults);

            // Показываем график в консоли
            ShowConsoleGraph(allResults);
        }

        static Dictionary<string, double> ReadScenarioResults(string scenarioName)
        {
            var results = new Dictionary<string, double>();
            string path = $"TestResults/{scenarioName}_summary.txt";

            if (!File.Exists(path)) return results;

            var lines = File.ReadAllLines(path);
            string currentMethod = "";

            foreach (var line in lines)
            {
                if (line.EndsWith(":"))
                {
                    currentMethod = line.TrimEnd(':');
                }
                else if (line.Contains("Среднее:") && !string.IsNullOrEmpty(currentMethod))
                {
                    var parts = line.Split(':');
                    if (parts.Length >= 2)
                    {
                        var timeStr = parts[1].Replace("мс", "").Trim();
                        if (double.TryParse(timeStr, out double time))
                        {
                            results[currentMethod] = time;
                        }
                    }
                }
            }

            return results;
        }

        static void ShowConsoleGraph(Dictionary<string, Dictionary<string, double>> allResults)
        {
            Console.WriteLine("\n📈 График производительности:");
            Console.WriteLine("============================\n");

            // Находим максимальное время для масштабирования
            double maxTime = allResults.Values
                .SelectMany(r => r.Values)
                .DefaultIfEmpty(1)
                .Max();

            var orderedScenarios = allResults.Keys.OrderByDescending(GetDupRatio).ToList();

            foreach (var scenario in orderedScenarios)
            {
                Console.WriteLine($"{scenario.PadRight(35)}");

                var results = allResults[scenario];
                var methods = new[] {
                    "Parallel.ForEach",
                    "Dict LWW + Parallel",
                    "MemoryCache + Parallel",
                    "Principium (cold)",
                    "Principium (warm)"
                };

                foreach (var method in methods)
                {
                    if (results.TryGetValue(method, out double time))
                    {
                        int barLength = (int)((time / maxTime) * 40);
                        string bar = new string('█', Math.Max(1, barLength)).PadRight(40);
                        Console.WriteLine($"  {method.PadRight(25)} {bar} {time,6:F0} мс");
                    }
                }
                Console.WriteLine();
            }
        }

        static double GetDupRatio(string scenarioName)
        {
            if (scenarioName.Contains("99%")) return 0.99;
            if (scenarioName.Contains("90%")) return 0.90;
            if (scenarioName.Contains("50%")) return 0.50;
            if (scenarioName.Contains("10%")) return 0.10;
            return 0.0;
        }

        static void GenerateComparisonTable(Dictionary<string, Dictionary<string, double>> allResults)
        {
            string path = "TestResults/comparison_table.md";

            using (var writer = new StreamWriter(path))
            {
                writer.WriteLine("# Сравнение производительности Principium.Parallel");
                writer.WriteLine();
                writer.WriteLine("| Сценарий | Parallel.ForEach | Dict LWW + Parallel | MemoryCache + Parallel | Principium (cold) | Principium (warm) |");
                writer.WriteLine("|----------|-----------------|----------------------|------------------------|------------------|------------------|");

                var orderedScenarios = allResults.Keys.OrderByDescending(GetDupRatio).ToList();

                foreach (var scenario in orderedScenarios)
                {
                    var results = allResults[scenario];
                    writer.WriteLine($"| {scenario} | " +
                        $"{results.GetValueOrDefault("Parallel.ForEach"),13:F0} мс | " +
                        $"{results.GetValueOrDefault("Dict LWW + Parallel"),20:F0} мс | " +
                        $"{results.GetValueOrDefault("MemoryCache + Parallel"),22:F0} мс | " +
                        $"{results.GetValueOrDefault("Principium (cold)"),16:F0} мс | " +
                        $"{results.GetValueOrDefault("Principium (warm)"),16:F0} мс |");
                }
            }

            Console.WriteLine($"\nТаблица сравнения сохранена в: {path}");
        }

        static void RunAllTests()
        {
            var testScenarios = new[]
            {
                new { Name = "Нет дубликатов", Items = 10_000, UniqueKeys = 10_000 },
                new { Name = "Низкая дупликация (10%)", Items = 10_000, UniqueKeys = 9_000 },
                new { Name = "Средняя дупликация (50%)", Items = 10_000, UniqueKeys = 5_000 },
                new { Name = "Высокая дупликация (90%)", Items = 10_000, UniqueKeys = 1_000 },
                new { Name = "Очень высокая дупликация (99%)", Items = 10_000, UniqueKeys = 100 }
            };

            foreach (var scenario in testScenarios)
            {
                Console.WriteLine($"\n📊 Тест: {scenario.Name}");
                Console.WriteLine($"   Элементов: {scenario.Items:N0}");
                Console.WriteLine($"   Уникальных ключей: {scenario.UniqueKeys:N0}");
                Console.WriteLine($"   Дупликация: {(1 - (double)scenario.UniqueKeys / scenario.Items):P1}");

                var results = RunScenarioTest(scenario.Items, scenario.UniqueKeys, scenario.Name);
                SaveResults(scenario.Name, results);
            }
        }

        static double CalculateStdDev(List<double> values)
        {
            if (values.Count <= 1) return 0;

            double avg = values.Average();
            double sum = values.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sum / (values.Count - 1));
        }

        static string SanitizeFileName(string fileName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }
            return fileName;
        }

        static void SaveResults(string scenarioName, Dictionary<string, List<double>> results)
        {
            // Сохраняем в CSV
            string csvPath = $"TestResults/{SanitizeFileName(scenarioName)}.csv";
            using (var writer = new StreamWriter(csvPath))
            {
                writer.WriteLine("Method,Iteration,TimeMs");
                foreach (var kvp in results)
                {
                    for (int i = 0; i < kvp.Value.Count; i++)
                    {
                        writer.WriteLine($"{kvp.Key},{i + 1},{kvp.Value[i]:F2}");
                    }
                }
            }

            // Сохраняем сводку
            string summaryPath = $"TestResults/{SanitizeFileName(scenarioName)}_summary.txt";
            using (var writer = new StreamWriter(summaryPath))
            {
                writer.WriteLine($"Сценарий: {scenarioName}");
                writer.WriteLine($"Дата теста: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine();
                writer.WriteLine("Результаты (среднее время в мс):");
                writer.WriteLine("=================================");

                foreach (var kvp in results)
                {
                    double avg = kvp.Value.Average();
                    double min = kvp.Value.Min();
                    double max = kvp.Value.Max();
                    double stdDev = CalculateStdDev(kvp.Value);

                    writer.WriteLine($"{kvp.Key}:");
                    writer.WriteLine($"  Среднее: {avg:F2} мс");
                    writer.WriteLine($"  Минимум: {min:F2} мс");
                    writer.WriteLine($"  Максимум: {max:F2} мс");
                    writer.WriteLine($"  Отклонение: {stdDev:F2} мс");
                    writer.WriteLine();
                }

                // Сравнение производительности
                writer.WriteLine("Сравнение производительности:");
                writer.WriteLine("=============================");

                double? parallelAvgOpt = results.ContainsKey("Parallel.ForEach")
                    ? results["Parallel.ForEach"].Average() : null;
                double? lwwAvgOpt = results.ContainsKey("Dict LWW + Parallel")
                    ? results["Dict LWW + Parallel"].Average() : null;

                foreach (var kvp in results)
                {
                    double otherAvg = kvp.Value.Average();
                    string methodName = kvp.Key;

                    if (parallelAvgOpt.HasValue)
                    {
                        double speedup = parallelAvgOpt.Value / otherAvg;
                        writer.WriteLine($"{methodName} vs Parallel.ForEach: {speedup:F2}x ускорение");
                    }

                    if (lwwAvgOpt.HasValue && methodName != "Dict LWW + Parallel")
                    {
                        double speedupLww = lwwAvgOpt.Value / otherAvg;
                        writer.WriteLine($"{methodName} vs Dict LWW + Parallel: {speedupLww:F2}x ускорение");
                    }
                }
            }

            Console.WriteLine($"   Результаты сохранены в: {csvPath}");
            Console.WriteLine($"   Сводка сохранена в: {summaryPath}");
        }

        static Dictionary<string, List<double>> RunScenarioTest(int totalItems, int uniqueKeys, string scenarioName)
        {
            var data = GenerateTestData(totalItems, uniqueKeys);

            var results = new Dictionary<string, List<double>>
            {
                ["Parallel.ForEach"] = new(),
                ["Dict LWW + Parallel"] = new(),
                ["MemoryCache + Parallel"] = new(),
                ["Principium (cold)"] = new(),
                ["Principium (warm)"] = new()
            };

            const int iterations = 5;
            const int warmup = 2;

            Console.WriteLine($"   Итераций: {iterations} (разогрев: {warmup})");

            for (int i = 0; i < iterations + warmup; i++)
            {
                bool isWarmup = i < warmup;

                // 1. Parallel.ForEach (naive baseline)
                if (!isWarmup)
                {
                    var time1 = TestParallelForEach(data, RealHeavyWork);
                    results["Parallel.ForEach"].Add(time1);
                }

                // 2. Dict LWW + Parallel (optimal one-shot LWW baseline)
                if (!isWarmup)
                {
                    var time2 = TestDictLwwParallel(data, RealHeavyWorkWithResult);
                    results["Dict LWW + Parallel"].Add(time2);
                }

                // 3. MemoryCache + Parallel (standard Microsoft cache)
                if (!isWarmup)
                {
                    var time3 = TestMemoryCacheParallel(data, RealHeavyWorkWithResult);
                    results["MemoryCache + Parallel"].Add(time3);
                }

                // 4. Principium (cold) - новый engine на каждый прогон
                if (!isWarmup)
                {
                    var time4 = TestPrincipiumCold(data, RealHeavyWorkWithResult);
                    results["Principium (cold)"].Add(time4);
                }

                // 5. Principium (warm) - reuse engine+cache между итерациями
                if (!isWarmup)
                {
                    var time5 = TestPrincipiumWarm(data, RealHeavyWorkWithResult);
                    results["Principium (warm)"].Add(time5);
                }

                if (!isWarmup)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }
            }

            return results;
        }

        static List<(int Key, string Value)> GenerateTestData(int totalItems, int uniqueKeys)
        {
            var random = new Random(42);
            var data = new List<(int Key, string Value)>();

            // Создаем фиксированные значения для каждого ключа (стабильный payload на ключ)
            var keyValues = new Dictionary<int, string>();
            for (int i = 0; i < uniqueKeys; i++)
            {
                keyValues[i] = $"Item_{i}_Value_{random.Next(0, 1000)}";
            }

            for (int i = 0; i < totalItems; i++)
            {
                int key = random.Next(0, uniqueKeys);
                data.Add((key, keyValues[key]));
            }

            return data;
        }

        static double TestParallelForEach(List<(int Key, string Value)> data, Action<string> work)
        {
            var sw = Stopwatch.StartNew();
            System.Threading.Tasks.Parallel.ForEach(data, item => work(item.Value));
            sw.Stop();
            return sw.Elapsed.TotalMilliseconds;
        }

        static double TestDictLwwParallel(List<(int Key, string Value)> data, Func<string, string> work)
        {
            var sw = Stopwatch.StartNew();

            // Dict LWW + Parallel (оптимальный one-shot LWW baseline)
            var latest = new Dictionary<int, string>();
            foreach (var item in data)
            {
                latest[item.Key] = item.Value;
            }

            var output = new ConcurrentDictionary<int, string>();
            System.Threading.Tasks.Parallel.ForEach(latest, kvp =>
            {
                output[kvp.Key] = work(kvp.Value);
            });

            sw.Stop();
            return sw.Elapsed.TotalMilliseconds;
        }

        static double TestMemoryCacheParallel(List<(int Key, string Value)> data, Func<string, string> work)
        {
            var sw = Stopwatch.StartNew();

            var cache = new MemoryCache(new MemoryCacheOptions());
            var output = new ConcurrentDictionary<int, string>();

            System.Threading.Tasks.Parallel.ForEach(data, item =>
            {
                var cacheKey = $"key_{item.Key}";

                if (!cache.TryGetValue(cacheKey, out string result))
                {
                    result = work(item.Value);
                    cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
                }

                output[item.Key] = result;
            });

            sw.Stop();
            return sw.Elapsed.TotalMilliseconds;
        }

        static double TestPrincipiumCold(List<(int Key, string Value)> data, Func<string, string> work)
        {
            var sw = Stopwatch.StartNew();
            var results = Paralleling.ForEach(
                data,
                x => x.Key,
                x => x.Value,
                work,
                new PrincipiumOptions(
                    ttl: TimeSpan.FromMinutes(5),
                    cacheCapacity: 100_000,
                    requireLww: true),
                adaptiveKey: Guid.NewGuid().ToString()); // cold: новый engine каждый раз
            sw.Stop();
            return sw.Elapsed.TotalMilliseconds;
        }

        static double TestPrincipiumWarm(List<(int Key, string Value)> data, Func<string, string> work)
        {
            var sw = Stopwatch.StartNew();
            var results = Paralleling.ForEach(
                data,
                x => x.Key,
                x => x.Value,
                work,
                new PrincipiumOptions(
                    ttl: TimeSpan.FromMinutes(5),
                    cacheCapacity: 100_000,
                    requireLww: true),
                adaptiveKey: "warm-key"); // warm: постоянный engine+cache
            sw.Stop();
            return sw.Elapsed.TotalMilliseconds;
        }

        static string RealHeavyWorkWithResult(string input)
        {
            RealHeavyWork(input);
            return $"Processed_{input.GetHashCode():X}";
        }

        static void RealHeavyWork(string input)
        {
            ulong hash = 1469598103934665603UL;
            foreach (char c in input)
            {
                hash ^= (byte)c;
                hash *= 1099511628211UL;
            }

            double result = 0;
            int computations = 5000;

            for (int i = 0; i < computations; i++)
            {
                double x = (double)hash + i;
                x = Math.Sin(x) * Math.Cos(x * 0.1);
                x = Math.Exp(Math.Log(Math.Abs(x) + 1));
                x = Math.Pow(x, 1.2);
                result += x;

                hash = (hash ^ (hash >> 30)) * 0xbf58476d1ce4e5b9UL;
                hash = (hash ^ (hash >> 27)) * 0x94d049bb133111ebUL;
                hash = hash ^ (hash >> 31);
            }

            if (result > double.MaxValue)
            {
                Console.Write("Impossible");
            }
        }
    }
}