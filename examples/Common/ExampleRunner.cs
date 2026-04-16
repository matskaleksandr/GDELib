using System.Reflection;
using System.Text;
using GDELib;

namespace GDELibExamples;

internal static class ExampleRunner
{
    public static void Run(string versionLabel)
    {
        Console.OutputEncoding = Encoding.UTF8;

        Console.WriteLine($"=== GDELib {versionLabel} ===");
        Console.WriteLine();

        PrintApiSummary();
        Console.WriteLine();

        RunTwoFileScenario(versionLabel);
        Console.WriteLine();

        RunOneFileScenario(versionLabel);
    }

    private static void PrintApiSummary()
    {
        Console.WriteLine("Публичный API DEObject:");

        Type type = typeof(DEObject);

        foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
        {
            Console.WriteLine($"  ctor: {FormatSignature(ctor)}");
        }

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                     .OrderBy(m => m.Name)
                     .ThenBy(m => m.GetParameters().Length))
        {
            Console.WriteLine($"  method: {FormatSignature(method)}");
        }
    }

    private static void RunTwoFileScenario(string versionLabel)
    {
        string root = PrepareScenario(versionLabel, "two-file");
        string sourceFile = Path.Combine(root, "note.txt");
        File.WriteAllText(sourceFile, $"Файл для сравнения релиза {versionLabel}");

        var de = CreateObject(root, oneFile: false, dataName: "data.sve", structName: "struct.sve");
        de.CreateCell("int", 42);
        de.CreateCell("double", 15.75);
        de.CreateCell("string", $"demo-{versionLabel}");
        de.CreateCell("bool", true);
        de.CreateCell("file", sourceFile);
        de.Save();

        string[] values = de.OpenAll();

        Console.WriteLine("Сценарий: двухфайловый режим");
        Console.WriteLine($"  Рабочая папка: {root}");
        Console.WriteLine("  Основные файлы:");
        foreach (var file in Directory.EnumerateFiles(root).OrderBy(path => path))
        {
            Console.WriteLine($"    {Path.GetFileName(file)}");
        }

        PrintLegacyArtifacts(root);

        Console.WriteLine("  Значения OpenAll():");
        for (int i = 0; i < values.Length; i++)
        {
            Console.WriteLine($"    [{i}] {values[i]}");
        }

        Console.WriteLine($"  OpenNext(0): {de.OpenNext(0)}");
        Console.WriteLine($"  OpenNext(1): {de.OpenNext(1)}");
        Console.WriteLine($"  NumberInfo(): {de.NumberInfo()}");
    }

    private static void RunOneFileScenario(string versionLabel)
    {
        string root = PrepareScenario(versionLabel, "one-file");

        var de = CreateObject(root, oneFile: true, dataName: "bundle.sve", structName: "struct.sve");
        de.CreateCell("int", 7);
        de.CreateCell("string", $"one-file-{versionLabel}");
        de.CreateCell("bool", false);
        de.Save();

        string[] values = de.OpenAll();

        Console.WriteLine("Сценарий: однофайловый режим");
        Console.WriteLine($"  Рабочая папка: {root}");
        Console.WriteLine("  Основные файлы:");
        foreach (var file in Directory.EnumerateFiles(root).OrderBy(path => path))
        {
            Console.WriteLine($"    {Path.GetFileName(file)}");
        }

        Console.WriteLine("  Значения OpenAll():");
        for (int i = 0; i < values.Length; i++)
        {
            Console.WriteLine($"    [{i}] {values[i]}");
        }
    }

    private static DEObject CreateObject(string root, bool oneFile, string dataName, string structName)
    {
        PrepareCacheArtifacts(root);
        return new DEObject(root, oneFile, dataName, structName, root);
    }

    private static string PrepareScenario(string versionLabel, string scenarioName)
    {
        string root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "runtime",
            versionLabel,
            scenarioName));

        CleanupScenario(root);
        Directory.CreateDirectory(root);
        PrepareCacheArtifacts(root);
        return root;
    }

    private static void CleanupScenario(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }

        string parent = Path.GetDirectoryName(root)!;
        string prefix = Path.GetFileName(root) + "\\cashfile\\";

        if (!Directory.Exists(parent))
        {
            return;
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(parent))
        {
            string name = Path.GetFileName(entry);
            if (!name.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (Directory.Exists(entry))
            {
                Directory.Delete(entry, true);
            }
            else if (File.Exists(entry))
            {
                File.Delete(entry);
            }
        }
    }

    private static void PrepareCacheArtifacts(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "cashfile"));
        Directory.CreateDirectory(root + "\\cashfile\\");
    }

    private static void PrintLegacyArtifacts(string root)
    {
        string parent = Path.GetDirectoryName(root)!;
        string prefix = Path.GetFileName(root) + "\\cashfile\\";

        var entries = Directory.Exists(parent)
            ? Directory.EnumerateFileSystemEntries(parent)
                .Where(path => Path.GetFileName(path).StartsWith(prefix, StringComparison.Ordinal))
                .OrderBy(path => path)
                .ToArray()
            : Array.Empty<string>();

        if (entries.Length == 0)
        {
            return;
        }

        Console.WriteLine("  Кэш и служебные артефакты:");
        foreach (var entry in entries)
        {
            Console.WriteLine($"    {Path.GetFileName(entry)}");
        }
    }

    private static string FormatSignature(MethodBase method)
    {
        string returnType = method is MethodInfo methodInfo ? ShortTypeName(methodInfo.ReturnType) + " " : string.Empty;
        string parameters = string.Join(", ", method.GetParameters().Select(p => $"{ShortTypeName(p.ParameterType)} {p.Name}"));
        return $"{returnType}{method.Name}({parameters})";
    }

    private static string ShortTypeName(Type type)
    {
        if (type == typeof(void)) return "void";
        if (type == typeof(int)) return "int";
        if (type == typeof(double)) return "double";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(string)) return "string";
        if (type == typeof(object)) return "object";
        if (type == typeof(DEObject)) return "DEObject";
        if (type == typeof(string[])) return "string[]";
        return type.Name;
    }
}
