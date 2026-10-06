namespace PdfGenerator.UnitTests;


/// <summary>
/// <para>Runs the SchedulerTests validation scenario.</para>
/// <para>Выполняет проверку сценария SchedulerTests.</para>
/// </summary>
public sealed class SchedulerTests
{

    /// <summary>
    /// <para>Runs the BoundsQueueRejectsConflictsAndCancelsBeforeExecution validation scenario.</para>
    /// <para>Выполняет проверку сценария BoundsQueueRejectsConflictsAndCancelsBeforeExecution.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task BoundsQueueRejectsConflictsAndCancelsBeforeExecution()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inner = new BlockingGenerator(entered, release);
        await using var scheduler = new ScheduledXmlPdfGenerator(inner, new()
        {
            MaxActiveJobs = 1,
            MaxQueuedJobs = 1
        });
        var request = new ConversionRequest(XmlSource.FromFile("input.xml"), XmlSource.FromFile("template.xsl"));
        Task<ConversionResult> first = scheduler.GenerateAsync(request, "first.pdf");
        await entered.Task;
        await Assert.ThrowsAsync<ConversionOutputConflictException>(() => scheduler.GenerateAsync(request, "first.pdf"));
        using var cancellation = new CancellationTokenSource();
        Task<ConversionResult> second = scheduler.GenerateAsync(request, "second.pdf", cancellation.Token);
        await Assert.ThrowsAsync<ConversionQueueFullException>(() => scheduler.GenerateAsync(request, "third.pdf"));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        release.SetResult();
        await first;
        Assert.Equal(1, inner.Calls);
    }

    /// <summary><para>Times out a queued job and returns its path and admission reservation.</para><para>Завершает ожидающее задание по тайм-ауту и освобождает путь и место в очереди.</para></summary>
    /// <returns><para>Asynchronous test completion.</para><para>Асинхронное завершение теста.</para></returns>
    [Fact]
    public async Task QueueTimeoutReleasesReservationAndSuccessfulJobsReportProfile()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inner = new BlockingGenerator(entered, release);
        await using var scheduler = new ScheduledXmlPdfGenerator(inner, new()
        {
            MaxActiveJobs = 1,
            MaxQueuedJobs = 1,
            QueueTimeout = TimeSpan.FromMilliseconds(100),
            ProfileVersion = "version"
        });
        var request = new ConversionRequest(XmlSource.FromFile("input.xml"), XmlSource.FromFile("template.xsl"));
        Task<ConversionResult> first = scheduler.GenerateAsync(request, "queue-first.pdf");
        await entered.Task;
        await Assert.ThrowsAsync<TimeoutException>(() => scheduler.GenerateAsync(request, "queue-second.pdf"));
        release.SetResult();
        await first;
        ConversionResult second = await scheduler.GenerateAsync(request, "queue-second.pdf");
        Assert.Equal("version", second.ProfileVersion);
        Assert.Equal(2, inner.Calls);
    }

    /// <summary><para>Stops admission, cancels a job after shutdown grace, and awaits its completion.</para><para>Прекращает приём, отменяет задание после периода завершения и ждёт его остановки.</para></summary>
    /// <returns><para>Asynchronous test completion.</para><para>Асинхронное завершение теста.</para></returns>
    [Fact]
    public async Task ShutdownCancelsAfterGraceAndWaitsForJobs()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var scheduler = new ScheduledXmlPdfGenerator(new BlockingGenerator(entered, release), new()
        {
            ShutdownTimeout = TimeSpan.FromMilliseconds(100)
        });
        var request = new ConversionRequest(XmlSource.FromFile("input.xml"), XmlSource.FromFile("template.xsl"));
        Task<ConversionResult> job = scheduler.GenerateAsync(request, "shutdown.pdf");
        await entered.Task;
        Task shutdown = scheduler.DisposeAsync().AsTask();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => scheduler.GenerateAsync(request, "shutdown-new.pdf"));
        await shutdown;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => job);
        scheduler.Dispose();
    }

    /// <summary>
    /// <para>Runs the BlockingGenerator validation scenario.</para>
    /// <para>Выполняет проверку сценария BlockingGenerator.</para>
    /// </summary>
    /// <param name="entered"><para>The entered argument.</para><para>Аргумент entered.</para></param>
    /// <param name="release"><para>The release argument.</para><para>Аргумент release.</para></param>
    private sealed class BlockingGenerator(TaskCompletionSource entered, TaskCompletionSource release) : IXmlPdfGenerator
    {

        /// <summary>
        /// <para>Gets or defines the Calls value used by BlockingGenerator.</para>
        /// <para>Возвращает или определяет значение Calls, используемое BlockingGenerator.</para>
        /// </summary>
        public int Calls
        {
            get; private set;
        }

        /// <summary>
        /// <para>Converts XML and XSLT to PDF. File publication is atomic; copying a completed PDF to a caller-owned stream is not atomic and leaves that stream open.</para>
        /// <para>Преобразует XML и XSLT в PDF. Публикация файла атомарна; копирование готового PDF в поток вызывающего кода не атомарно и оставляет поток открытым.</para>
        /// </summary>
        /// <param name="xmlPath"><para>Local XML input path.</para><para>Путь локального входного XML.</para></param>
        /// <param name="xsltPath"><para>Local stylesheet path; relative dependencies require a permitted base directory.</para><para>Путь локального шаблона; относительным зависимостям требуется разрешённый базовый каталог.</para></param>
        /// <param name="pdfPath"><para>PDF destination, atomically replaced only after successful rendering.</para><para>Путь PDF, атомарно заменяемый после успешного рендеринга.</para></param>
        /// <param name="cancellationToken"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
        /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
        /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
        public Task GenerateFileAsync(string xmlPath, string xsltPath, string pdfPath, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <summary>
        /// <para>Converts XML and XSLT to PDF. File publication is atomic; copying a completed PDF to a caller-owned stream is not atomic and leaves that stream open.</para>
        /// <para>Преобразует XML и XSLT в PDF. Публикация файла атомарна; копирование готового PDF в поток вызывающего кода не атомарно и оставляет поток открытым.</para>
        /// </summary>
        /// <param name="request"><para>Immutable job inputs and settings; its streams remain caller-owned.</para><para>Неизменяемые входы и настройки задания; его потоки принадлежат вызывающему коду.</para></param>
        /// <param name="pdfPath"><para>PDF destination, atomically replaced only after successful rendering.</para><para>Путь PDF, атомарно заменяемый после успешного рендеринга.</para></param>
        /// <param name="cancellationToken"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
        /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
        /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
        /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
        public async Task<ConversionResult> GenerateAsync(ConversionRequest request, string pdfPath, CancellationToken cancellationToken = default)
        {
            Calls++;
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return new(request.JobId, 1, 10, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, []);
        }
    }
}
