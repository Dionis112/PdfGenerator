# PdfGenerator

.NET 8-библиотека для преобразования **XML + XSLT 2.0 → XSL-FO → PDF**.

NuGet-пакет: `Company.XmlFo`, текущая версия — `0.1.0-preview.2`.

Рендерер поставляется вместе с пакетом: в стандартный Docker-образ .NET не нужно устанавливать Java, Apache FOP, системные шрифты или графические библиотеки. Для XSLT используется SaxonCS-HE, для PDF — Apache FOP через IKVM.

## Возможности

- XSLT 2.0, относительные `xsl:include` и `xsl:import`.
- Кириллица со встроенным DejaVu Sans.
- PNG из локального файла или data URI, включая прозрачность.
- Удаление ICC-профиля PNG без перекодирования пикселей и изменения исходного изображения.
- SVG, включая текст, прозрачность и фильтры, проверенные синтетическим корпусом.
- Атомарная публикация результата: предыдущий файл сохраняется при ошибке или отмене до публикации.
- Настраиваемые лимиты, параллелизм и регистрация сервисов через DI.

## Поддерживаемая среда

| Среда | Поддержка |
|---|---|
| Windows x64, .NET 8 | Проверена |
| Debian 12 / Linux glibc x64, .NET 8 | Проверена в Docker |
| Alpine / musl, ARM64, macOS | Не поддерживаются этой preview |

Нужны записываемые временный каталог и каталог результата. Для Linux публикуйте приложение с RID `linux-x64`. Пакет содержит нативные компоненты, которые копируются при публикации приложения.

## Установка

Пакет пока не опубликован на nuget.org. Соберите исходники командой `./eng/verify.ps1` и разместите **все три** пакета из `artifacts/packages` в локальном или частном NuGet-feed:

- `Company.XmlFo`
- `PdfGenerator.Application`
- `PdfGenerator.Domain`

Приложение подключает только `Company.XmlFo`; остальные зависимости NuGet восстанавливает автоматически.

```powershell
dotnet add package Company.XmlFo --version 0.1.0-preview.2 --source ./artifacts/packages
```

Укажите в `--source` путь к своему feed. Maven SDK используется только для сборки исходников библиотеки: потребитель получает готовые .NET-сборки FOP/Batik.

## Использование

```csharp
using Company.XmlFo;

using var transformer = new SaxonFoTransformer();
using var renderer = new EmbeddedFopRenderer();
var generator = new XmlPdfGenerator(transformer, renderer);

await generator.GenerateFileAsync(
    "input.xml",
    "document.xsl",
    "result.pdf",
    cancellationToken);
```

В приложении с Microsoft Dependency Injection:

```csharp
using Company.XmlFo;

builder.Services.AddXmlPdf();

// Получите IXmlPdfGenerator через конструктор своего сервиса.
```

Для получения только XSL-FO используйте `SaxonFoTransformer.TransformFileAsync()` или регистрацию `AddXmlFo()`.

Настройки встроенного рендерера:

```csharp
builder.Services.AddXmlPdf(new EmbeddedFopOptions
{
    MaxConcurrency = 2,
    MaxPdfBytes = 64 * 1024 * 1024,
    MaxFoBytes = 64 * 1024 * 1024,
    MaxImageBytes = 32 * 1024 * 1024
});
```

`ConfigurationPath` позволяет передать собственную конфигурацию FOP. По умолчанию используется встроенный DejaVu Sans. Для существующей установки внешнего FOP сохранён адаптер `FopProcessRenderer` с `FopOptions`.

## PNG и ICC

Перед рендерингом удаляются все блоки PNG `iCCP`. Пиксели, альфа-канал, палитра и остальные блоки сохраняются побайтово. Временная копия изображения используется в FO и удаляется после задания; исходный файл не меняется.

Обрабатываются `fo:external-graphic`, фоновые изображения FO и `svg:image` внутри FO. Поддерживаются локальные пути, `file:` URI, `xml:base`, base64 и percent-encoded data URI. Повреждённые границы PNG или CRC сохранённых блоков приводят к ошибке. Сам ICC не распаковывается, поэтому удаляется и повреждённый профиль с корректными границами блока.

HTTP(S)-изображения требуется предварительно скачать в локальный файл. Ссылки внутри отдельных внешних SVG не обходятся рекурсивно. После удаления ICC интерпретация цвета может измениться; FOP может добавить в PDF собственный стандартный sRGB-профиль.

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
dotnet run --project PdfGenerator.Demo -c Release --no-restore -- --xmltest-data/basic/input.xml --xslttest-data/basic/document.xsl --out artifacts/result.pdf --pdf
```

## Структура репозитория

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
- XML и XSLT должны быть локальными файлами. DTD, внешние функции, `document()/doc()`, `unparsed-text()` и дополнительные выходные документы запрещены. Включения ограничены каталогом главного XSLT, без символических ссылок.
- Параметры XSLT, initial template/mode, stream API и кэш шаблонов пока не реализованы.
- Saxon и встроенный FOP выполняют синхронные вычисления. Отмена проверяется до начала работы и перед публикацией результата; во время вычисления слот остаётся занятым. Жёсткий timeout предусмотрен только у внешнего FOP.
- Лимиты файлов не ограничивают суммарную память, общий объём изображений и время вычислений.
- Инициализируйте встроенный рендерер до использования других Java/AWT-рендереров в том же процессе: он настраивает Fontconfig для процесса.
- Экземпляры сервисов можно держать singleton; `Dispose` вызывайте после завершения всех заданий. Для параллельных заданий используйте разные пути результата.
- `ConversionException.Stage` и `InnerException` содержат диагностику. Исключения движков могут включать фрагменты входных данных.
- Производственные шаблоны, полный набор возможностей SVG и нагрузочные характеристики требуют отдельной проверки.

## Документация и лицензии

- [Архитектура и runtime](docs/architecture.md)
- [Тестирование](docs/testing.md)
- [Сторонние компоненты и лицензии](THIRD-PARTY-NOTICES.md)

Оригинальные лицензии и NOTICE включённых компонентов сохранены в `PdfGenerator.Infrastructure/Resources/Licenses` и в каталоге `licenses/` NuGet.
