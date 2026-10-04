namespace PdfGenerator;


/// <summary>
/// <para>Resolves only local resources within explicitly permitted roots and rejects network paths and symbolic links.</para>
/// <para>Разрешает локальные ресурсы внутри явно разрешённых корней, отклоняя сетевые пути и символические ссылки.</para>
/// </summary>
public sealed class LocalResourcePolicy
{

    /// <summary>
    /// <para>Gets or defines the AllowedRoots value used by LocalResourcePolicy.</para>
    /// <para>Возвращает или определяет значение AllowedRoots, используемое LocalResourcePolicy.</para>
    /// </summary>
    public IReadOnlyList<string> AllowedRoots
    {
        get;
    }


    /// <summary>
    /// <para>Initializes LocalResourcePolicy with an immutable snapshot of service settings.</para>
    /// <para>Создаёт LocalResourcePolicy со снимком настроек сервиса.</para>
    /// </summary>
    /// <param name="allowedRoots"><para>The allowedRoots argument.</para><para>Аргумент allowedRoots.</para></param>
    /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
    public LocalResourcePolicy(IEnumerable<string> allowedRoots)
    {
        ArgumentNullException.ThrowIfNull(allowedRoots);
        string[] roots = allowedRoots.Select(Path.GetFullPath).Distinct().ToArray();
        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
            {
                throw new DirectoryNotFoundException(root);
            }
            ValidateLocal(new Uri(root));
            RejectLinks(root);
        }
        AllowedRoots = Array.AsReadOnly(roots);
    }


    /// <summary>
    /// <para>Normalizes a file URI, checks permitted roots and rejects symbolic links before returning the local path.</para>
    /// <para>Нормализует файловый URI, проверяет разрешённые корни и отклоняет символические ссылки перед возвратом локального пути.</para>
    /// </summary>
    /// <param name="uri"><para>The uri argument.</para><para>Аргумент uri.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    public string Resolve(Uri uri)
    {
        ValidateLocal(uri);
        string path = Path.GetFullPath(uri.LocalPath);
        if (!AllowedRoots.Any(root => IsWithin(root, path)))
        {
            throw new IOException("Resource is outside the permitted directories.");
        }
        RejectLinks(path);
        return path;
    }


    /// <summary>
    /// <para>Executes IsWithin as part of the LocalResourcePolicy conversion workflow.</para>
    /// <para>Выполняет IsWithin в цепочке преобразования LocalResourcePolicy.</para>
    /// </summary>
    /// <param name="root"><para>The root argument.</para><para>Аргумент root.</para></param>
    /// <param name="path"><para>The path argument.</para><para>Аргумент path.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    private static bool IsWithin(string root, string path)
    {
        string relative = Path.GetRelativePath(root, path);
        return !Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }


    /// <summary>
    /// <para>Executes ValidateLocal as part of the LocalResourcePolicy conversion workflow.</para>
    /// <para>Выполняет ValidateLocal в цепочке преобразования LocalResourcePolicy.</para>
    /// </summary>
    /// <param name="uri"><para>The uri argument.</para><para>Аргумент uri.</para></param>
    private static void ValidateLocal(Uri uri)
    {
        if (!uri.IsAbsoluteUri || !uri.IsFile || uri.IsUnc)
        {
            throw new IOException("Only local file resources are permitted.");
        }
    }


    /// <summary>
    /// <para>Executes RejectLinks as part of the LocalResourcePolicy conversion workflow.</para>
    /// <para>Выполняет RejectLinks в цепочке преобразования LocalResourcePolicy.</para>
    /// </summary>
    /// <param name="path"><para>The path argument.</para><para>Аргумент path.</para></param>
    private static void RejectLinks(string path)
    {
        for (FileSystemInfo? item = new FileInfo(path); item is not null; item = item is FileInfo file ? file.Directory : ((DirectoryInfo)item).Parent)
        {
            if ((item.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Symbolic links in resource paths are not permitted.");
            }
        }
    }
}
