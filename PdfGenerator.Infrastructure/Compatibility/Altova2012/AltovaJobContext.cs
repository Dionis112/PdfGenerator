using System.Globalization;
using System.Numerics;
using Saxon.Api;
using Saxon.Hej.trans;

namespace PdfGenerator;

/// <summary><para>Owns counters, dynamic-expression budget and temporary resources for one synchronous transformation.</para><para>Владеет счётчиками, бюджетом выражений и временными ресурсами одного синхронного преобразования.</para></summary>
internal sealed class AltovaJobContext : IDisposable
{
    /// <summary><para>Flows with execution, never with the shared executable.</para><para>Следует за выполнением, а не за общим шаблоном.</para></summary>
    private static readonly AsyncLocal<AltovaJobContext?> active = new();
    /// <summary><para>Previous scope for nested host execution.</para><para>Предыдущая область вложенного выполнения.</para></summary>
    private readonly AltovaJobContext? previous;
    /// <summary><para>Immutable evaluation limits.</para><para>Неизменяемые пределы выполнения.</para></summary>
    internal readonly Altova2012Options Options;
    /// <summary><para>Series indexed by identifier.</para><para>Серии по идентификатору.</para></summary>
    private readonly Dictionary<string, (BigInteger Next, string Group)> counters = new(StringComparer.Ordinal);
    /// <summary><para>Expression cache with call-site static context in its key.</para><para>Кэш выражений с контекстом места вызова в ключе.</para></summary>
    private readonly Dictionary<(object Site, string Expression, int Arity), XPathExecutable> expressions = [];
    /// <summary><para>Current job temporary directory, created on demand.</para><para>Временный каталог задания, создаваемый по необходимости.</para></summary>
    private string? directory;
    /// <summary><para>Evaluation counters.</para><para>Счётчики выполнения.</para></summary>
    private int evaluations, depth, characters;

    /// <summary><para>Begins an isolated scope.</para><para>Открывает изолированную область.</para></summary>
    /// <param name="options"><para>Evaluation budgets.</para><para>Бюджеты выполнения.</para></param>
    internal AltovaJobContext(Altova2012Options options)
    {
        Options = options;
        previous = active.Value;
        active.Value = this;
    }

    /// <summary><para>Gets the current transformation scope.</para><para>Возвращает область текущего преобразования.</para></summary>
    internal static AltovaJobContext Current => active.Value ?? throw new XPathException("Altova function requires a transformation scope.", "FOER0000");

    /// <summary><para>Checks total calls, length and nesting before evaluating.</para><para>Проверяет число вызовов, длину и вложенность перед вычислением.</para></summary>
    /// <param name="expression"><para>Dynamic XPath.</para><para>Динамический XPath.</para></param>
    internal void EnterEvaluation(string expression)
    {
        if (++evaluations > Options.MaxEvaluations || expression.Length > Options.MaxExpressionCharacters || depth >= Options.MaxEvaluationDepth)
        {
            throw new XPathException("Dynamic XPath budget exceeded.", "FOER0000");
        }
        depth++;
    }

    /// <summary><para>Leaves an evaluation, including after failure.</para><para>Завершает вычисление, включая ошибку.</para></summary>
    internal void LeaveEvaluation() => depth--;

    /// <summary><para>Caches executable expressions only, never arguments or results.</para><para>Кэширует только выражения без аргументов и результатов.</para></summary>
    /// <param name="site"><para>Immutable call-site context identity.</para><para>Идентичность неизменяемого контекста вызова.</para></param>
    /// <param name="expression"><para>XPath text.</para><para>Текст XPath.</para></param>
    /// <param name="arity"><para>Argument count.</para><para>Число аргументов.</para></param>
    /// <param name="compile"><para>Compiler for this context.</para><para>Компилятор данного контекста.</para></param>
    /// <returns><para>Compiled XPath.</para><para>Скомпилированный XPath.</para></returns>
    internal XPathExecutable GetExpression(object site, string expression, int arity, Func<XPathExecutable> compile)
    {
        (object Site, string Expression, int Arity) key = (site, expression, arity);
        if (expressions.TryGetValue(key, out XPathExecutable? result))
        {
            return result;
        }
        if (expressions.Count >= Options.MaxCachedExpressions || (long)characters + expression.Length > Options.MaxTotalExpressionCharacters)
        {
            throw new XPathException("Dynamic XPath cache budget exceeded.", "FOER0000");
        }
        result = compile();
        expressions.Add(key, result);
        characters += expression.Length;
        return result;
    }

    /// <summary><para>Generates one number and resets when the group changes, including A to B to A.</para><para>Выдаёт номер и сбрасывает серию при изменении группы, включая A → B → A.</para></summary>
    /// <param name="id"><para>Series identifier.</para><para>Идентификатор серии.</para></param>
    /// <param name="start"><para>Initial integer.</para><para>Начальное число.</para></param>
    /// <param name="increment"><para>Integer increment.</para><para>Шаг.</para></param>
    /// <param name="group"><para>Current group.</para><para>Текущая группа.</para></param>
    /// <returns><para>The generated integer.</para><para>Выданное целое число.</para></returns>
    internal XdmValue Next(string id, BigInteger start, BigInteger increment, string group)
    {
        BigInteger value = counters.TryGetValue(id, out (BigInteger Next, string Group) counter) && counter.Group == group ? counter.Next : start;
        counters[id] = (value + increment, group);
        return new XdmAtomicValue(value.ToString(CultureInfo.InvariantCulture), XdmAtomicType.BuiltInAtomicType(QName.XS_INTEGER));
    }

    /// <summary><para>Resets a series; the next call supplies its starting value.</para><para>Сбрасывает серию; следующий вызов задаёт начальное значение.</para></summary>
    /// <param name="id"><para>Series identifier.</para><para>Идентификатор серии.</para></param>
    internal void Reset(string id) => counters.Remove(id);

    /// <summary><para>Returns an existing native absolute directory path with a trailing separator.</para><para>Возвращает существующий абсолютный путь каталога с завершающим разделителем.</para></summary>
    internal string TempFolder
    {
        get
        {
            if (directory is null)
            {
                directory = Path.Combine(Path.GetTempPath(), "xmlpdf-altova-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
            }
            return directory + Path.DirectorySeparatorChar;
        }
    }

    /// <summary><para>Restores the scope and removes all job-local state.</para><para>Восстанавливает область и удаляет состояние задания.</para></summary>
    public void Dispose()
    {
        active.Value = previous;
        expressions.Clear();
        counters.Clear();
        if (directory is not null)
        {
            Directory.Delete(directory, true);
        }
    }
}
