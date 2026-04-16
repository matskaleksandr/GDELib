# Быстрый старт

Этот документ помогает начать работу с `GDELib 1.4.0` без лишней подготовки: подключить пакет, создать контейнер, сохранить данные и прочитать их обратно.

## 1. Что нужно для старта

Для работы с библиотекой нужен проект на одной из поддерживаемых платформ:

- `.NET 6.0`
- `.NET Standard 2.1`
- `.NET Framework 4.7.2`

Внешний API версии `1.4.0` построен вокруг одного класса — `DEObject`.

## 2. Установка через NuGet

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

## 3. Подключение через DLL

Если библиотека подключается без NuGet:

1. Откройте пакет `GDELib 1.4.0`.
2. Выберите сборку из подходящей папки:
   - `lib/net6.0`
   - `lib/netstandard2.1`
   - `lib/net472`
3. Добавьте ссылку на `GDELib.dll` в проект.

Этот способ удобен для офлайн-сборок и контролируемого распространения DLL внутри команды или проекта.

## 4. Первый рабочий пример

```csharp
using System;
using System.IO;
using GDELib;

string storeDir = Path.Combine(Environment.CurrentDirectory, "QuickStartStore");
Directory.CreateDirectory(storeDir);

var de = new DEObject(storeDir);
de.CreateCell("int", 100);
de.CreateCell("double", 12.5);
de.CreateCell("string", "demo");
de.CreateCell("bool", false);
de.Save();

string[] values = de.OpenAll();

foreach (string value in values)
{
    Console.WriteLine(value);
}
```

Что делает этот код:

- создаёт рабочую папку;
- формирует контейнер `DEObject`;
- добавляет четыре значения;
- сохраняет их в формат SVE;
- читает сохранённый набор обратно.

## 5. Пример с файлом

```csharp
using System;
using System.IO;
using GDELib;

string storeDir = Path.Combine(Environment.CurrentDirectory, "FileStore");
Directory.CreateDirectory(storeDir);

string sourceFile = Path.Combine(storeDir, "report.txt");
File.WriteAllText(sourceFile, "Отчёт для примера");

var de = new DEObject(storeDir);
de.CreateCell("file", sourceFile);
de.Save();

string[] values = de.OpenAll();
string restoredFilePath = values[0];

Console.WriteLine(restoredFilePath);
```

Здесь важно понимать следующее:

- в метод передаётся путь к исходному файлу;
- библиотека сохраняет файл внутрь контейнера;
- при чтении возвращается путь к восстановленной копии файла.

## 6. Что создаётся на диске

### Двухфайловый режим

По умолчанию библиотека создаёт:

- `struct.sve`
- `data.sve`
- папку `cashfile`

Такой режим удобен, когда хочется явно разделять структуру контейнера и полезные данные.

### Однофайловый режим

Если создать объект так:

```csharp
var de = new DEObject(storeDir, true);
```

то основной контейнер будет один. Например, при имени `bundle.sve` на диске останется один главный файл:

```csharp
var de = new DEObject(storeDir, true, "bundle.sve", "struct.sve", storeDir);
```

## 7. Как использовать один и тот же объект повторно

Создавать новый экземпляр для чтения необязательно. Обычный рабочий вариант выглядит так:

```csharp
var de = new DEObject(storeDir);
de.CreateCell("int", 7);
de.CreateCell("string", "one object");
de.Save();

string[] values = de.OpenAll();
Console.WriteLine(values[0]);
Console.WriteLine(values[1]);
```

Если в приложении удобнее разделить запись и чтение по этапам, можно создать и новый экземпляр. Оба подхода допустимы.

## 8. Типичные ошибки подключения

### Рабочая папка не подготовлена

Если каталог ещё не существует, создайте его заранее через `Directory.CreateDirectory(...)`.

### Выбрана не та DLL

При ручном подключении убедитесь, что сборка соответствует платформе проекта.

### Перепутан режим хранения

Если данные были записаны в однофайловом режиме, открывать их тоже лучше в однофайловом режиме. То же правило действует и для двухфайлового режима.

## 9. Рекомендации для практического использования

- Используйте понятные имена рабочих папок и файлов контейнера.
- Для типовых приложений начинайте с двухфайлового режима: он проще для визуального понимания структуры сохранения.
- Если нужно хранить всё в одном контейнере, переключайтесь на `_TOne = true`.
- После сохранения файловых ресурсов работайте уже с путём, который вернула библиотека при чтении.

## 10. Куда идти дальше

- [Public-API.md](Public-API.md) — быстро посмотреть доступные методы.
- [API-DEObject.md](API-DEObject.md) — подробно изучить `DEObject`.
- [Examples.md](Examples.md) — взять готовые рабочие сценарии.
- [FileFormat.md](FileFormat.md) — понять организацию формата `1.4.0`.
