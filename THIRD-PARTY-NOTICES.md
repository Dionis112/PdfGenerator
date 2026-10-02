# Сторонние компоненты

Оригинальные лицензии и NOTICE сохраняются в `PdfGenerator.Infrastructure/Resources/Licenses` и включаются в NuGet в каталог `licenses/`.

## .NET и шрифт

- SaxonCS-HE 13.0.0: условия поставки находятся в NuGet-пакете производителя. Его LICENSE.txt содержит общие условия SaxonCS; перед распространением нужно учитывать именно условия используемой поставки HE.
- IKVM 8.16.1: транзитивная NuGet-зависимость; лицензия поставляется вместе с IKVM.
- DejaVu Sans: включён в библиотеку; лицензия — `DejaVu-LICENSE.txt`.
- Тестовые Moq, AutoFixture и FluentAssertions не входят в зависимости runtime-пакета.

## Нативные библиотеки Linux x64

Файлы получены из пакетов Debian 12 amd64. Используются базовые libc/libm/libz стандартного образа .NET. Дополнительной установки графических пакетов в runtime-образ не требуется.

| Debian-пакет | Версия | Лицензия в репозитории |
|---|---|---|
| libfreetype6 | 2.12.1+dfsg-5+deb12u4 | libfreetype6.txt |
| libfontconfig1 | 2.14.1-4 | libfontconfig1.txt |
| libpng16-16 | 1.6.39-2+deb12u5 | libpng16-16.txt |
| libbrotli1 | 1.0.9-2+b6 | libbrotli1.txt |
| libexpat1 | 2.5.0-1+deb12u4 | libexpat1.txt |

FreeType используется по FreeType Project License. Обработка шрифтов частично основана на FreeType. При обновлении бинарных файлов обновляйте лицензии и повторяйте контейнерные проверки.

## Java-компоненты, скомпилированные в .NET

Ниже перечислены Maven-зависимости, фактически разрешённые при сборке FOP 2.11. Лицензии инструментов сборки и посторонних Java-приложений в поставку не включены.

- `org.apache.xmlgraphics:batik-shared-resources:1.19`
- `org.apache.xmlgraphics:batik-i18n:1.19`
- `org.apache.xmlgraphics:batik-constants:1.19`
- `commons-logging:commons-logging:1.3.0`
- `commons-io:commons-io:2.17.0`
- `org.apache.xmlgraphics:batik-util:1.19`
- `org.apache.xmlgraphics:xmlgraphics-commons:2.11`
- `xml-apis:xml-apis-ext:1.3.04`
- `org.apache.xmlgraphics:batik-xml:1.19`
- `org.apache.xmlgraphics:batik-awt-util:1.19`
- `xml-apis:xml-apis:1.4.01`
- `org.apache.xmlgraphics:batik-ext:1.19`
- `org.apache.xmlgraphics:batik-css:1.19`
- `org.apache.xmlgraphics:batik-parser:1.19`
- `org.apache.xmlgraphics:batik-dom:1.19`
- `org.apache.xmlgraphics:batik-svg-dom:1.19`
- `org.apache.xmlgraphics:batik-anim:1.19`
- `org.apache.xmlgraphics:batik-script:1.19`
- `org.apache.xmlgraphics:batik-gvt:1.19`
- `org.bouncycastle:bcprov-jdk15to18:1.78.1`
- `org.apache.xmlgraphics:batik-svggen:1.19`
- `org.apache.xmlgraphics:batik-bridge:1.19`
- `javax.media:jai-core:1.1.3`
- `org.bouncycastle:bcutil-jdk15to18:1.78.1`
- `org.apache.pdfbox:pdfbox-io:3.0.3`
- `org.apache.ant:ant:1.10.14`
- `com.thoughtworks.qdox:qdox:1.12`
- `org.apache.xmlgraphics:fop-util:2.11`
- `org.apache.xmlgraphics:batik-transcoder:1.19`
- `com.sun.media:jai-codec:1.1.3`
- `org.bouncycastle:bcpkix-jdk15to18:1.78.1`
- `org.apache.pdfbox:fontbox:3.0.3`
- `jakarta.servlet:jakarta.servlet-api:5.0.0`
- `org.apache.xmlgraphics:fop-events:2.11`
- `org.apache.xmlgraphics:batik-codec:1.19`
- `org.apache.xmlgraphics:batik-extension:1.19`
- `org.apache.xmlgraphics:fop-core:2.11`
- `org.apache.xmlgraphics:fop:2.11`


В репозитории пока нет файла LICENSE для собственного кода проекта.
