# PdfGenerator

> [!WARNING]
> Проект разработан с помощью ИИ для личного пользования. Используйте его только на свой страх и риск.

.NET 8-библиотека для преобразования **XML + XSLT 2.0 → XSL-FO → PDF**.

NuGet-пакет: `PdfGenerator`, текущая версия — `0.1.0-preview.3`.

Рендерер поставляется вместе с пакетом: в стандартный Docker-образ .NET не нужно устанавливать Java, Apache FOP, системные шрифты или графические библиотеки. Для XSLT используется SaxonCS-HE, для PDF — Apache FOP через IKVM.

## Возможности

- XSLT 2.0, относительные `xsl:include` и `xsl:import`.
- Файловый и потоковый API, типизированные параметры XSLT и метрики задания.
- Кириллица со встроенным DejaVu Sans.
- PNG из локального файла или data URI, включая прозрачность.
- Удаление ICC-профиля PNG без перекодирования пикселей и изменения исходного изображения.
- SVG, включая текст, прозрачность и фильтры, проверенные синтетическим корпусом.
- Атомарная публикация результата: предыдущий файл сохраняется при ошибке или отмене до публикации.
- Настраиваемые лимиты, параллелизм и регистрация сервисов через DI.
- Ограниченная очередь, кэши шаблонов и фабрик FOP, отдельный пул worker.

## Поддерживаемая среда

| Среда | Поддержка |
|---|---|
| Windows x64, .NET 8 | Проверена |
| Debian 12 / Linux glibc x64, .NET 8 | Проверена в Docker |
| Alpine / musl, ARM64, macOS | Не поддерживаются этой preview |

Нужны записываемые временный каталог и каталог результата. Для Linux публикуйте приложение с RID `linux-x64`. Пакет содержит нативные компоненты, которые копируются при публикации приложения.

## Установка

Пакет пока не опубликован на nuget.org. В каталоге исходников выполните `./eng/verify.ps1`. Скопируйте **все три** пакета из `artifacts/packages` в локальный или частный NuGet-feed:

- `PdfGenerator`
- `PdfGenerator.Application`
- `PdfGenerator.Domain`

Приложение подключает только `PdfGenerator`; остальные зависимости восстанавливаются транзитивно. Maven SDK нужен для сборки исходников библиотеки; потребитель получает готовые .NET-сборки FOP/Batik.

Для первого запуска создайте консольное приложение:

```powershell
dotnet new console -n PdfExample --framework net8.0
Set-Location PdfExample
```

Создайте в его каталоге файл `NuGet.Config`. В примере пакеты библиотеки находятся в `C:\NuGet\PdfGenerator`; замените путь своим. nuget.org нужен для сторонних зависимостей, а пакеты `PdfGenerator*` выбираются из вашего feed.

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="pdfgenerator-local" value="C:\NuGet\PdfGenerator" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="pdfgenerator-local">
      <package pattern="PdfGenerator*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

Установите пакет из каталога приложения:

```powershell
dotnet add package PdfGenerator --version 0.1.0-preview.3
```

В проекте появится ссылка:

```xml
<ItemGroup>
  <PackageReference Include="PdfGenerator" Version="0.1.0-preview.3" />
</ItemGroup>
```

Если используется Central Package Management, укажите версию в `Directory.Packages.props`, а в `PackageReference` оставьте только `Include`. При повторной сборке собственной preview с той же версией учитывайте NuGet-cache: для проверки нового пакета используйте отдельный пустой каталог восстановления, например `dotnet restore --packages ./fresh-packages`. В feed должны находиться согласованные версии всех трёх пакетов.

## Инструкция по использованию

1. [Первый PDF: XML, XSLT и программа](#1-первый-pdf).
2. [Запрос, параметры и результат](#2-запрос-параметры-и-результат).
3. [Входные и выходные потоки](#3-входные-и-выходные-потоки).
4. [Регистрация в DI](#4-регистрация-в-di).
5. [Выбор режима и настройка worker](#5-выбор-режима-и-настройка-worker).
6. [Ресурсы, шрифты и большие документы](#6-ресурсы-шрифты-и-большие-документы).
7. [Ошибки, отмена и очередь](#7-ошибки-отмена-и-очередь).
8. [Кэши, отдельные этапы и диагностика](#8-кэши-отдельные-этапы-и-диагностика).
9. [Публикация и контейнер](#9-публикация-и-контейнер).

### 1. Первый PDF

Библиотеке нужны XML с данными и XSLT, который формирует **XSL-FO**. HTML на выходе шаблона не подходит. Для этого примера сохраните рядом с `Program.cs` два файла в UTF-8.

`input.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<report>
  <title>Первый отчёт</title>
  <item>Кириллица отображается встроенным шрифтом.</item>
  <item>Второй пункт отчёта.</item>
</report>
```

`document.xsl`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="2.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:xs="http://www.w3.org/2001/XMLSchema"
    xmlns:fo="http://www.w3.org/1999/XSL/Format"
    xmlns:r="urn:report"
    exclude-result-prefixes="xs r">
  <xsl:param name="r:title" as="xs:string" select="string(/report/title)" />
  <xsl:param name="includeDetails" as="xs:boolean" select="true()" />
  <xsl:param name="createdAt" as="xs:dateTime"
             select="xs:dateTime('2026-10-04T00:00:00Z')" />

  <xsl:template match="/">
    <fo:root>
      <fo:layout-master-set>
        <fo:simple-page-master master-name="a4"
            page-width="210mm" page-height="297mm" margin="15mm">
          <fo:region-body />
        </fo:simple-page-master>
      </fo:layout-master-set>
      <fo:page-sequence master-reference="a4">
        <fo:flow flow-name="xsl-region-body"
                 font-family="DejaVu Sans" font-size="11pt">
          <fo:block font-size="16pt" space-after="8mm">
            <xsl:value-of select="$r:title" />
          </fo:block>
          <fo:block space-after="4mm">
            Дата: <xsl:value-of select="$createdAt" />
          </fo:block>
          <xsl:if test="$includeDetails">
            <xsl:for-each select="report/item">
              <fo:block space-after="3mm">
                <xsl:value-of select="." />
              </fo:block>
            </xsl:for-each>
          </xsl:if>
        </fo:flow>
      </fo:page-sequence>
    </fo:root>
  </xsl:template>
</xsl:stylesheet>
```

Замените `Program.cs` следующим кодом:

```csharp
using PdfGenerator;

using var transformer = new SaxonFoTransformer();
using var renderer = new EmbeddedFopRenderer();
var generator = new XmlPdfGenerator(transformer, renderer);

Directory.CreateDirectory("output");
await generator.GenerateFileAsync(
    "input.xml", "document.xsl", "output/result.pdf");
Console.WriteLine(Path.GetFullPath("output/result.pdf"));
```

Выполните `dotnet run` из каталога приложения. В `output/result.pdf` появится одностраничный отчёт с кириллицей. Входные пути в этом примере разрешаются относительно **текущего рабочего каталога**, а не расположения DLL. В сервисе передавайте абсолютные пути и заранее создавайте каталог результата. Если шаблоны должны поставляться с приложением, добавьте для них `CopyToOutputDirectory` и `CopyToPublishDirectory` в проекте потребителя.

`GenerateFileAsync` сохранён для простого файлового сценария. Для параметров, идентификатора и метрик используйте `GenerateAsync`.

### 2. Запрос, параметры и результат

Следующий код использует `generator` из первого примера и тот же шаблон:

```csharp
var request = new ConversionRequest(
    XmlSource.FromFile("input.xml"),
    XmlSource.FromFile("document.xsl"),
    parameters: [
        new XsltParameter("title", "Отчёт с параметрами", "urn:report"),
        new XsltParameter("includeDetails", true),
        new XsltParameter("createdAt", DateTimeOffset.UtcNow)
    ],
    jobId: "report-001");

ConversionResult result = await generator.GenerateAsync(
    request, "output/parameterized.pdf");
Console.WriteLine($"{result.JobId}: {result.PageCount} стр., {result.PdfBytes} байт");
Console.WriteLine($"Всего: {result.TotalDuration.TotalMilliseconds:F0} мс");
foreach (string warning in result.Warnings)
{
    Console.WriteLine($"Предупреждение: {warning}");
}
```

Первый аргумент `XsltParameter` — **локальное имя**, без префикса. Третий — URI пространства имён. Поэтому `("title", value, "urn:report")` соответствует `r:title` из примера; передавать строку `"r:title"` нельзя. Префикс XSLT может быть любым, важен URI.

| Значение .NET | Тип XML Schema |
|---|---|
| `string` | `xs:string` |
| `bool` | `xs:boolean` |
| `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong` | `xs:integer` |
| `decimal` | `xs:decimal` |
| `DateTimeOffset` | `xs:dateTime` с часовым поясом |

Отсутствующий параметр сохраняет значение по умолчанию XSLT; пустая строка передаётся явно. `null`, `DateTime`, `float`, `double` и повторные QName запрещены. Представление не зависит от культуры; значения параметров не входят в ключ кэша компиляции.

| Поле результата | Назначение |
|---|---|
| `JobId` | Заданный идентификатор или автоматически созданный GUID |
| `PageCount`, `PdfBytes` | Число страниц и размер PDF; сторонний рендерер без отчёта может вернуть `PageCount = null` |
| `QueueDuration` | Ожидание слота у планировщика |
| `InputPreparationDuration` | Подготовка входов; в worker включает родительский staging |
| `TransformDuration`, `RenderDuration`, `PublishDuration` | Крупные этапы преобразования |
| `TotalDuration` | Полное измеренное время, включая очередь при использовании планировщика |
| `StageDurations` | Подробные вложенные этапы; их нельзя все складывать с крупными этапами повторно |
| `Warnings` | Коды предупреждений встроенного FOP без текста документа |
| `Diagnostics` | Диагностики Saxon: код, этап, признак предупреждения, строка и столбец; без текста документа и путей |
| `ProfileVersion` | Версия подготовленного профиля при регистрации через соответствующую перегрузку DI |
| `EngineCounters` | Наблюдения кэшей/GC внутри worker; словарь может быть пустым |

### 3. Входные и выходные потоки

Файлы и потоки можно сочетать в одном запросе. Для XSLT из потока задайте абсолютный локальный base URI, если используются относительные include/import или изображения. URI обозначает исходное расположение шаблона, а не временный каталог worker.

```csharp
using var xml = File.OpenRead("input.xml");
using var xslt = File.OpenRead("document.xsl");
using var output = File.Create("output/stream.pdf");

var request = new ConversionRequest(
    XmlSource.FromStream(xml),
    XmlSource.FromStream(xslt, new Uri(Path.GetFullPath("document.xsl"))));
ConversionResult result = await generator.GenerateAsync(request, output);
await output.FlushAsync();
```

Входы читаются с текущей позиции без перемотки. Потоки без `Length`/`Seek` поддерживаются. Библиотека оставляет потоки вызывающего кода открытыми, включая ошибки и отмену; закрытие и `Flush` выполняет приложение. Используйте отдельные потоки для XML, XSLT и PDF. Один входной поток нельзя одновременно использовать в нескольких заданиях.

Лимит входа проверяется при чтении. После 256 КиБ по умолчанию буфер переносится во временный файл. Это ограничивает инфраструктурный буфер, но Saxon продолжает строить XML-дерево в памяти.

PDF копируется в выходной поток только после успешного рендеринга. **Запись в поток не атомарна:** ошибка записи или отмена при копировании могут оставить часть PDF. Поток не очищается и не перематывается. Для сохранения предыдущего файла используйте файловую перегрузку `GenerateAsync(request, "output/result.pdf")`: замена происходит после успешного выполнения и проверки отмены.

### 4. Регистрация в DI

В ASP.NET Core зарегистрируйте библиотеку один раз при запуске:

```csharp
using PdfGenerator;

builder.Services.AddXmlPdf();
builder.Services.AddSingleton<ReportService>();
```

Сервис получает генератор через конструктор:

```csharp
using PdfGenerator;

public sealed class ReportService(IXmlPdfGenerator generator)
{
    public Task<ConversionResult> CreateAsync(
        string xmlPath, string xsltPath, string pdfPath,
        CancellationToken cancellationToken = default)
    {
        var request = new ConversionRequest(
            XmlSource.FromFile(xmlPath), XmlSource.FromFile(xsltPath));
        return generator.GenerateAsync(request, pdfPath, cancellationToken);
    }
}
```

Для консольного приложения с DI дополнительно подключите контейнер: `dotnet add package Microsoft.Extensions.DependencyInjection --version 8.0.0`. В ASP.NET Core он уже доступен. Полный консольный пример:

```csharp
using Microsoft.Extensions.DependencyInjection;
using PdfGenerator;

var services = new ServiceCollection();
services.AddXmlPdf();
await using var provider = services.BuildServiceProvider();
IXmlPdfGenerator generator = provider.GetRequiredService<IXmlPdfGenerator>();
Directory.CreateDirectory("output");
await generator.GenerateFileAsync("input.xml", "document.xsl", "output/di.pdf");
```

DI использует singleton-сервисы и планировщик **всей цепочки**, от подготовки до публикации. Не создавайте контейнер и движки на каждый документ: иначе теряется прогрев кэшей. Для параллельных документов задавайте разные выходные пути и независимые входные потоки. Контейнер освобождайте через `DisposeAsync` после завершения работы; полученные из DI сервисы вручную не освобождайте.

При прямом создании `XmlPdfGenerator` очереди нет. Если нужен общий лимит, оберните его в `ScheduledXmlPdfGenerator`. Сам планировщик владеет внутренним генератором, а прямо созданные `SaxonFoTransformer` и `EmbeddedFopRenderer` остаются ответственностью вызывающего кода.

### 5. Выбор режима и настройка worker

| Режим | Когда использовать | Отмена |
|---|---|---|
| `InProcess` — по умолчанию | Доверенные шаблоны, небольшой объём, важна стоимость вызова | Синхронный движок завершает вычисление до освобождения слота |
| `Worker` | Нужны принудительная отмена/timeout и отдельный процесс для тяжёлого задания | Родитель останавливает процесс и дожидается выхода |

Чтобы включить worker, замените регистрацию `AddXmlPdf()` следующей. Не регистрируйте оба режима для одного `IXmlPdfGenerator` в одном контейнере.

```csharp
services.AddXmlPdf(
    PdfExecutionMode.Worker,
    workerOptions: new PdfWorkerOptions
    {
        MaxWorkers = 2,
        MemoryBudgetBytes = 2L * 1024 * 1024 * 1024,
        MaxWorkingSetBytes = 512L * 1024 * 1024,
        StartupTimeout = TimeSpan.FromSeconds(30),
        JobTimeout = TimeSpan.FromMinutes(2),
        RecycleAfterJobs = 100
    },
    schedulerOptions: new PdfSchedulerOptions
    {
        MaxActiveJobs = 2,
        MaxQueuedJobs = 8,
        QueueTimeout = TimeSpan.FromSeconds(30),
        ShutdownTimeout = TimeSpan.FromSeconds(30)
    });
```

Для ASP.NET Core вместо `services` используйте `builder.Services`. Код преобразования остаётся тем же. Worker входит в NuGet, запускается через установленный `dotnet` и не требует Java, Maven или внешнего FOP. Каждый процесс выполняет один документ, сохраняет кэши и перезапускается после заданного числа заданий. При необходимости задайте `DotnetHostPath` и записываемый `WorkingDirectory` в `PdfWorkerOptions`.

Согласуйте `MaxActiveJobs` с `MaxWorkers`; иначе часть ожидания окажется внутри пула. По умолчанию InProcess имеет два активных задания, Worker — один, оба допускают восемь ожидающих с ожиданием до 30 секунд. Полная очередь отклоняет новый запрос сразу.

`MemoryBudgetBytes` проверяет резерв `MaxWorkers × MaxWorkingSetBytes`. `MaxWorkingSetBytes` наблюдается каждые 100 мс и может быть кратковременно превышен. Это не hard limit ОС; строгий общий предел задаёт окружение, например Docker `--memory`. Не выбирайте число процессов только по CPU. Ошибочные документы автоматически не повторяются.

### 6. Ресурсы, шрифты и большие документы

Для файлового шаблона base URI определяется автоматически. По умолчанию его зависимости и изображения разрешены внутри каталога шаблона. Для ресурсов в других каталогах укажите все разрешённые корни явно:

```csharp
string templatePath = Path.GetFullPath("templates/document.xsl");
var request = new ConversionRequest(
    XmlSource.FromFile("input.xml"),
    XmlSource.FromFile(templatePath),
    resources: new LocalResourcePolicy([
        Path.GetDirectoryName(templatePath)!,
        Path.GetFullPath("images")
    ]));
await generator.GenerateAsync(request, "output/resources.pdf");
```

Это отдельный пример с вашим шаблоном из `templates`. Base URI разрешает относительный путь, а `LocalResourcePolicy` определяет, доступен ли он. `xml:base` не расширяет разрешения. Сеть, UNC, выход за разрешённые корни и символические ссылки запрещены. Явно переданные XML/XSLT являются входами; политика ограничивает их зависимости. HTTP(S)-ресурсы предварительно сохраните в разрешённый локальный каталог.

Основные лимиты по умолчанию:

| Настройка | Значение | Что ограничивает |
|---|---:|---|
| `XmlFoOptions.MaxInputBytes` | 16 МиБ | Входной XML |
| `XmlFoOptions.MaxStylesheetBytes` | 16 МиБ | Суммарный снимок XSLT с include/import |
| `XmlFoOptions.MaxOutputBytes` | 64 МиБ | Сформированный FO |
| `EmbeddedFopOptions.MaxFoBytes` / `MaxPdfBytes` | 64 / 64 МиБ | Вход FO и выход PDF |
| `EmbeddedFopOptions.MaxImageBytes` | 32 МиБ | Отдельное изображение |
| `EmbeddedFopOptions.MaxImages` | 1000 | Уникальные подготовленные изображения |
| `EmbeddedFopOptions.MaxTotalImageBytes` | 128 МиБ | Суммарные изображения |
| `EmbeddedFopOptions.MaxTemporaryBytes` | 512 МиБ | Бюджет временной подготовки/записи |

Настройки сервиса неизменяемы после создания. Переопределяйте их при регистрации, например `services.AddXmlPdf(PdfExecutionMode.InProcess, options: new EmbeddedFopOptions { MaxPdfBytes = 128L * 1024 * 1024 }, xmlOptions: new XmlFoOptions { MaxInputBytes = 32L * 1024 * 1024 })`. Поднимайте согласованно лимиты формирования и чтения FO, если увеличиваете промежуточный документ.

Для XML около 50 МиБ используйте готовые профили:

```csharp
using Microsoft.Extensions.DependencyInjection;
using PdfGenerator;

using var profile = PdfProcessingProfile.Create(
    "large",
    xmlOptions: PdfResourceProfiles.LargeXml,
    rendererOptions: PdfResourceProfiles.LargeRenderer);
var services = new ServiceCollection();
services.AddXmlPdf(profile, PdfExecutionMode.Worker,
    PdfResourceProfiles.LargeWorker, PdfResourceProfiles.LargeScheduler);
await using var provider = services.BuildServiceProvider();
var generator = provider.GetRequiredService<IXmlPdfGenerator>();
Directory.CreateDirectory("output");
await generator.GenerateFileAsync("input.xml", "document.xsl", "output/large.pdf");
```

`LargeXml` допускает 64 МиБ XML, `LargeRenderer` — 128 МиБ FO/PDF, `LargeWorker` — один процесс, наблюдаемый RSS 1 ГиБ и timeout 5 минут. `LargeScheduler` допускает одно активное и четыре ожидающих задания. Размер XML сам по себе не гарантирует достаточную память. Граничные 100 МиБ измеряются с явным увеличением лимита; замеры и бюджеты приведены в [отчёте](docs/performance.md).

Для своих шрифтов создайте профиль, передав необходимые файлы и начертания:

```csharp
using var profile = PdfProcessingProfile.Create("reports", fonts: [
    new PdfFont("fonts/ReportSans.ttf", "Report Sans"),
    new PdfFont("fonts/ReportSans-Bold.ttf", "Report Sans", Weight: 700)
]);
services.AddXmlPdf(profile, PdfExecutionMode.Worker);
```

В FO используйте `font-family="Report Sans"`, для жирного начертания — `font-weight="700"`. Файлы OpenType/TrueType проверяются и копируются в профиль; конфигурация FOP формируется автоматически. Поэтому не задавайте одновременно `rendererOptions.ConfigurationPath` при `PdfProcessingProfile.Create`. Для отдельной собственной конфигурации FOP задайте `EmbeddedFopOptions.ConfigurationPath` в обычной регистрации без подготовленного профиля.

**Профиль должен жить дольше контейнера и всех заданий.** В полном примере выше порядок объявлений обеспечивает освобождение контейнера перед профилем. При ASP.NET Core сохраняйте профиль на весь срок жизни приложения и освобождайте после остановки использующих его сервисов. Версия профиля учитывает шрифты и настройки и возвращается в `ConversionResult.ProfileVersion` при регистрации через профиль.

Пользовательские FO-шрифты работают в обоих режимах; пользовательские SVG/AWT-шрифты требуют Worker. В InProcess SVG использует встроенный DejaVu и отклоняет неизвестное семейство. Для SVG задавайте шрифт атрибутом `font-family`, а не CSS `font`/`font-family`: эти CSS-свойства запрещены.

### 7. Ошибки, отмена и очередь

Токен передаётся последним аргументом обеих перегрузок `GenerateAsync` и `GenerateFileAsync`. Для отдельного задания можно уменьшить лимит XML и задать timeout:

```csharp
using var cancellation = new CancellationTokenSource();
var request = new ConversionRequest(
    XmlSource.FromFile("input.xml"),
    XmlSource.FromFile("document.xsl"),
    settings: new ConversionSettings
    {
        MaxInputBytes = 8L * 1024 * 1024,
        Timeout = TimeSpan.FromSeconds(45)
    });

try
{
    await generator.GenerateAsync(request, "output/cancellable.pdf", cancellation.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Задание отменено.");
}
catch (ConversionQueueFullException)
{
    Console.WriteLine("Очередь заполнена: отложите запрос на стороне приложения.");
}
catch (ConversionOutputConflictException)
{
    Console.WriteLine("Этот выходной путь уже занят другим заданием.");
}
catch (TimeoutException)
{
    Console.WriteLine("Истёк срок ожидания очереди или исполнения InProcess.");
}
catch (PdfWorkerException ex)
{
    Console.WriteLine($"Ошибка worker: {ex.Kind}");
}
catch (ConversionException ex)
{
    foreach (XsltExecutionDiagnostic diagnostic in ex.Diagnostics)
    {
        Console.Error.WriteLine($"{diagnostic.Stage}: {diagnostic.Code}, строка {diagnostic.Line}, столбец {diagnostic.Column}");
    }
    Console.WriteLine($"Преобразование завершилось ошибкой на этапе {ex.Stage}.");
}
```

В этом фрагменте `generator` должен быть получен из DI или обёрнут в `ScheduledXmlPdfGenerator`, чтобы применялись очередь и timeout InProcess. Прямой `XmlPdfGenerator` не устанавливает таймер задания. Прямой `WorkerXmlPdfGenerator` применяет timeout worker, но не добавляет ограниченную внешнюю очередь. Для отмены вызовите `cancellation.Cancel()` из обработчика вашего приложения; в ASP.NET Core можно передавать токен отмены HTTP-запроса.

`ConversionSettings.MaxInputBytes` только ужесточает сервисный лимит XML; увеличить его на отдельный вызов нельзя. В Worker `Timeout` переопределяет `JobTimeout`; отсчёт исполнения начинается при обмене с уже запущенным worker, отдельно от подготовки входов, запуска процесса и очереди. В InProcess планировщик начинает таймер после получения активного слота, но синхронный Saxon/FOP нельзя принудительно прервать: фактическое завершение может оказаться позже указанного срока. Для принудительного прерывания выбирайте Worker.

| Ситуация | Исключение / стадия |
|---|---|
| Неверные аргументы | `ArgumentException`, `ArgumentNullException`, `ArgumentOutOfRangeException` |
| Очередь заполнена | `ConversionQueueFullException` |
| Долго нет активного слота | `TimeoutException` |
| Одновременная запись одного пути через планировщик | `ConversionOutputConflictException` |
| Отмена вызывающего кода | `OperationCanceledException` |
| Чтение, компиляция, преобразование, FOP, публикация | `ConversionException.Stage`: `Input`, `Compile`, `Transform`, `Render`, `Publish` |
| Запуск, обмен или авария worker | `PdfWorkerException.Kind`: `Startup`, `Protocol`, `Crash` |

`ConversionException.Diagnostics`, `TransformResult.Diagnostics` и `ConversionResult.Diagnostics`
содержат диагностики реального выполнения Saxon. Предупреждения компиляции сохраняются с шаблоном
и возвращаются при попадании в кэш; предупреждения выполнения принадлежат отдельному заданию.
Сбор ограничен 64 записями на этап; ошибка вытесняет предупреждение, если лимит заполнен.
`Line` и `Column` могут быть `null`, если Saxon не сообщил позицию. Коды пользовательских ошибок
заменяются на `SAXON_ERROR`; сообщения движка, значения XML и пути не включаются.
Worker передаёт те же поля. Предварительный аудит через `IXsltDiagnostics` остаётся отдельной проверкой шаблона.
| Timeout / наблюдаемая память worker | `PdfWorkerException.Kind`: `Timeout`, `MemoryLimit` |

Не повторяйте автоматически ошибки шаблона или лимитов. Предыдущий файловый PDF сохраняется при ошибке или отмене **до публикации**. Ошибка при копировании в Stream может оставить частичные данные. Исключения движков в `InnerException` могут содержать фрагменты документа; учитывайте это при логировании. Автоматический вывод `xsl:message`, `trace()` и error reporter Saxon отключён, значения параметров не добавляются в сообщения библиотеки.

При остановке планировщик прекращает приём, ждёт задания до `ShutdownTimeout`, затем запрашивает отмену оставшихся. Worker завершается принудительно после короткого grace period; синхронные InProcess-движки всё равно должны закончить работу. Не освобождайте прямо созданные движки, пока их задания выполняются.

### 8. Кэши, отдельные этапы и диагностика

Для повторных документов держите сервисы долго живущими. Параметры и transformer принадлежат конкретному заданию; compiled XSLT и фабрики FOP переиспользуются в ограниченных кэшах. Изменение главного XSLT, include/import, конфигурации или шрифтов обновляет соответствующий снимок. Кэши worker находятся в каждом процессе.

У прямо созданного `SaxonFoTransformer` доступны прогрев, счётчики и очистка:

```csharp
await transformer.WarmupAsync(XmlSource.FromFile("document.xsl"));
Console.WriteLine(transformer.CacheStatistics);
Console.WriteLine(renderer.CacheStatistics);
transformer.ClearTemplateCache();
```

Этот фрагмент использует движки из первого примера. Очистка не требуется перед каждым документом и не меняет файлы шаблонов. `XmlFoOptions` задаёт `TemplateCacheEntries` (32), `TemplateCacheBytes` (оценочные 128 МиБ), `TemplateCacheIdleTime` (30 минут); `EmbeddedFopOptions` — `FactoryCacheEntries` (4) и `FactoryCacheBytes` (оценочные 128 МиБ). Оценочные бюджеты кэша не являются строгими лимитами памяти процесса.

Можно выполнить этапы отдельно, чтобы проверить промежуточный FO:

```csharp
using PdfGenerator;

using var transformer = new SaxonFoTransformer();
using var renderer = new EmbeddedFopRenderer();
Directory.CreateDirectory("output");
await transformer.TransformFileAsync("input.xml", "document.xsl", "output/result.fo");
await renderer.RenderFileAsync("output/result.fo", "output/from-fo.pdf");
```

При отдельных этапах расположение FO служит базой для относительных изображений. Храните FO рядом с нужными ресурсами либо используйте `RenderWithResourcesAsync` с исходным base URI и явной политикой. Для только XML → FO DI-регистрация `AddXmlFo()` предоставляет `IXsltFoTransformer`.

Диагностика XSLT не требует XML и не выполняет шаблон. `Analyze(string)` принимает **текст**, `AnalyzeFile(string)` — путь и анализирует локальные include/import в каталоге главного шаблона:

```csharp
using PdfGenerator;

IXsltDiagnostics diagnostics = new XsltDiagnostics();
XsltDiagnosticReport report = diagnostics.AnalyzeFile("document.xsl");
Console.WriteLine($"Проверено шаблонов: {report.StylesheetsInspected}");
foreach (XsltFunctionUsage function in report.Functions)
{
    Console.WriteLine($"{{{function.NamespaceUri}}}{function.Name}: {function.Support}");
}
foreach (XsltDiagnostic issue in report.Diagnostics)
{
    Console.WriteLine($"{issue.Severity} {issue.Code}: {issue.Message}");
}
```

`AddXmlFo()` и `AddXmlPdf()` также регистрируют `IXsltDiagnostics`. Altova и незарегистрированные пользовательские расширения сейчас помечаются `Unsupported`; совместимость с ними не реализована. `Supported` означает найденную стандартную функцию или `xsl:function`, а не доказанное совпадение результатов с другим процессором. Для Saxon/EXSLT/EXPath возможен `ReviewRequired`. Анализ сообщает об ограниченных конструкциях и ресурсах, но не проверяет runtime-семантику и динамически построенные функции. Отчёт содержит имена функций, URI и относительные имена модулей.

В Demo есть команда `dotnet run --project PdfGenerator.Demo -- --diagnose-xslt path/to/document.xsl`: JSON-отчёт, код возврата `3` при ошибках. Для существующей внешней установки FOP доступен `FopProcessRenderer` с `FopOptions`; для стандартного использования достаточно встроенного рендерера.

### 9. Публикация и контейнер

Потребителю достаточно .NET 8 runtime соответствующей платформы. Сохраните весь каталог публикации: worker DLL, runtimeconfig/deps и native assets копируются через NuGet buildTransitive. Не переносите только одну DLL библиотеки. Single-file и trimming в этой preview не поддерживаются.

Для Linux glibc x64:

```powershell
dotnet publish -c Release -r linux-x64 --self-contained false -o publish
```

Если `input.xml` и `document.xsl` используются как файлы рядом с приложением, добавьте в `.csproj` потребителя:

```xml
<ItemGroup>
  <None Update="input.xml;document.xsl"
        CopyToOutputDirectory="PreserveNewest"
        CopyToPublishDirectory="PreserveNewest" />
</ItemGroup>
```

Аналогично включите свои include/import и изображения. Пример Dockerfile **потребителя** для уже опубликованного `PdfExample`:

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0-bookworm-slim
WORKDIR /app
COPY publish/ ./
USER $APP_UID
ENTRYPOINT ["dotnet", "PdfExample.dll"]
```

Пример программы пишет относительный `output/result.pdf`. Для read-only контейнера направьте рабочий каталог в `/tmp` и сделайте опубликованные входы доступными там:

```powershell
docker build -t pdf-example .
docker run --rm --read-only --network none --memory 2g --cpus 2 --tmpfs /tmp:rw,noexec,nosuid,size=512m --workdir /tmp --entrypoint /bin/sh pdf-example -c 'cp /app/input.xml /app/document.xsl /tmp/ && dotnet /app/PdfExample.dll'
```

В этом демонстрационном запуске PDF находится во временном `/tmp/output` и удаляется вместе с контейнером. Для сохранения результата смонтируйте записываемый каталог и передавайте в программе путь внутри него; копировать шаблоны необязательно, если приложение использует абсолютные пути `/app/...`. Runtime-образ не требует `apt`, Java, Maven или установки системных шрифтов. Для worker тот же установленный dotnet host доступен внутри образа.

## PNG и ICC

Перед рендерингом удаляются все блоки PNG `iCCP`. Пиксели, альфа-канал, палитра и остальные блоки сохраняются побайтово. Временная копия изображения используется в FO и удаляется после задания; исходный файл не меняется.

Обрабатываются `fo:external-graphic`, фоновые изображения FO и `svg:image` внутри FO. Поддерживаются локальные пути, `file:` URI, `xml:base`, base64 и percent-encoded data URI. Повреждённые границы PNG или CRC сохранённых блоков приводят к ошибке. Сам ICC не распаковывается, поэтому удаляется и повреждённый профиль с корректными границами блока.

HTTP(S)-изображения требуется предварительно скачать в разрешённый локальный каталог. Внешние SVG обрабатываются рекурсивно (максимальная глубина 16); PNG внутри них тоже очищаются. Активный SVG, внешние ссылки SVG вне `image`, CSS imports и внешние CSS url запрещены; `url(#fragment)` поддерживается. Встроенные data URI поддерживаются для PNG. После удаления ICC интерпретация цвета может измениться; FOP может добавить в PDF собственный стандартный sRGB-профиль.

Отдельная операция для собственного рендерера:

```csharp
byte[] cleanedPng = PngIccProfileRemover.RemoveIccProfile(pngBytes);
```

## Сборка и проверка

Требуются .NET SDK из `global.json` и доступ к NuGet/Maven при первой сборке исходников. Для контейнерных проверок нужен Docker.

```powershell
./eng/verify.ps1
./eng/verify.ps1 -Docker
```

Скрипт запускает тесты, создаёт пакеты и проверяет Demo с `PackageReference` на упакованную библиотеку.

```powershell
./eng/format.ps1 -Check
docker build -t pdfgenerator .
docker run --rm --read-only --tmpfs /tmp --network none pdfgenerator
```

Образ запускает полную цепочку с кириллицей, PNG/ICC/прозрачностью и SVG под non-root. Runtime-стадия Dockerfile содержит стандартный .NET runtime и опубликованное приложение; установки системных пакетов нет.

Пример Demo после сборки пакетов:

```powershell
dotnet run --project PdfGenerator.Demo -c Release --no-restore -- --xml test-data/basic/input.xml --xslt test-data/basic/document.xsl --out artifacts/result.pdf --pdf
```

## Структура репозитория

### Сборка из чистой копии

Нужны .NET SDK из `global.json`, PowerShell 7 (`pwsh`) и доступ к nuget.org и Maven Central при первой сборке. Docker нужен только для контейнерной проверки.

```powershell
git clone https://github.com/Dionis112/PdfGenerator.git
Set-Location PdfGenerator
pwsh -File ./eng/verify.ps1
pwsh -File ./eng/format.ps1 -Check
```

Сначала запускайте `verify.ps1`: Demo использует локально собранный NuGet-пакет, поэтому обычная сборка всего решения до упаковки библиотеки не является первым шагом. Скрипт создаёт пакеты и проверяет потребителя автоматически.

GitHub Actions выполняет проверку на Windows и сборку с тестами в Debian-контейнере. Workflow не публикует пакеты или образы. Каталоги `bin`, `obj`, `.vs` и `artifacts` создаются локально и исключены из Git; их можно удалить после завершения сборки. В `docs/benchmarks` сохранены небольшие JSON-отчёты, на которые ссылается документация.

### Каталоги

```text
PdfGenerator.Domain/             Контракты ошибок и этапы преобразования
PdfGenerator.Application/        Интерфейсы и оркестрация XML → FO → PDF
PdfGenerator.Infrastructure/     Saxon, FOP, PNG/ICC, DI и ресурсы runtime
PdfGenerator.Demo/               Консольный пример — потребитель NuGet
PdfGenerator.UnitTests/          xUnit, Moq, AutoFixture, FluentAssertions
PdfGenerator.IntegrationTests/   Реальные движки, файлы, отмена и лимиты
eng/                            Упаковка, локальный feed и проверка
test-data/                     Синтетические XML/XSLT и PNG fixture
docs/                          Архитектура и тестирование
```

Зависимости проектов: `Infrastructure → Application → Domain`. Версии пакетов централизованы в `Directory.Packages.props`; настройки сборки и стиля — в `Directory.Build.props` и `.editorconfig`.

## Ограничения preview

- Используйте доверенные шаблоны, FO, SVG и конфигурацию FOP. Рендерер не является песочницей.
- XML и XSLT принимаются из файлов или потоков. DTD, внешние функции, `document()/doc()`, `unparsed-text()` и дополнительные выходные документы запрещены. Ресурсы ограничены разрешёнными каталогами, без символических ссылок.
- Initial template/mode не входят в текущий контракт.
- В режиме InProcess Saxon и встроенный FOP выполняют синхронные вычисления: отмена не прерывает движок, а слот остаётся занятым до его завершения. Режим Worker обеспечивает принудительные отмену и timeout через завершение дочернего процесса.
- Квоты файлов, изображений и временного диска проверяются отдельно. Они не ограничивают память дерева и native-ресурсов. Worker наблюдает working set; строгий общий предел памяти задаётся окружением.
- Инициализируйте встроенный рендерер до использования других Java/AWT-рендереров в том же процессе: он настраивает Fontconfig для процесса.
- Экземпляры сервисов можно держать singleton; `Dispose` вызывайте после завершения всех заданий. Для параллельных заданий используйте разные пути результата.
- `ConversionException.Stage` и `InnerException` содержат диагностику. Исключения движков могут включать фрагменты входных данных.
- Производственные шаблоны, полный набор возможностей SVG и нагрузочные характеристики требуют отдельной проверки.

## Документация и лицензии

Собственный код проекта распространяется по [лицензии MIT](LICENSE): его можно использовать, изменять и распространять, в том числе в коммерческих проектах, с сохранением уведомления об авторских правах и текста лицензии. Сторонние библиотеки, шрифты и нативные компоненты сохраняют свои лицензии, перечисленные ниже.

- [Архитектура и runtime](docs/architecture.md)
- [План развития: API, производительность и worker](docs/development-plan.md)
- [Тестирование](docs/testing.md)
- [Результаты нагрузки и бюджеты](docs/performance.md)
- [Сторонние компоненты и лицензии](THIRD-PARTY-NOTICES.md)

Оригинальные лицензии и NOTICE включённых компонентов сохранены в `PdfGenerator.Infrastructure/Resources/Licenses` и в каталоге `licenses/` NuGet.
