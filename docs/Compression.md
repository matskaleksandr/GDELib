# Сжатие в GDELib

Этот документ описывает, где именно библиотека пытается уменьшать размер данных и как устроен текущий алгоритм.

## Что сжимается, а что нет

Простыми словами:

- **матрицы `int[,]`** сжимаются отдельным алгоритмом `DESMini`; `[confirmed by code]`
- **длинные серии `int`** в однофайловом режиме сворачиваются в специальный блок `"l"`; `[confirmed by code]`
- **файлы-вложения** упаковываются в zip; `[confirmed by code]`
- обычные `string`, `double` и `bool` не проходят через отдельный общий компрессор верхнего уровня.

Это важно: GDELib не использует общий дефлейт для всего контейнера. Алгоритм сжатия специализирован под матрицы и повторяющиеся паттерны целых чисел.

## Идея алгоритма простыми словами

Текущий алгоритм для матриц работает так:

1. смотрит на содержимое матрицы;
2. выбирает часто встречающиеся значения или короткие последовательности значений;
3. заменяет их короткими индексами;
4. упаковывает индексы по 4 бита;
5. при необходимости дополнительно индексирует уже получившийся поток байтов;
6. выбирает более компактный из двух вариантов.

По сути это доменно-специфическая индексация, а не универсальный архиватор.

## Pipeline матричного блока

Ниже описан pipeline текущей реализации `MatrixIndexStorage.SaveMatrix(...)`. `[confirmed by code]`

### Этап 1. Выбор режима словаря

Алгоритм строит два варианта блока:

- **single-key mode**: словарь составлен из наиболее частых одиночных значений;
- **sequence mode**: словарь строится из коротких повторяющихся последовательностей длиной до 4 ячеек.

После этого выбирается тот вариант, который даёт меньший размер блока.

### Этап 2. Подготовка словаря последовательностей

Для каждого кандидата:

- целое значение кодируется через `EncodeIntForTrie(...)`;
- используется ZigZag-преобразование для знаковых чисел; `[confirmed by code]`
- байтовые представления последовательностей заносятся в trie.

Ограничение:

- максимум словарных элементов — `14`, потому что индексы упаковываются в 4-битные nibble и значение `0xE` зарезервировано как raw-marker. `[confirmed by code]`

### Этап 3. `int-delta`

Если в матрице нет отрицательных значений и минимальное значение больше нуля, алгоритм вычитает минимальное значение из всех элементов и сохраняет это смещение отдельно.

Зачем:

- уменьшить абсолютные значения;
- повысить шанс на короткое представление и повторяемость паттернов.

### Этап 4. Индексация матрицы

Матрица проходит построчно слева направо.

Для каждой позиции:

- алгоритм ищет самую длинную последовательность, присутствующую в trie;
- если она найдена, пишет индекс словаря в 4 бита;
- если нет, пишет raw-marker `0xE`, затем длину raw-представления и сами байты числа по nibble.

### Этап 5. `byte-delta`

После первичной индексации формируется байтовый поток `matrixDataBytes`.

Если минимальный байт в потоке больше нуля:

- из каждого байта вычитается этот минимум;
- значение минимума сохраняется как `byteDelta`.

Это ещё один локальный шаг нормализации данных.

### Этап 6. Вторичная индексация байтов

Дальше алгоритм:

- считает частоты байтов в `shiftedBytes`;
- берёт до 14 самых частых значений;
- строит второй trie уже по одиночным байтам;
- повторно кодирует байтовый поток nibble-индексами.

Если вторичная стадия даёт меньший размер, она включается. Иначе используется исходный поток после первой стадии.

### Этап 7. Завершение блока

Верхний уровень записи матрицы:

- пишет `versionFlag` (`0` или `1`);
- пишет выбранный блок;
- завершает его маркером `FF 00 FF`. `[confirmed by code]`

Важно:

- при чтении `versionFlag` валидируется, но в текущей реализации почти не влияет на дальнейшую логику разборщика, потому что ключевая информация и так уже лежит внутри самого блока. `[confirmed by code]`

## Диаграмма этапов

```mermaid
flowchart TD
    A[int[,] matrix] --> B[Построить два варианта словаря]
    B --> C[Сформировать trie по значениям или последовательностям]
    C --> D[Применить int-delta при возможности]
    D --> E[Построчно закодировать матрицу nibble-индексами]
    E --> F[Посчитать byte-delta]
    F --> G[Попробовать вторичную индексацию байтов]
    G --> H{Что короче?}
    H -->|Stage 2 короче| I[Записать stage 2]
    H -->|Raw stage 1 короче| J[Записать stage 1]
    I --> K[Записать маркер конца FF 00 FF]
    J --> K
```

## Псевдокод

```text
function SaveMatrix(matrix):
    singleBlock = SerializeDynamicBlock(matrix, useSequences = false)
    sequenceBlock = SerializeDynamicBlock(matrix, useSequences = true)

    if size(sequenceBlock) < size(singleBlock):
        write versionFlag = 1
        write sequenceBlock
    else:
        write versionFlag = 0
        write singleBlock

    write endMarker = FF 00 FF

function SerializeDynamicBlock(matrix, useSequences):
    mappedSeqs = buildMappedSequences(matrix, useSequences)
    trie1 = buildTrie(mappedSeqs)

    intDelta = computeOptionalIntDelta(matrix)
    matrixToWrite = applyIntDeltaIfNeeded(matrix, intDelta)

    matrixDataBytes = encodeMatrixAsNibbleIndices(matrixToWrite, mappedSeqs, trie1)

    byteDelta = computeOptionalByteDelta(matrixDataBytes)
    shiftedBytes = applyByteDeltaIfNeeded(matrixDataBytes, byteDelta)

    mappedBytes = pickTopBytes(shiftedBytes)
    trie2 = buildTrie(mappedBytes)
    stage2Data = encodeShiftedBytesAsNibbleIndices(shiftedBytes, mappedBytes, trie2)

    if size(stage2Data) < size(matrixDataBytes):
        write stage2 mode
    else:
        write raw matrixDataBytes
```

## Где алгоритм даёт выигрыш

Ниже не обещание производительности, а инженерный вывод из логики реализации:

- матрицы с повторяющимися числами;
- матрицы, где по строкам часто встречаются короткие одинаковые последовательности;
- данные с положительными значениями и ненулевым общим минимумом, где помогает `int-delta`;
- байтовые потоки с повторяющимися байтами после первой стадии. `[inferred from implementation]`

## Что влияет на эффективность

| Фактор | Влияние |
| --- | --- |
| Повторяемость одиночных значений | Увеличивает шанс выиграть в single-key mode |
| Повторяемость коротких последовательностей по строкам | Увеличивает шанс выиграть в sequence mode |
| Положительные значения с общим минимумом | Делают полезным `int-delta` |
| Небольшое число реально различных байтов после stage 1 | Делают полезной вторичную индексацию |
| Случайные данные без повторений | Уменьшают пользу словаря и индексации |

## Ограничения алгоритма

- Компрессор специализирован только под матрицы и некоторые серии `int`; это не универсальный контейнерный компрессор. `[confirmed by code]`
- Максимум словарных записей на стадии индексации — `14`. `[confirmed by code]`
- Конец матричного блока определяется сигнатурой `FF 00 FF`, а не длиной блока. Это потенциальная зона неоднозначности. `[confirmed by code]`
- Сжатие анализирует последовательности по строкам, а не по столбцам или диагоналям. `[confirmed by code]`
- В benchmark без реальных данных нельзя честно заявлять процент экономии.

## Сжатие серий `int` в однофайловом режиме

Это отдельная оптимизация, не равная матричному компрессору один в один.

Если библиотека находит подряд идущую серию не меньше чем из 5 значений типа `int`, она:

- записывает в секции структуры маркер `"l"`;
- сохраняет длину серии;
- превращает серию в матрицу `N x 1`;
- сохраняет её тем же матричным блоком `DESMini`. `[confirmed by code]`

Практический смысл:

- однофайловый режим может выигрывать на длинных числовых сериях даже без явных пользовательских матриц.

## Benchmark-план

Поскольку в репозитории нет опубликованных численных benchmark-результатов, корректный путь такой:

1. выбрать набор сценариев;
2. мерить отдельно запись, чтение и итоговый размер;
3. сохранять результаты в CSV;
4. строить графики уже по фактическим данным.

### Набор сценариев

| Сценарий | Что проверяет |
| --- | --- |
| `Scalar.Mixed.Small` | Малый набор `int/double/string/bool` без матриц |
| `Scalar.IntRuns` | Длинные серии `int` для однофайлового режима |
| `Matrix.Repeated` | Матрицы с большим числом повторов |
| `Matrix.RowPatterns` | Матрицы с повторяющимися последовательностями по строкам |
| `Matrix.Random` | Случайные данные как worst-case для словаря |
| `File.Payload` | Влияние zip-вложений на общий размер и время |

### Что измерять

- время `Save()`
- время `OpenAll()`
- суммарный размер выходных файлов
- размер отдельно `data.sve`
- размер отдельно `struct.sve` в двухфайловом режиме
- пиковую память процесса, если нужен углублённый профиль `[needs verification]`

## C#-каркас benchmark

Ниже каркас, который можно положить в отдельный консольный проект. Он использует только реальный публичный API GDELib.

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using GDELib;

internal static class Program
{
    private sealed record ScenarioResult(
        string Scenario,
        bool OneFile,
        int CellCount,
        long Bytes,
        double SaveMs,
        double OpenAllMs);

    private static void Main()
    {
        string root = Path.Combine(Environment.CurrentDirectory, "benchmark-out");
        Directory.CreateDirectory(root);

        var results = new List<ScenarioResult>
        {
            MeasureScalarMixed(root, oneFile: false),
            MeasureScalarMixed(root, oneFile: true),
            MeasureIntRuns(root, oneFile: false),
            MeasureIntRuns(root, oneFile: true),
            MeasureMatrixRepeated(root, oneFile: false),
            MeasureMatrixRepeated(root, oneFile: true),
            MeasureMatrixRandom(root, oneFile: false),
            MeasureMatrixRandom(root, oneFile: true)
        };

        string csvPath = Path.Combine(root, "results.csv");
        using var writer = new StreamWriter(csvPath, false);
        writer.WriteLine("Scenario,OneFile,CellCount,Bytes,SaveMs,OpenAllMs");

        foreach (var row in results)
        {
            writer.WriteLine(string.Join(",",
                row.Scenario,
                row.OneFile.ToString(),
                row.CellCount.ToString(CultureInfo.InvariantCulture),
                row.Bytes.ToString(CultureInfo.InvariantCulture),
                row.SaveMs.ToString(CultureInfo.InvariantCulture),
                row.OpenAllMs.ToString(CultureInfo.InvariantCulture)));
        }

        Console.WriteLine($"Benchmark CSV saved to: {csvPath}");
    }

    private static ScenarioResult MeasureScalarMixed(string root, bool oneFile)
    {
        return Measure(root, "Scalar.Mixed", oneFile, de =>
        {
            for (int i = 0; i < 500; i++)
            {
                de.CreateCell(i);
                de.CreateCell(i * 0.5);
                de.CreateCell("value_" + i);
                de.CreateCell(i % 2 == 0);
            }
        }, expectedCells: 2000);
    }

    private static ScenarioResult MeasureIntRuns(string root, bool oneFile)
    {
        return Measure(root, "Scalar.IntRuns", oneFile, de =>
        {
            for (int i = 0; i < 5000; i++)
            {
                de.CreateCell(i % 20);
            }
        }, expectedCells: 5000);
    }

    private static ScenarioResult MeasureMatrixRepeated(string root, bool oneFile)
    {
        return Measure(root, "Matrix.Repeated", oneFile, de =>
        {
            int[,] matrix = new int[200, 200];
            for (int r = 0; r < matrix.GetLength(0); r++)
            {
                for (int c = 0; c < matrix.GetLength(1); c++)
                {
                    matrix[r, c] = (r + c) % 4;
                }
            }

            de.CreateCell(matrix);
        }, expectedCells: 1);
    }

    private static ScenarioResult MeasureMatrixRandom(string root, bool oneFile)
    {
        return Measure(root, "Matrix.Random", oneFile, de =>
        {
            var random = new Random(12345);
            int[,] matrix = new int[200, 200];
            for (int r = 0; r < matrix.GetLength(0); r++)
            {
                for (int c = 0; c < matrix.GetLength(1); c++)
                {
                    matrix[r, c] = random.Next(-100000, 100000);
                }
            }

            de.CreateCell(matrix);
        }, expectedCells: 1);
    }

    private static ScenarioResult Measure(
        string root,
        string scenario,
        bool oneFile,
        Action<DEObject> fill,
        int expectedCells)
    {
        string dir = Path.Combine(root, scenario.Replace('.', '_') + "_" + (oneFile ? "one" : "two"));
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }

        Directory.CreateDirectory(dir);

        var writer = new DEObject(dir, oneFile);
        fill(writer);

        var sw = Stopwatch.StartNew();
        writer.Save();
        sw.Stop();
        double saveMs = sw.Elapsed.TotalMilliseconds;

        var reader = new DEObject(dir, oneFile);
        sw.Restart();
        string[] values = reader.OpenAll();
        sw.Stop();
        double openMs = sw.Elapsed.TotalMilliseconds;

        if (values == null || values.Length != expectedCells)
        {
            throw new InvalidOperationException("Round-trip check failed.");
        }

        long bytes = Directory
            .EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly)
            .Sum(path => new FileInfo(path).Length);

        return new ScenarioResult(scenario, oneFile, expectedCells, bytes, saveMs, openMs);
    }
}
```

## Шаблон таблицы результатов

После запуска каркаса удобно собрать результаты в такую таблицу:

| Scenario | OneFile | CellCount | TotalBytes | SaveMs | OpenAllMs | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| Scalar.Mixed | false | 2000 |  |  |  |  |
| Scalar.Mixed | true | 2000 |  |  |  |  |
| Scalar.IntRuns | false | 5000 |  |  |  |  |
| Scalar.IntRuns | true | 5000 |  |  |  |  |
| Matrix.Repeated | false | 1 |  |  |  |  |
| Matrix.Repeated | true | 1 |  |  |  |  |
| Matrix.Random | false | 1 |  |  |  |  |
| Matrix.Random | true | 1 |  |  |  |  |

## Какие графики строить

- `SaveMs` по сценариям и режимам;
- `OpenAllMs` по сценариям и режимам;
- `TotalBytes` по сценариям и режимам;
- относительное отличие `OneFile` vs `TwoFile`;
- отдельный график для матричных сценариев `Repeated` vs `Random`.

## Что ещё нужно измерить

- сравнение опубликованного пакета `1.4.0` и текущей ветки `1.5 Preview` на одинаковом наборе данных; `[needs verification]`
- влияние пароля на размер и время;
- влияние файловых вложений разного размера;
- устойчивость формата на граничных данных, включая матричный блок с внутренней подпоследовательностью `FF 00 FF`. `[needs verification]`
