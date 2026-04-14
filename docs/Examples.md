# Примеры использования

Этот документ собран как набор коротких сценариев, которые можно брать за основу при интеграции GDELib в приложение.

## Минимальный пример

```csharp
using System;
using System.IO;
using GDELib;

string storeDir = Path.Combine(Environment.CurrentDirectory, "Example-Minimal");
Directory.CreateDirectory(storeDir);

var writer = new DEObject(storeDir);
writer.CreateCell(42);
writer.CreateCell("hello");
writer.Save();

var reader = new DEObject(storeDir);
string[] values = reader.OpenAll();

Console.WriteLine(string.Join(", ", values));
```

Когда использовать:

- для smoke-test после подключения пакета;
- для проверки путей и прав доступа;
- для первого знакомства с `DEObject`.

## Пример записи разных типов

```csharp
using System;
using System.IO;
using GDELib;

string storeDir = Path.Combine(Environment.CurrentDirectory, "Example-Write");
Directory.CreateDirectory(storeDir);

var de = new DEObject(storeDir);

// Базовые типы
de.CreateCell(100);
de.CreateCell(15.75);
de.CreateCell("Документ");
de.CreateCell(true);

// Универсальная строковая перегрузка удобна, когда тип приходит извне
de.CreateCell("file", @"C:\Temp\report.txt");

de.Save();
```

Что важно:

- порядок `CreateCell(...)` определяет порядок последующего чтения;
- файл добавляется не как текст, а как путь к файлу, который библиотека затем упакует в zip. `[confirmed by code]`

## Пример чтения всего набора

```csharp
using System;
using System.IO;
using GDELib;

string storeDir = Path.Combine(Environment.CurrentDirectory, "Example-ReadAll");

var de = new DEObject(storeDir);
string[] values = de.OpenAll();

if (values == null)
{
    Console.WriteLine("Чтение не удалось.");
    return;
}

for (int i = 0; i < values.Length; i++)
{
    Console.WriteLine($"[{i}] = {values[i]}");
}
```

Когда использовать:

- если нужен весь набор сразу;
- если дальше планируется `NextData()`;
- если нужно быстро проверить round-trip после `Save()`.

## Пример пошагового чтения с диска

```csharp
using System;
using System.IO;
using GDELib;

string storeDir = Path.Combine(Environment.CurrentDirectory, "Example-OpenNext");

var de = new DEObject(storeDir);

string first = de.OpenNext();
string second = de.OpenNext();
string fourth = de.OpenNext(3);

Console.WriteLine(first);
Console.WriteLine(second);
Console.WriteLine(fourth);
```

Что нужно помнить:

- `OpenNext()` читает из файла;
- повторные вызовы не являются дешёвой потоковой итерацией, потому что библиотека каждый раз заново разбирает файл. `[confirmed by code]`

## Пример последовательного чтения из памяти

Сценарий полезен, когда вы один раз открыли весь набор и хотите обойти его без новых обращений к диску.

```csharp
using System;
using System.IO;
using GDELib;

string storeDir = Path.Combine(Environment.CurrentDirectory, "Example-NextData");

var de = new DEObject(storeDir);
de.OpenAll();

for (int i = 0; i < de.NumberInfoInt(); i++)
{
    string value = de.NextData();
    Console.WriteLine(value);
}
```

Практический смысл:

- `OpenAll()` загружает набор в память;
- `NextData()` просто двигает внутренний индекс.

## Пример повторного открытия данных новым объектом

```csharp
using System;
using System.IO;
using GDELib;

string storeDir = Path.Combine(Environment.CurrentDirectory, "Example-Reopen");
Directory.CreateDirectory(storeDir);

var writer = new DEObject(storeDir);
writer.CreateCell("persisted");
writer.Save();

// Новый экземпляр имитирует новый запуск приложения
var reader = new DEObject(storeDir);
string[] values = reader.OpenAll();

Console.WriteLine(values[0]);
```

Это лучший шаблон для прикладной проверки, что сохранение реально прошло успешно.

## Пример работы с матрицами

Матрицы подтверждены в текущем коде репозитория, но не относятся к минимальному опубликованному набору `1.4.0`. `[confirmed by git history]`

```csharp
using System;
using System.IO;
using GDELib;

string storeDir = Path.Combine(Environment.CurrentDirectory, "Example-Matrix");
Directory.CreateDirectory(storeDir);

int[,] matrix =
{
    { 1, 2, 3 },
    { 5, 8, 13 }
};

var writer = new DEObject(storeDir);
writer.CreateCell(matrix);
writer.Save();

var reader = new DEObject(storeDir);
string[] values = reader.OpenAll();

// values[0] будет строкой вида "matrix_0"
int[,] restored = reader.MatrixData(values[0]);
Console.WriteLine(restored[1, 2]);
```

## Пример с паролем

```csharp
using System;
using System.IO;
using GDELib;

string storeDir = Path.Combine(Environment.CurrentDirectory, "Example-Password");
Directory.CreateDirectory(storeDir);

var writer = new DEObject(storeDir);
writer.Password("secret");
writer.CreateCell("protected");
writer.Save();

var reader = new DEObject(storeDir);
reader.Password("secret");

string[] values = reader.OpenAll();
Console.WriteLine(values?[0] ?? "Не удалось прочитать данные");
```

Ограничение:

- пароль проверяет право чтения, но не шифрует содержимое файла. `[confirmed by code]`

## Пример типичной ошибки и исправления

### Ошибка: забыли установить пароль перед чтением

```csharp
using System.IO;
using GDELib;

string storeDir = Path.Combine(Environment.CurrentDirectory, "Example-Password-Fail");

var writer = new DEObject(storeDir);
writer.Password("secret");
writer.CreateCell("data");
writer.Save();

var brokenReader = new DEObject(storeDir);
string[] brokenValues = brokenReader.OpenAll(); // Вернётся null
```

Исправление:

```csharp
using System.IO;
using GDELib;

string storeDir = Path.Combine(Environment.CurrentDirectory, "Example-Password-Fail");

var reader = new DEObject(storeDir);
reader.Password("secret");

string[] values = reader.OpenAll();
```

### Ошибка: попытка трактовать матрицу как обычную строку

Неправильно:

```csharp
string[] values = de.OpenAll();
Console.WriteLine(values[0]); // Вы увидите только "matrix_0"
```

Правильно:

```csharp
string[] values = de.OpenAll();
int[,] matrix = de.MatrixData(values[0]);
Console.WriteLine(matrix[0, 0]);
```

## Best practices

- Используйте типизированные перегрузки `CreateCell(...)`, а не строковую, если тип известен на этапе компиляции.
- После `Save()` открывайте данные новым экземпляром `DEObject` и сравнивайте результат.
- Для массового чтения делайте `OpenAll()` один раз, а потом итерируйтесь через `NextData()`.
- Задавайте явные каталоги для хранения и кэша.
- Не используйте `Password(...)` как замену шифрованию.
- Для удаления одного элемента предпочитайте `ResetData(i)`, а не `Dell(i)`, пока дефект пересчёта индексов не исправлен. `[confirmed by code]`
