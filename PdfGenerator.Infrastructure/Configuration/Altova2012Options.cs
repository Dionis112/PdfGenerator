namespace PdfGenerator;

/// <summary><para>Enables the documented AltovaXML 2012 general functions with bounded, job-local evaluation.</para><para>Включает общие функции AltovaXML 2012 с ограниченным динамическим выполнением в пределах задания.</para></summary>
public sealed record Altova2012Options
{
    /// <summary><para>Implementation identity, also checked by workers and included in cache keys.</para><para>Версия реализации для проверки worker и ключей кэша.</para></summary>
    public const string ImplementationVersion = "altova2012-general-v1";

    /// <summary><para>Maximum characters in one dynamic XPath.</para><para>Максимум символов динамического XPath.</para></summary>
    public int MaxExpressionCharacters { get; init; } = 16 * 1024;

    /// <summary><para>Maximum evaluate invocations per transformation.</para><para>Максимум вызовов evaluate в преобразовании.</para></summary>
    public int MaxEvaluations { get; init; } = 1024;

    /// <summary><para>Maximum nested evaluate calls.</para><para>Максимум вложенных вызовов evaluate.</para></summary>
    public int MaxEvaluationDepth { get; init; } = 16;

    /// <summary><para>Maximum distinct compiled dynamic expressions per job.</para><para>Максимум разных скомпилированных динамических выражений задания.</para></summary>
    public int MaxCachedExpressions { get; init; } = 128;

    /// <summary><para>Maximum aggregate characters in compiled dynamic expressions per job.</para><para>Максимум суммарных символов скомпилированных динамических выражений задания.</para></summary>
    public int MaxTotalExpressionCharacters { get; init; } = 256 * 1024;

    /// <summary><para>Validates evaluation budgets before admitting a job.</para><para>Проверяет бюджеты до приёма задания.</para></summary>
    internal void Validate()
    {
        if (MaxExpressionCharacters <= 0 || MaxEvaluations <= 0 || MaxEvaluationDepth <= 0 || MaxEvaluationDepth > 64 ||
            MaxCachedExpressions <= 0 || MaxTotalExpressionCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Altova2012Options));
        }
    }
}
