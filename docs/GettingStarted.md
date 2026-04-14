# Быстрый старт с GDELib

Этот документ показывает минимальный рабочий путь: установить библиотеку, создать `DEObject`, записать значения, повторно открыть файл и проверить результат.

## Что нужно знать заранее

GDELib работает не как ORM и не как JSON-сериализатор. Вы создаёте объект контейнера `DEObject`, добавляете в него ячейки данных и вызываете методы сохранения и открытия.

Поддерживаемые платформы опубликованного пакета `1.4.0`:

- `.NET 6.0`
- `.NET Standard 2.1`
- `.NET Framework 4.7.2`

Это подтверждается содержимым опубликованного пакета на NuGet. `[confirmed by package metadata]`

## Установка через NuGet

### Package Manager Console

```powershell
Install-Package GDELib -Version 1.4.0
```

### `PackageReference`

```xml
<ItemGroup>
  <PackageReference Include="GDELib" Version="1.4.0" />
</ItemGroup>
```

### `.NET CLI`

```bash
dotnet add package GDELib --version 1.4.0
```

## Подключение через `.dll`

Если библиотека подключается вручную:

1. Скачайте пакет `GDELib.1.4.0.nupkg` или возьмите его из локального NuGet-cache.
2. Выберите DLL из нужной папки:
   - `lib/net6.0/GDELib.dll`
   - `lib/netstandard2.1/GDELib.dll`
   - `lib/net472/GDELib.dll`
3. Добавьте ссылку на DLL в проект.

Этот вариант обычно выбирают для офлайн-сред, CI без NuGet feed или legacy-решений.

## Первый рабочий пример

Ниже пример в двухфайловом режиме, который использует только подтверждённый публичный API.

```csharp
using System;
using System.IO;
using GDELib;

string storeDir = Path.Combine(Environment.CurrentDirectory, "Store");
Directory.CreateDirectory(storeDir);

var data = new DEObject(storeDir);
data.CreateCell(100);
data.CreateCell(12.5);
data.CreateCell("demo");
data.CreateCell(false);
data.Save();

var reopened = new DEObject(storeDir);
string[] values = reopened.OpenAll();

foreach (string value in values)
{
    Console.WriteLine(value);
}
```

Что здесь важно:

- `storeDir` — это папка, куда библиотека будет писать файлы;
- `CreateCell(...)` добавляет значения в память;
- `Save()` записывает состояние на диск;
- `OpenAll()` читает всё содержимое обратно.

## Что создаётся на диске

### Двухфайловый режим

Если вы создали объект так:

```csharp
var de = new DEObject(storeDir);
```

то по умолчанию используются:

- `data.sve`
- `struct.sve`
- папка `cashfile` для временной работы с вложенными файлами

Параметры имён можно изменить через конструктор.

### Однофайловый режим

Если вы создали объект так:

```csharp
var de = new DEObject(storeDir, _TOne: true);
```

то библиотека будет использовать один основной файл данных `data.sve`. В этом режиме структура и полезная нагрузка находятся в одном потоке. `[confirmed by code]`

## Пример однофайлового режима

```csharp
using System;
using System.IO;
using GDELib;

string storeDir = Path.Combine(Environment.CurrentDirectory, "SingleFileStore");
Directory.CreateDirectory(storeDir);

var de = new DEObject(storeDir, _TOne: true);
de.CreateCell(1);
de.CreateCell(2);
de.CreateCell(3);
de.Save();

var reopened = new DEObject(storeDir, _TOne: true);
string[] values = reopened.OpenAll();

Console.WriteLine(string.Join(", ", values));
```

## Пример с матрицей

Матрицы доступны в текущем коде репозитория. Это уже выходит за пределы опубликованного пакета `1.4.0` и относится к более новой ветке разработки. `[confirmed by git history]`

```csharp
using System;
using System.IO;
using GDELib;

string storeDir = Path.Combine(Environment.CurrentDirectory, "MatrixStore");
Directory.CreateDirectory(storeDir);

int[,] matrix =
{
    { 1, 1, 2 },
    { 3, 5, 8 }
};

var de = new DEObject(storeDir);
de.CreateCell(matrix);
de.Save();

var reopened = new DEObject(storeDir);
string[] values = reopened.OpenAll();

int[,] restored = reopened.MatrixData(values[0]);
Console.WriteLine(restored[1, 2]);
```

## Типичные ошибки подключения

### Несовместимая целевая платформа

Симптом: проект не может использовать DLL или пакет.

Что проверить:

- выбрана ли DLL из правильной папки `lib/...`;
- совместим ли ваш TFM с `net6.0`, `netstandard2.1` или `net472`.

### Папка хранения не существует

Симптом: `Save()` или `OpenAll()` выводит сообщение об ошибке в консоль.

Решение:

- заранее создавайте директорию хранения через `Directory.CreateDirectory(...)`;
- не полагайтесь на неявное создание всех промежуточных каталогов библиотекой.

### Неправильный пароль при чтении

Симптом: `OpenAll()` или `OpenNext()` возвращает `null`, а в консоль выводится сообщение о неверном пароле.

Решение:

- вызовите `Password(...)` до чтения;
- используйте один и тот же пароль при сохранении и открытии.

Важно: пароль в текущей реализации не шифрует данные, а только ограничивает чтение через проверку хэша. `[confirmed by code]`

### Перепутан режим хранения

Симптом: объект создан в двухфайловом режиме, а читает однофайловые данные, или наоборот.

Решение:

- используйте одинаковое значение `_TOne` при записи и повторном открытии;
- если меняете режим через `Association(bool)`, пересохраните данные новым вызовом `Save()`.

### Сборка из исходников текущего репозитория

Симптом: проект из текущего снимка репозитория не собирается как есть.

Что известно:

- в `csproj` включена подпись сборки;
- проект ссылается на `QuQ.pfx`;
- в текущем workspace этот файл отсутствует. `[confirmed by code]`

Практически это значит, что при сборке из исходников может потребоваться либо вернуть ключ, либо временно отключить подпись. `[needs verification]`

## Рекомендации для production

- Всегда задавайте явную папку хранения, а не полагайтесь на `Environment.CurrentDirectory`.
- После `Save()` выполняйте round-trip проверку: откройте файл новым экземпляром `DEObject` и сравните данные.
- Для повторного перебора уже загруженных значений используйте `OpenAll()` один раз, затем `NextData()`. Это дешевле, чем вызывать `OpenNext()` много раз подряд, потому что `OpenNext()` каждый раз повторно читает файл с начала. `[confirmed by code]`
- Если используете файловые вложения, выделяйте отдельную папку кэша и следите за правами на запись.
- Не рассматривайте `Password(...)` как криптографическую защиту данных.

## Куда идти дальше

- [Public-API.md](Public-API.md) — карта публичного API.
- [API-DEObject.md](API-DEObject.md) — подробное описание `DEObject`.
- [FileFormat.md](FileFormat.md) — устройство файлового формата.
- [Examples.md](Examples.md) — дополнительные практические сценарии.
