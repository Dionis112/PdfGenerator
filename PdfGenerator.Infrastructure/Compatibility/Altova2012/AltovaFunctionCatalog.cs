using System.Globalization;
using System.Numerics;
using System.Text;
using System.Xml;
using Saxon.Api;
using Saxon.Hej.trans;

namespace PdfGenerator;

/// <summary><para>Single source of function availability for registration and diagnostics.</para><para>Единый источник доступности функций для регистрации и диагностики.</para></summary>
internal static class AltovaFunctionCatalog
{
    /// <summary><para>Altova's namespace, independent of the stylesheet prefix.</para><para>Пространство имён Altova, независимое от префикса шаблона.</para></summary>
    internal const string Namespace = "http://www.altova.com/xslt-extensions";
    /// <summary><para>Functions implemented from documentation; original-engine parity is not yet confirmed.</para><para>Функции по документации; сравнение с оригинальным движком пока не подтверждено.</para></summary>
    internal static readonly string[] Names = ["distinct-nodes", "evaluate", "generate-auto-number", "reset-auto-number", "encode-for-rtf", "get-temp-folder"];

    /// <summary><para>Returns supported argument counts.</para><para>Возвращает поддерживаемое число аргументов.</para></summary>
    /// <param name="name"><para>Local function name.</para><para>Локальное имя функции.</para></param>
    /// <returns><para>Minimum and maximum arity, or an unavailable marker.</para><para>Минимальная и максимальная арность либо признак отсутствия.</para></returns>
    internal static (int Min, int Max) Arity(string name) => name switch
    {
        "evaluate" => (1, 33),
        "distinct-nodes" or "reset-auto-number" => (1, 1),
        "generate-auto-number" => (4, 4),
        "encode-for-rtf" => (3, 3),
        "get-temp-folder" => (0, 0),
        _ => (-1, -1)
    };
}

/// <summary><para>Defines Altova calls with focus and side effects declared to the optimizer.</para><para>Определяет вызовы Altova с объявлением зависимости от контекста и побочных эффектов.</para></summary>
/// <param name="name"><para>Function name.</para><para>Имя функции.</para></param>
internal sealed class AltovaFunctionDefinition(string name) : ExtensionFunctionDefinition
{
    /// <summary><para>Registered QName.</para><para>Регистрируемый QName.</para></summary>
    public override QName FunctionName => new(AltovaFunctionCatalog.Namespace, name);
    /// <summary><para>Minimum argument count.</para><para>Минимум аргументов.</para></summary>
    public override int MinimumNumberOfArguments => AltovaFunctionCatalog.Arity(name).Min;
    /// <summary><para>Maximum argument count; evaluate allows 32 parameters.</para><para>Максимум аргументов; evaluate допускает 32 параметра.</para></summary>
    public override int MaximumNumberOfArguments => AltovaFunctionCatalog.Arity(name).Max;
    /// <summary><para>Types required by the documented function contracts.</para><para>Типы аргументов по документированным контрактам.</para></summary>
    public override XdmSequenceType[] ArgumentTypes => name switch
    {
        "evaluate" => [Atomic("string"), XdmSequenceType.AnySequenceType],
        "generate-auto-number" => [Atomic("string"), Atomic("integer"), Atomic("integer"), Atomic("string")],
        "reset-auto-number" => [Atomic("string")],
        "encode-for-rtf" => [Atomic("string", '?'), Atomic("boolean"), Atomic("boolean")],
        "get-temp-folder" => [],
        _ => [XdmSequenceType.AnySequenceType]
    };
    /// <summary><para>Dynamic XPath inherits the caller's focus.</para><para>Динамический XPath наследует контекст вызова.</para></summary>
    public override bool DependsOnFocus => name is "evaluate" or "distinct-nodes";
    /// <summary><para>State and nested dynamic calls must not be folded or reordered as pure calls.</para><para>Вызовы с состоянием и динамические вложенные вызовы нельзя сворачивать как чистые.</para></summary>
    public override bool HasSideEffects => name is "evaluate" or "generate-auto-number" or "reset-auto-number" or "get-temp-folder";
    /// <summary><para>Returns the declared output type.</para><para>Возвращает тип результата.</para></summary>
    /// <param name="argumentTypes"><para>Actual static argument types.</para><para>Фактические статические типы аргументов.</para></param>
    /// <returns><para>Result type.</para><para>Тип результата.</para></returns>
    public override XdmSequenceType ResultType(XdmSequenceType[] argumentTypes) => name switch
    {
        "generate-auto-number" => Atomic("integer"),
        "encode-for-rtf" or "get-temp-folder" => Atomic("string"),
        _ => XdmSequenceType.AnySequenceType
    };
    /// <summary><para>Creates a call-site object containing only immutable static context.</para><para>Создаёт объект места вызова только с неизменяемым статическим контекстом.</para></summary>
    /// <returns><para>Function call.</para><para>Вызов функции.</para></returns>
    public override ExtensionFunctionCall MakeFunctionCall() => new AltovaFunctionCall(name);
    /// <summary><para>Builds an atomic sequence type.</para><para>Создаёт атомарный тип последовательности.</para></summary>
    /// <param name="type"><para>XML Schema type.</para><para>Тип XML Schema.</para></param>
    /// <param name="occurrence"><para>Cardinality.</para><para>Мощность последовательности.</para></param>
    /// <returns><para>Sequence type.</para><para>Тип последовательности.</para></returns>
    private static XdmSequenceType Atomic(string type, char occurrence = ' ') => new(XdmAtomicType.BuiltInAtomicType(new QName("http://www.w3.org/2001/XMLSchema", type)), occurrence);
}

/// <summary><para>Executes a call with the caller's namespaces, base URI and focus.</para><para>Выполняет вызов с пространствами имён, базовым URI и контекстом вызывающего выражения.</para></summary>
/// <param name="name"><para>Function name.</para><para>Имя функции.</para></param>
internal sealed class AltovaFunctionCall(string name) : ExtensionFunctionCall
{
    /// <summary><para>Copied namespace bindings.</para><para>Копия привязок пространств имён.</para></summary>
    private readonly Dictionary<string, string> namespaces = new(StringComparer.Ordinal);
    /// <summary><para>Static base URI.</para><para>Статический базовый URI.</para></summary>
    private Uri? baseUri;
    /// <summary><para>Default collation used by deep-equal.</para><para>Сопоставление строк по умолчанию для deep-equal.</para></summary>
    private string? collation;

    /// <summary><para>Copies only the needed static values; never retains the compiler context.</para><para>Копирует необходимые значения, не удерживая контекст компилятора.</para></summary>
    /// <param name="context"><para>Call-site static context.</para><para>Статический контекст места вызова.</para></param>
    public override void SupplyStaticContext(StaticContext context)
    {
        baseUri = context.BaseUri;
        collation = context.Implementation.getDefaultCollationName();
        using IEnumerator<string> prefixes = context.Implementation.getNamespaceResolver().iteratePrefixes();
        while (prefixes.MoveNext())
        {
            string prefix = prefixes.Current;
            string? uri = context.GetNamespaceForPrefix(prefix);
            if (uri is not null)
            {
                namespaces[prefix] = uri;
            }
        }
        namespaces[""] = context.GetNamespaceForPrefix("") ?? "";
    }

    /// <summary><para>Dispatches a registered general function.</para><para>Выполняет зарегистрированную общую функцию.</para></summary>
    /// <param name="arguments"><para>Typed argument sequences.</para><para>Типизированные последовательности аргументов.</para></param>
    /// <param name="context"><para>Dynamic caller context.</para><para>Динамический контекст вызова.</para></param>
    /// <returns><para>XDM output preserving original nodes.</para><para>Результат XDM с сохранением исходных узлов.</para></returns>
    public override XdmValue Call(XdmValue[] arguments, DynamicContext context)
    {
        AltovaJobContext job = AltovaJobContext.Current;
        return name switch
        {
            "evaluate" => Evaluate(arguments, context, job),
            "distinct-nodes" => Distinct(arguments[0], context, job),
            "generate-auto-number" => job.Next(arguments[0][0].StringValue,
                BigInteger.Parse(arguments[1][0].StringValue, CultureInfo.InvariantCulture),
                BigInteger.Parse(arguments[2][0].StringValue, CultureInfo.InvariantCulture), arguments[3][0].StringValue),
            "reset-auto-number" => Reset(job, arguments[0][0].StringValue),
            "get-temp-folder" => new XdmAtomicValue(job.TempFolder),
            "encode-for-rtf" => new XdmAtomicValue(Encode(arguments[0].Empty ? "" : arguments[0][0].StringValue,
                ((XdmAtomicValue)arguments[1][0]).AsBoolean(), ((XdmAtomicValue)arguments[2][0]).AsBoolean())),
            _ => throw new XPathException("Unknown compatibility function.", "XPST0017")
        };
    }

    /// <summary><para>Resets a series and returns an empty sequence; parity of the reset result remains unconfirmed.</para><para>Сбрасывает серию и возвращает пустую последовательность; возврат reset пока не сверён с Altova.</para></summary>
    /// <param name="job"><para>Current scope.</para><para>Текущая область.</para></param>
    /// <param name="id"><para>Series identifier.</para><para>Идентификатор серии.</para></param>
    /// <returns><para>Empty sequence.</para><para>Пустая последовательность.</para></returns>
    private static XdmValue Reset(AltovaJobContext job, string id)
    {
        job.Reset(id);
        return XdmEmptySequence.Instance;
    }

    /// <summary><para>Creates a compiler with the copied static context; stylesheet functions and schema types are outside this profile.</para><para>Создаёт компилятор с копией контекста; функции шаблона и схемные типы вне текущего профиля.</para></summary>
    /// <param name="processor"><para>Restricted shared processor.</para><para>Ограниченный общий процессор.</para></param>
    /// <param name="parameters"><para>Number of parameter variables.</para><para>Число переменных параметров.</para></param>
    /// <returns><para>Job-local XPath compiler.</para><para>Компилятор XPath задания.</para></returns>
    private XPathCompiler Compiler(Processor processor, int parameters)
    {
        XPathCompiler compiler = processor.NewXPathCompiler();
        compiler.XPathLanguageVersion = "2.0";
        compiler.BaseUri = baseUri;
        compiler.WarningHandler = _ => { };
        foreach ((string prefix, string uri) in namespaces)
        {
            compiler.DeclareNamespace(prefix, uri);
        }
        if (collation is not null)
        {
            compiler.DeclareDefaultCollation(collation);
        }
        for (int i = 1; i <= parameters; i++)
        {
            compiler.DeclareVariable(new QName("", "p" + i));
        }
        return compiler;
    }

    /// <summary><para>Evaluates with typed parameters and the original positional focus.</para><para>Вычисляет с типизированными параметрами и исходным позиционным контекстом.</para></summary>
    /// <param name="arguments"><para>Expression and parameter values.</para><para>Выражение и значения параметров.</para></param>
    /// <param name="context"><para>Caller focus.</para><para>Контекст вызова.</para></param>
    /// <param name="job"><para>Evaluation budgets and cache.</para><para>Бюджеты и кэш выполнения.</para></param>
    /// <returns><para>Materialized XDM result.</para><para>Материализованный результат XDM.</para></returns>
    private XdmValue Evaluate(XdmValue[] arguments, DynamicContext context, AltovaJobContext job)
    {
        string expression = arguments[0][0].StringValue;
        job.EnterEvaluation(expression);
        try
        {
            XPathExecutable executable = job.GetExpression(this, expression, arguments.Length,
                () => Compiler(context.Processor, arguments.Length - 1).Compile(expression));
            XPathSelector selector = executable.Load();
            for (int i = 1; i < arguments.Length; i++)
            {
                selector.SetVariable(new QName("", "p" + i), arguments[i]);
            }
            if (context.ContextItem is not null)
            {
                selector.ContextItem = context.ContextItem;
                // SaxonCS 13 API exposes only ContextItem; this pinned internal bridge preserves position()/last().
                selector.Implementation.getXPathContextObject().setCurrentIterator(context.Implementation.getCurrentIterator());
            }
            selector.XmlDocumentResolver = _ => throw new XmlException("Dynamic XPath resources are disabled.");
            selector.TextResolver = _ => throw new XmlException("Dynamic XPath text resources are disabled.");
            return selector.Evaluate();
        }
        catch (SaxonApiException error)
        {
            // Never propagate expression text or source data in compatibility error messages.
            string code = error.ErrorCode?.LocalName ?? "FOER0000";
            throw new XPathException("Dynamic XPath evaluation failed.", code);
        }
        finally
        {
            job.LeaveEvaluation();
        }
    }

    /// <summary><para>Uses Saxon's deep-equal, retaining the first original node in input order.</para><para>Использует deep-equal Saxon и сохраняет первый исходный узел в порядке входа.</para></summary>
    /// <param name="nodes"><para>Input nodes.</para><para>Входные узлы.</para></param>
    /// <param name="context"><para>Restricted processor.</para><para>Ограниченный процессор.</para></param>
    /// <param name="job"><para>Expression cache.</para><para>Кэш выражений.</para></param>
    /// <returns><para>Distinct original nodes.</para><para>Уникальные исходные узлы.</para></returns>
    private XdmValue Distinct(XdmValue nodes, DynamicContext context, AltovaJobContext job)
    {
        var retained = new List<XdmItem>();
        XPathExecutable executable = job.GetExpression(this, "deep-equal($p1,$p2)", 2,
            () => Compiler(context.Processor, 2).Compile("deep-equal($p1,$p2)"));
        XPathSelector selector = executable.Load();
        foreach (XdmItem node in nodes)
        {
            if (node is not XdmNode)
            {
                throw new XPathException("distinct-nodes requires nodes.", "XPTY0004");
            }
            bool duplicate = false;
            foreach (XdmItem previous in retained)
            {
                selector.SetVariable(new QName("", "p1"), node);
                selector.SetVariable(new QName("", "p2"), previous);
                if (selector.EffectiveBooleanValue())
                {
                    duplicate = true;
                    break;
                }
            }
            if (!duplicate)
            {
                retained.Add(node);
            }
        }
        return new XdmValue(retained);
    }

    /// <summary><para>Escapes RTF syntax and UTF-16 Unicode units with optional whitespace preservation.</para><para>Экранирует RTF и Unicode UTF-16 с необязательным сохранением пробелов.</para></summary>
    /// <param name="text"><para>Input string.</para><para>Входная строка.</para></param>
    /// <param name="whitespace"><para>Preserve spaces and tabs.</para><para>Сохранять пробелы и табуляцию.</para></param>
    /// <param name="newlines"><para>Preserve line breaks.</para><para>Сохранять переносы строк.</para></param>
    /// <returns><para>RTF fragment.</para><para>Фрагмент RTF.</para></returns>
    private static string Encode(string text, bool whitespace, bool newlines)
    {
        var output = new StringBuilder();
        bool space = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }
                if (newlines)
                {
                    output.Append("\\line ");
                    space = false;
                    continue;
                }
                c = ' ';
            }
            if (c is ' ' or '\t')
            {
                if (whitespace)
                {
                    output.Append(c == '\t' ? "\\tab " : "\\u32?");
                }
                else if (!space)
                {
                    output.Append(' ');
                }
                space = true;
                continue;
            }
            space = false;
            if (c is '{' or '}' or '\\')
            {
                output.Append('\\').Append(c);
            }
            else if (c > 127)
            {
                output.Append("\\u").Append(unchecked((short)c).ToString(CultureInfo.InvariantCulture)).Append('?');
            }
            else
            {
                output.Append(c);
            }
        }
        return output.ToString();
    }
}
