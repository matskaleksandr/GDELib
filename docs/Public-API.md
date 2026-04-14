# Карта публичного API

Этот документ отвечает на вопрос: какие типы и методы библиотеки действительно доступны конечному пользователю.

Короткий вывод:

- публичный внешний тип у библиотеки один — `GDELib.DEObject`; `[confirmed by code]`
- остальные типы (`DESaver`, `DESMini`, `Yacheyka`, `MatrixIndexStorage`, trie-структуры) являются внутренней реализацией и объявлены как `internal`; `[confirmed by code]`
- базовый класс `DEObject` — это `System.Object`, поэтому стандартные методы `ToString()`, `GetType()`, `Equals(object)` и `GetHashCode()` доступны как часть обычной .NET-поверхности.

## Иерархия типов

```text
System.Object
└── GDELib.DEObject
```

## Публичные типы

| Тип | Kind | Declaring type | Назначение | Статус |
| --- | --- | --- | --- | --- |
| `GDELib.DEObject` | class | `GDELib.DEObject` | Центральная точка работы с библиотекой: создание контейнера, добавление ячеек, сохранение, открытие, последовательное чтение | `confirmed by code` |

Публичных перечислений, интерфейсов, структур, делегатов и исключений во внешнем API не обнаружено. `[confirmed by code]`

## Публичные свойства

У `DEObject` нет публичных свойств. Весь внешний API построен на методах. `[confirmed by code]`

## Публичные конструкторы и методы

| Член | Kind | Declaring type | Как вызывается пользователем | Краткое назначение | Подробнее |
| --- | --- | --- | --- | --- | --- |
| `DEObject(string path_, bool _TOne = false, string _NameData = "data.sve", string _NameStruct = "struct.sve", string _pathcash = "pathcash")` | constructor | `GDELib.DEObject` | `new DEObject(...)` | Создаёт контейнер и настраивает режим хранения | [API-DEObject.md](API-DEObject.md#конструктор-deobject) |
| `Password(string passw)` | method | `GDELib.DEObject` | `de.Password("secret")` | Задаёт пароль для последующего сохранения и чтения | [API-DEObject.md](API-DEObject.md#passwordstring-passw) |
| `CreateCell(string types, object data)` | method | `GDELib.DEObject` | `de.CreateCell("int", 42)` | Универсальное добавление ячейки по строковому имени типа | [API-DEObject.md](API-DEObject.md#семейство-createcell) |
| `CreateCell(int data)` | method | `GDELib.DEObject` | `de.CreateCell(42)` | Добавляет целое число | [API-DEObject.md](API-DEObject.md#семейство-createcell) |
| `CreateCell(double data)` | method | `GDELib.DEObject` | `de.CreateCell(12.5)` | Добавляет `double` | [API-DEObject.md](API-DEObject.md#семейство-createcell) |
| `CreateCell(float data)` | method | `GDELib.DEObject` | `de.CreateCell(12.5f)` | Добавляет `float`, который хранится как `double` | [API-DEObject.md](API-DEObject.md#семейство-createcell) |
| `CreateCell(string data)` | method | `GDELib.DEObject` | `de.CreateCell("text")` | Добавляет строку | [API-DEObject.md](API-DEObject.md#семейство-createcell) |
| `CreateCell(bool data)` | method | `GDELib.DEObject` | `de.CreateCell(true)` | Добавляет логическое значение | [API-DEObject.md](API-DEObject.md#семейство-createcell) |
| `CreateCell(int[,] matrix)` | method | `GDELib.DEObject` | `de.CreateCell(matrix)` | Добавляет матрицу `int[,]` | [API-DEObject.md](API-DEObject.md#семейство-createcell) |
| `MatrixData(string code)` | method | `GDELib.DEObject` | `de.MatrixData("matrix_0")` | Возвращает матрицу по коду-маркеру | [API-DEObject.md](API-DEObject.md#matrixdatastring-code) |
| `Dell(int i)` | method | `GDELib.DEObject` | `de.Dell(0)` | Удаляет ячейку из текущего набора в памяти | [API-DEObject.md](API-DEObject.md#dellint-i) |
| `Save()` | method | `GDELib.DEObject` | `de.Save()` | Сохраняет текущий набор на диск | [API-DEObject.md](API-DEObject.md#save) |
| `OpenAll()` | method | `GDELib.DEObject` | `de.OpenAll()` | Полностью читает набор данных из файла | [API-DEObject.md](API-DEObject.md#openall) |
| `OpenNext(int i = -1)` | method | `GDELib.DEObject` | `de.OpenNext()` / `de.OpenNext(3)` | Читает одно значение из файла с последовательным смещением | [API-DEObject.md](API-DEObject.md#opennextint-i--1) |
| `NextData(int i = -1)` | method | `GDELib.DEObject` | `de.NextData()` / `de.NextData(3)` | Возвращает следующее значение из уже загруженного в память набора | [API-DEObject.md](API-DEObject.md#nextdataint-i--1) |
| `ResetData(int i = -1)` | method | `GDELib.DEObject` | `de.ResetData()` / `de.ResetData(2)` | Очищает весь набор или удаляет один элемент из памяти | [API-DEObject.md](API-DEObject.md#resetdataint-i--1) |
| `NumberInfo()` | method | `GDELib.DEObject` | `de.NumberInfo()` | Возвращает число ячеек строкой | [API-DEObject.md](API-DEObject.md#numberinfo) |
| `NumberInfoInt()` | method | `GDELib.DEObject` | `de.NumberInfoInt()` | Возвращает число ячеек как `int` | [API-DEObject.md](API-DEObject.md#numberinfoint) |
| `Association(bool _TOne)` | method | `GDELib.DEObject` | `de.Association(true)` | Переключает режим однофайловой/двухфайловой работы для последующих операций | [API-DEObject.md](API-DEObject.md#associationbool-_tone) |
| `CloneDataDE(DEObject DE)` | method | `GDELib.DEObject` | `target.CloneDataDE(source)` | Подтягивает данные из другого `DEObject` | [API-DEObject.md](API-DEObject.md#clonedatadedeobject-de) |
| `RecreateCell(int i, string types, dynamic data)` | method | `GDELib.DEObject` | `de.RecreateCell(0, "string", "new")` | Пересоздаёт ячейку по индексу | [API-DEObject.md](API-DEObject.md#recreatecellint-i-string-types-dynamic-data) |

## Унаследованные методы `System.Object`

Эти методы не объявлены в коде GDELib, но доступны конечному пользователю как часть обычной модели .NET.

| Член | Declaring type | Как вызывается | Является ли частью внешнего API | Комментарий |
| --- | --- | --- | --- | --- |
| `ToString()` | `System.Object` | `de.ToString()` | Да, как стандартный метод базового класса | Специальной переопределённой логики в `DEObject` нет. `[confirmed by code]` |
| `GetType()` | `System.Object` | `de.GetType()` | Да | Возвращает `System.Type` экземпляра `DEObject`. |
| `Equals(object obj)` | `System.Object` | `de.Equals(other)` | Да | Используется стандартная ссылочная семантика, так как переопределения нет. `[confirmed by code]` |
| `GetHashCode()` | `System.Object` | `de.GetHashCode()` | Да | Работает стандартная реализация базового класса. |

Не включены:

- `MemberwiseClone()` — `protected`, конечный пользователь напрямую не вызывает;
- `Finalize()` — служебный метод времени выполнения.

## Что не входит во внешний API

Следующие сущности есть в коде, но не должны документироваться как пользовательские:

- `DESaver`
- `DESMini`
- `Yacheyka`
- `ByteTrieNode`
- `ByteTrie`
- `KeysNibbleMap`
- `MatrixIndexStorage`

Они важны для понимания формата и алгоритма, но не предназначены для прямого вызова из клиентского кода. `[confirmed by code]`

## Практический вывод

Если вы интегрируете GDELib в приложение, почти весь ваш код будет выглядеть так:

1. создать `DEObject`;
2. заполнить его через `CreateCell(...)`;
3. вызвать `Save()`;
4. в новом экземпляре вызвать `OpenAll()` или `OpenNext()`;
5. при встрече `matrix_N` извлечь матрицу через `MatrixData(...)`.

Для детального описания семантики каждого метода переходите в [API-DEObject.md](API-DEObject.md).
