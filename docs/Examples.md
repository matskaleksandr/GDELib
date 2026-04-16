# Примеры использования

Ниже собраны практические примеры для `GDELib 1.4.0`. Все они опираются на реальный публичный API библиотеки и подходят как отправная точка для собственного проекта.

## Минимальный пример

```csharp
using System;
using System.IO;
using GDELib;

string storeDir = Path.Combine(Environment.CurrentDirectory, "Example-Minimal");
Directory.CreateDirectory(storeDir);

var de = new DEObject(storeDir);
de.CreateCell("int", 42);
de.CreateCell("string", "hello");
de.Save();

string[] values = de.OpenAll();
Console.WriteLine(string.Join(", ", values));
```

## Пример записи разных типов данных

```csharp
using System;
using System.IO;
using GDELib;

string storeDir = Path.Combine(Environment.CurrentDirectory, "Example-Write");
Directory.CreateDirectory(storeDir);

var de = new DEObject(storeDir);
de.CreateCell("int", 100);
de.CreateCell("double", 15.75);
de.CreateCell("string", "Документ");
de.CreateCell("bool", true);
de.Save();
```

## Пример чтения всего набора

```csharp
using System;
using System.IO;
using GDELib;

string storeDir = Path.Combine(Environment.CurrentDirectory, "Example-ReadAll");

var de = new DEObject(storeDir);
string[] values = de.OpenAll();

foreach (string value in values)
{
    Console.WriteLine(value);
}
```

## Пример пошагового чтения

```csharp
using System;
using System.IO;
using GDELib;

string storeDir = Path.Combine(Environment.CurrentDirectory, "Example-OpenNext");

var de = new DEObject(storeDir);
Console.WriteLine(de.OpenNext(0));
Console.WriteLine(de.OpenNext(1));
```

Такой вариант удобен, если нужно получить конкретные позиции без полного перебора всего массива.

## Пример повторного чтения тем же объектом

```csharp
using System;
using System.IO;
using GDELib;

string storeDir = Path.Combine(Environment.CurrentDirectory, "Example-Reuse");
Directory.CreateDirectory(storeDir);

var de = new DEObject(storeDir);
de.CreateCell("int", 7);
de.CreateCell("string", "reuse");
de.Save();

string[] values = de.OpenAll();
Console.WriteLine(values[0]);
Console.WriteLine(values[1]);
```

Этот сценарий подходит, если запись и проверка результата выполняются в одном и том же участке кода.

## Пример чтения новым объектом

```csharp
using System;
using System.IO;
using GDELib;

string storeDir = Path.Combine(Environment.CurrentDirectory, "Example-NewObject");
Directory.CreateDirectory(storeDir);

var writer = new DEObject(storeDir);
writer.CreateCell("string", "persisted");
writer.Save();

var reader = new DEObject(storeDir);
string[] values = reader.OpenAll();
Console.WriteLine(values[0]);
```

Такой подход часто удобен, если чтение отделено от записи логикой приложения.

## Пример работы с файлом

```csharp
using System;
using System.IO;
using GDELib;

string storeDir = Path.Combine(Environment.CurrentDirectory, "Example-File");
Directory.CreateDirectory(storeDir);

string sourceFile = Path.Combine(storeDir, "note.txt");
File.WriteAllText(sourceFile, "Пример файла");

var de = new DEObject(storeDir);
de.CreateCell("file", sourceFile);
de.Save();

string restoredFilePath = de.OpenAll()[0];
Console.WriteLine(restoredFilePath);
```

В этом примере:

- в `CreateCell("file", ...)` передаётся путь к исходному файлу;
- библиотека сохраняет файл внутри контейнера;
- после чтения возвращается путь к его восстановленной копии.

## Пример однофайлового режима

```csharp
using System;
using System.IO;
using GDELib;

string storeDir = Path.Combine(Environment.CurrentDirectory, "Example-OneFile");
Directory.CreateDirectory(storeDir);

var de = new DEObject(storeDir, true, "bundle.sve", "struct.sve", storeDir);
de.CreateCell("int", 1);
de.CreateCell("string", "one-file");
de.Save();

string[] values = de.OpenAll();
Console.WriteLine(string.Join(", ", values));
```

## Пример изменения данных

```csharp
using System;
using System.IO;
using GDELib;

string storeDir = Path.Combine(Environment.CurrentDirectory, "Example-Update");
Directory.CreateDirectory(storeDir);

var de = new DEObject(storeDir);
de.CreateCell("string", "Старое значение");
de.RecreateCell(0, "string", "Новое значение");
de.Save();

Console.WriteLine(de.OpenAll()[0]);
```

## Пример удаления элемента

```csharp
using System;
using System.IO;
using GDELib;

string storeDir = Path.Combine(Environment.CurrentDirectory, "Example-Delete");
Directory.CreateDirectory(storeDir);

var de = new DEObject(storeDir);
de.CreateCell("int", 1);
de.CreateCell("int", 2);
de.CreateCell("int", 3);

de.Dell(1);
de.Save();

Console.WriteLine(string.Join(", ", de.OpenAll()));
```

## Пример типичной ошибки и исправления

### Ошибка: каталог ещё не создан

```csharp
string storeDir = Path.Combine(Environment.CurrentDirectory, "MissingFolder");
var de = new DEObject(storeDir);
```

Лучше так:

```csharp
string storeDir = Path.Combine(Environment.CurrentDirectory, "MissingFolder");
Directory.CreateDirectory(storeDir);
var de = new DEObject(storeDir);
```

## Best practices

- Начинайте с двухфайлового режима, если хотите проще видеть структуру сохранения.
- Используйте однофайловый режим, если приложению нужен один основной контейнер.
- После чтения файловых ячеек используйте путь, который вернула библиотека.
- Если нужно быстро проверить запись, можно прочитать данные тем же объектом через `OpenAll()`.
