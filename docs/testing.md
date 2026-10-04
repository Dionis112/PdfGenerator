# Тестирование

## Полная проверка

```powershell
./eng/verify.ps1
./eng/verify.ps1 -DependencyFeed "$env:USERPROFILE/.nuget/packages"
./eng/verify.ps1 -Docker
./eng/format.ps1 -Check
```

verify проверяет двуязычные XML summary/param/returns/exception, запускает модульные и интеграционные тесты, собирает три NuGet-пакета, восстанавливает Demo в отдельный новый каталог пакетов и выполняет self-test. Отдельный каталог исключает использование старого содержимого при одинаковой preview-версии. DependencyFeed позволяет читать уже скачанные сторонние пакеты из локального cache. В Docker потребитель также использует свежий каталог и опубликованные пакеты.

Текущий состав: 6 модульных и 74 интеграционных сценария. Модульные проверки подтверждают передачу аргументов, отмену, очистку, ограничения очереди, конфликт выходного пути, queue timeout, освобождение reservations и graceful shutdown. Интеграционные проверки используют настоящие Saxon/FOP.

## Покрытие

- Старый файловый API и запросы, QName, string/bool/integer/decimal/DateTimeOffset, отсутствие/пустая строка и параллельная изоляция параметров.
- Stream без Seek/Length, переход буфера на диск, превышение лимитов, ошибки чтения/записи, отмена, caller-owned lifetime и base URI/include.
- Версия/снимок шрифта профиля, кириллица, страницы, предупреждения, локальная политика ресурсов, запрет сети, DTD и внешних XSLT-операций.
- PNG/iCCP/CRC, альфа-канал, base64/percent URI, локальные картинки, nested SVG, активный SVG и небезопасные CSS-ссылки.
- Общая компиляция, изменение транзитивного include/import, bounded cache, восстановление после ошибки, отмена отдельного ожидателя, изменение конфигурации FOP и параллельное использование фабрики.
- Квоты PDF во время записи, число изображений, временный диск, сохранение предыдущего результата и очистка промежуточных файлов.
- Реальный worker: переиспользование процесса, внешний crash и восстановление, жесткий timeout и отмена бесконечного XSLT, typed Stream protocol, превышение наблюдаемой памяти, startup circuit, oversized/incompatible protocol, recycle и пользовательский SVG-шрифт.

Self-test NuGet-потребителя проверяет FO/PDF, Stream+параметры+include, число страниц/байты и bundled worker. PNG с повреждённым ICC и прозрачностью, SVG с текстом и Gaussian blur выполняются без внешнего FOP.

## Контейнер

```powershell
docker build -t pdfgenerator .
docker run --rm --read-only --tmpfs /tmp:rw,noexec,nosuid,size=512m --network none --memory 2g --cpus 4 pdfgenerator --self-test --pdf --output /tmp/smoke
```

Build запускает тесты, пакует NuGet и делает framework-dependent publish linux-x64. Runtime — стандартный .NET 8 Debian 12/glibc x64, non-root, без apt/Java/Maven. Worker получает граф зависимостей потребителя после publish, поскольку IKVM RID assets размещаются иначе, чем при обычном build. Runtime не требует сети. Single-file/trimming и другие RID не заявлены.

## Нагрузка и критерии

```powershell
./eng/benchmark.ps1
./eng/benchmark.ps1 -Mode worker -Sizes 10,50 -Concurrency 1,2,4
./eng/benchmark.ps1 -Scenario baseline
./eng/benchmark.ps1 -Scenario mixed -Sizes 10 -Concurrency 2 -Iterations 20
./eng/benchmark.ps1 -Scenario stream -Mode worker -Sizes 10 -Concurrency 1
./eng/benchmark-container.ps1
```

Стенд генерирует XML 10/25/50/100 МиБ, grouping/sorting/keys, многостраничную таблицу, 20 повторных PNG с ICC и SVG. Фактический размер записан в JSON. Scenario baseline создаёт новые экземпляры адаптеров на задание; это контроль без переиспользования, сохраняющий итоговую streaming-подготовку FO. Исторический замер до оптимизаций хранится отдельно.

JSON содержит runtime/OS/движки/шрифт, настройки, структуру корпуса, холодный и прогретые вызовы, этапы, queue wait, p50/p95, throughput, peak RSS/heap, GC/allocations, временные байты и кэши. Для worker peak RSS суммирует parent+children; peak managed heap относится к parent, child GC/caches доступны в EngineCounters. Выборка каждые 50 мс является наблюдением и может пропустить короткий пик. Сборщик файлов допускает исчезновение каталогов между чтениями.

Полные отчёты и бюджеты — [performance.md](performance.md). Реальных обезличенных шаблонов пока нет; согласованные профильные бюджеты относятся к синтетическому корпусу, а не произвольным XML/XSLT.

## Независимая проверка PDF

Для проверки текста и изображений используется pypdf, для визуального контроля — Poppler pdftoppm. Сравниваются страницы, извлечённый текст и данные изображений после холодного старта, прогрева, контрольного запуска, worker и Linux; metadata/даты PDF исключены из побайтовой гарантии. Пример сравнения и JSON-результат находятся в docs/benchmarks.

```powershell
pdftoppm -scale-to 1200 -singlefile -png result.pdf artifacts/preview
```

Для производственных шаблонов добавляйте свои эталонные PDF, проверку текста, изображений, переносов и layout. Публичный корпус синтетический.
