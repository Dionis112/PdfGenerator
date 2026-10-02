# Тестирование

## Автоматические проверки

```powershell
./eng/verify.ps1
./eng/verify.ps1 -Docker
```

Скрипт запускает модульные и интеграционные тесты, упаковывает все три библиотеки, восстанавливает Demo из локального feed в отдельный каталог NuGet и выполняет полную цепочку XML → FO → PDF.

Для повторных проверок можно указать уже заполненный каталог NuGet в `-DependencyFeed`. Это сохраняет отдельный каталог потребителя и устраняет повторное скачивание больших IKVM-пакетов:

```powershell
./eng/verify.ps1 -DependencyFeed "$env:USERPROFILE/.nuget/packages"
```

Тестовый стек: xUnit 2.9.3, Test SDK 17.14.1, Moq 4.20.72, AutoFixture.AutoMoq 4.18.1 и FluentAssertions 8.10.0. Они не входят в зависимости runtime-пакета. Версии централизованно заданы в `Directory.Packages.props`.

Модульные тесты проверяют оркестрацию, передачу аргументов и отмены, очистку временного FO и отсутствие вызова рендерера после ошибки преобразования. Интеграционные тесты используют реальные Saxon и встроенный FOP, а также проверяют файловые ограничения, включения XSLT, PNG/ICC, повреждённые данные и отмену внешнего процесса.

## Форматирование

`.editorconfig` задаёт собственные правила проекта на основе Roslyn-настроек, совместимых с C# 12 и .NET SDK 8. Для правил, проверяемых сборкой, задана явная severity, поскольку SDK 8 не всегда применяет severity из значения style-option.

```powershell
./eng/format.ps1
./eng/format.ps1 -Check
```

Источник настроек: [Microsoft: code style rule options](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/code-style-rule-options).

Скрипт отдельно запускает whitespace и выбранные C# style-правила. Это обходит ошибку SDK 8 при применении некоторых изменений метаданных связанных файлов и сохраняет общий публичный namespace `Company.XmlFo`.

## Docker

```powershell
docker build -t pdfgenerator .
docker run --rm --read-only --tmpfs /tmp --network none pdfgenerator
```

Build-стадия запускает тесты, создаёт NuGet и публикует Demo с RID `linux-x64`. Runtime-стадия использует стандартный .NET 8 Debian 12 slim без установки системных пакетов.

Demo self-test проверяет основной и пустой XML, сохранение результата после ошибки, PNG из файла и data URI с повреждённым ICC, неизменность исходного изображения и SVG с текстом, прозрачностью и Gaussian blur.

Проверенный корпус синтетический. Для выпуска с производственными шаблонами нужны отдельные эталонные PDF и нагрузочные измерения.

## Независимая проверка PDF

Poppler нужен только для проверки результата, а не для работы библиотеки. В отдельной среде с установленным Poppler:

```powershell
dotnet run --project PdfGenerator.Demo -c Release --no-restore -- --self-test --pdf --verify-tools --data test-data --output artifacts/pdf-verification
pdftoppm -scale-to 1200 -singlefile -png artifacts/pdf-verification/images.pdf artifacts/pdf-verification/preview
```

`--verify-tools` проверяет извлечённую кириллицу и наличие alpha-масок, сравнивает список изображений с PDF из PNG без ICC. Для проверки самих пикселей извлеките картинки обоих PDF через `pdfimages -png` и сравните соответствующие файлы побайтово.
