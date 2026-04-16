using System.Diagnostics;
using System.Globalization;
using System.Text;
using GDELib;

Console.OutputEncoding = Encoding.UTF8;

BenchmarkRunner.Run();

internal static class BenchmarkRunner
{
    private const int WarmupIterations = 2;
    private const int MeasurementIterations = 8;

    private static readonly BenchmarkScenario[] Scenarios =
    {
        new(
            "Scalar_400",
            "100 наборов int/double/string/bool",
            400,
            static (inputRoot, de) =>
            {
                for (int i = 0; i < 100; i++)
                {
                    de.CreateCell("int", i);
                    de.CreateCell("double", i + 0.25);
                    de.CreateCell("string", $"value_{i:000}");
                    de.CreateCell("bool", i % 2 == 0);
                }
            }),
        new(
            "Scalar_4000",
            "1000 наборов int/double/string/bool",
            4000,
            static (inputRoot, de) =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    de.CreateCell("int", i);
                    de.CreateCell("double", i + 0.25);
                    de.CreateCell("string", $"value_{i:0000}");
                    de.CreateCell("bool", i % 2 == 0);
                }
            }),
        new(
            "Files_3x64KB",
            "Три файловые ячейки по 64 КиБ",
            3,
            static (inputRoot, de) =>
            {
                foreach (string filePath in CreateInputFiles(inputRoot, 3, 64 * 1024))
                {
                    de.CreateCell("file", filePath);
                }
            }),
        new(
            "Mixed_4003",
            "1000 наборов скалярных значений и три файла по 64 КиБ",
            4003,
            static (inputRoot, de) =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    de.CreateCell("int", i);
                    de.CreateCell("double", i + 0.25);
                    de.CreateCell("string", $"value_{i:0000}");
                    de.CreateCell("bool", i % 2 == 0);
                }

                foreach (string filePath in CreateInputFiles(inputRoot, 3, 64 * 1024))
                {
                    de.CreateCell("file", filePath);
                }
            })
    };

    public static void Run()
    {
        string benchmarkRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "runtime"));

        string resultsRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "results"));

        Directory.CreateDirectory(benchmarkRoot);
        Directory.CreateDirectory(resultsRoot);

        var measurements = new List<MeasurementRow>();

        foreach (BenchmarkScenario scenario in Scenarios)
        {
            foreach (bool oneFile in new[] { false, true })
            {
                Console.WriteLine($"Сценарий: {scenario.Name}; режим: {(oneFile ? "OneFile" : "TwoFile")}");

                for (int iteration = 0; iteration < WarmupIterations + MeasurementIterations; iteration++)
                {
                    bool isWarmup = iteration < WarmupIterations;
                    MeasurementRow row = MeasureScenario(benchmarkRoot, scenario, oneFile, iteration, isWarmup);

                    if (!isWarmup)
                    {
                        measurements.Add(row);
                    }

                    Console.WriteLine(
                        $"  {(isWarmup ? "warmup" : "measure")} #{iteration + 1}: " +
                        $"Save={row.SaveMs.ToString("F3", CultureInfo.InvariantCulture)} ms; " +
                        $"OpenAll={row.OpenAllMs.ToString("F3", CultureInfo.InvariantCulture)} ms; " +
                        $"Bytes={row.ContainerBytes}");
                }
            }
        }

        WriteCsv(resultsRoot, measurements);
        WriteSummary(resultsRoot, measurements);
    }

    private static MeasurementRow MeasureScenario(
        string benchmarkRoot,
        BenchmarkScenario scenario,
        bool oneFile,
        int iteration,
        bool isWarmup)
    {
        string scenarioLabel = $"{scenario.Name}_{(oneFile ? "one" : "two")}_{iteration}";
        string scenarioRoot = Path.Combine(benchmarkRoot, scenarioLabel);
        string inputRoot = Path.Combine(benchmarkRoot, $"{scenarioLabel}_input");

        CleanupScenario(scenarioRoot);
        CleanupPath(inputRoot);

        Directory.CreateDirectory(scenarioRoot);
        Directory.CreateDirectory(inputRoot);
        PrepareCacheArtifacts(scenarioRoot);

        var de = new DEObject(
            scenarioRoot,
            oneFile,
            oneFile ? "bundle.sve" : "data.sve",
            "struct.sve",
            scenarioRoot);

        scenario.Populate(inputRoot, de);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var stopwatch = Stopwatch.StartNew();
        de.Save();
        stopwatch.Stop();
        double saveMs = stopwatch.Elapsed.TotalMilliseconds;

        (long containerBytes, int artifactCount) = MeasureArtifacts(scenarioRoot);

        stopwatch.Restart();
        string[] values = de.OpenAll();
        stopwatch.Stop();
        double openAllMs = stopwatch.Elapsed.TotalMilliseconds;

        if (values.Length != scenario.ExpectedValues)
        {
            throw new InvalidOperationException(
                $"Сценарий {scenario.Name} вернул {values.Length} значений вместо {scenario.ExpectedValues}.");
        }

        CleanupScenario(scenarioRoot);
        CleanupPath(inputRoot);

        return new MeasurementRow(
            scenario.Name,
            scenario.Description,
            oneFile ? "OneFile" : "TwoFile",
            isWarmup,
            iteration + 1,
            saveMs,
            openAllMs,
            containerBytes,
            artifactCount,
            values.Length);
    }

    private static IEnumerable<string> CreateInputFiles(string inputRoot, int count, int sizeBytes)
    {
        for (int i = 0; i < count; i++)
        {
            string filePath = Path.Combine(inputRoot, $"input_{i + 1}.bin");
            byte[] buffer = CreateDeterministicBuffer(sizeBytes, i + 1);
            File.WriteAllBytes(filePath, buffer);
            yield return filePath;
        }
    }

    private static byte[] CreateDeterministicBuffer(int sizeBytes, int seed)
    {
        byte[] buffer = new byte[sizeBytes];
        var random = new Random(1000 + seed);
        random.NextBytes(buffer);
        return buffer;
    }

    private static (long totalBytes, int artifactCount) MeasureArtifacts(string scenarioRoot)
    {
        long totalBytes = 0;
        int artifactCount = 0;

        if (Directory.Exists(scenarioRoot))
        {
            foreach (string file in Directory.EnumerateFiles(scenarioRoot, "*", SearchOption.AllDirectories))
            {
                totalBytes += new FileInfo(file).Length;
                artifactCount++;
            }
        }

        string? parent = Path.GetDirectoryName(scenarioRoot);
        if (parent == null || !Directory.Exists(parent))
        {
            return (totalBytes, artifactCount);
        }

        string prefix = Path.GetFileName(scenarioRoot) + "\\cashfile\\";

        foreach (string entry in Directory.EnumerateFileSystemEntries(parent))
        {
            string name = Path.GetFileName(entry);
            if (!name.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (Directory.Exists(entry))
            {
                foreach (string file in Directory.EnumerateFiles(entry, "*", SearchOption.AllDirectories))
                {
                    totalBytes += new FileInfo(file).Length;
                    artifactCount++;
                }
            }
            else if (File.Exists(entry))
            {
                totalBytes += new FileInfo(entry).Length;
                artifactCount++;
            }
        }

        return (totalBytes, artifactCount);
    }

    private static void CleanupScenario(string scenarioRoot)
    {
        CleanupPath(scenarioRoot);

        string? parent = Path.GetDirectoryName(scenarioRoot);
        if (parent == null || !Directory.Exists(parent))
        {
            return;
        }

        string prefix = Path.GetFileName(scenarioRoot) + "\\cashfile\\";

        foreach (string entry in Directory.EnumerateFileSystemEntries(parent))
        {
            string name = Path.GetFileName(entry);
            if (!name.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            CleanupPath(entry);
        }
    }

    private static void CleanupPath(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
            return;
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void PrepareCacheArtifacts(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "cashfile"));
        Directory.CreateDirectory(root + "\\cashfile\\");
    }

    private static void WriteCsv(string resultsRoot, IReadOnlyCollection<MeasurementRow> measurements)
    {
        string csvPath = Path.Combine(resultsRoot, "gdelib-1.4.0-benchmark-raw.csv");

        var lines = new List<string>
        {
            "Scenario,Description,Mode,Iteration,SaveMs,OpenAllMs,ContainerBytes,ArtifactCount,ValueCount"
        };

        lines.AddRange(measurements.Select(m => string.Join(",",
            m.Scenario,
            EscapeCsv(m.Description),
            m.Mode,
            m.Iteration.ToString(CultureInfo.InvariantCulture),
            m.SaveMs.ToString("F6", CultureInfo.InvariantCulture),
            m.OpenAllMs.ToString("F6", CultureInfo.InvariantCulture),
            m.ContainerBytes.ToString(CultureInfo.InvariantCulture),
            m.ArtifactCount.ToString(CultureInfo.InvariantCulture),
            m.ValueCount.ToString(CultureInfo.InvariantCulture))));

        File.WriteAllLines(csvPath, lines, Encoding.UTF8);
    }

    private static void WriteSummary(string resultsRoot, IReadOnlyCollection<MeasurementRow> measurements)
    {
        string summaryPath = Path.Combine(resultsRoot, "gdelib-1.4.0-benchmark-summary.md");
        var summaryRows = measurements
            .GroupBy(m => new { m.Scenario, m.Description, m.Mode })
            .OrderBy(g => g.Key.Scenario)
            .ThenBy(g => g.Key.Mode)
            .Select(g => new SummaryRow(
                g.Key.Scenario,
                g.Key.Description,
                g.Key.Mode,
                Mean(g.Select(x => x.SaveMs)),
                StdDev(g.Select(x => x.SaveMs)),
                Mean(g.Select(x => x.OpenAllMs)),
                StdDev(g.Select(x => x.OpenAllMs)),
                Mean(g.Select(x => (double)x.ContainerBytes)),
                Mean(g.Select(x => (double)x.ArtifactCount))))
            .ToArray();

        var lines = new List<string>
        {
            "# Сводка benchmark для GDELib 1.4.0",
            string.Empty,
            "| Scenario | Description | Mode | Avg Save, ms | StdDev Save, ms | Avg OpenAll, ms | StdDev OpenAll, ms | Avg Bytes | Avg Artifact Count |",
            "| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |"
        };

        lines.AddRange(summaryRows.Select(row =>
            $"| {row.Scenario} | {row.Description} | {row.Mode} | {row.AvgSaveMs:F3} | {row.StdDevSaveMs:F3} | {row.AvgOpenAllMs:F3} | {row.StdDevOpenAllMs:F3} | {row.AvgBytes:F0} | {row.AvgArtifactCount:F1} |"));

        File.WriteAllLines(summaryPath, lines, Encoding.UTF8);
    }

    private static string EscapeCsv(string value)
    {
        if (!value.Contains(',') && !value.Contains('"'))
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static double Mean(IEnumerable<double> values)
    {
        double[] array = values.ToArray();
        return array.Length == 0 ? 0 : array.Average();
    }

    private static double StdDev(IEnumerable<double> values)
    {
        double[] array = values.ToArray();
        if (array.Length == 0)
        {
            return 0;
        }

        double mean = array.Average();
        double variance = array.Select(v => Math.Pow(v - mean, 2)).Average();
        return Math.Sqrt(variance);
    }
}

internal sealed record BenchmarkScenario(
    string Name,
    string Description,
    int ExpectedValues,
    Action<string, DEObject> Populate);

internal sealed record MeasurementRow(
    string Scenario,
    string Description,
    string Mode,
    bool IsWarmup,
    int Iteration,
    double SaveMs,
    double OpenAllMs,
    long ContainerBytes,
    int ArtifactCount,
    int ValueCount);

internal sealed record SummaryRow(
    string Scenario,
    string Description,
    string Mode,
    double AvgSaveMs,
    double StdDevSaveMs,
    double AvgOpenAllMs,
    double StdDevOpenAllMs,
    double AvgBytes,
    double AvgArtifactCount);
