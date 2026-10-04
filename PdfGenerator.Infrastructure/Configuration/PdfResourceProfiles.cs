namespace PdfGenerator;

/// <summary>
/// <para>Measured starting settings for ordinary documents and XML around fifty MiB; complex templates require their own budget verification.</para>
/// <para>Измеренные начальные настройки обычных документов и XML около пятидесяти МиБ; сложные шаблоны требуют отдельной проверки бюджета.</para>
/// </summary>
public static class PdfResourceProfiles
{
    /// <summary><para>Ordinary XML limits, preserving the original sixteen MiB input contract.</para><para>Лимиты обычного XML, сохраняющие исходный предел входа шестнадцать МиБ.</para></summary>
    public static XmlFoOptions NormalXml { get; } = new();
    /// <summary><para>Large XML limits with one active transformation and a sixty-four MiB input ceiling.</para><para>Лимиты большого XML с одним активным преобразованием и пределом входа шестьдесят четыре МиБ.</para></summary>
    public static XmlFoOptions LargeXml { get; } = new() { MaxInputBytes = 64L * 1024 * 1024, MaxConcurrency = 1 };
    /// <summary><para>Large FO/PDF limits with a one GiB temporary-file budget.</para><para>Лимиты большого FO/PDF с бюджетом временных файлов один ГиБ.</para></summary>
    public static EmbeddedFopOptions LargeRenderer
    {
        get;
    } = new()
    {
        MaxConcurrency = 1,
        MaxFoBytes = 128L * 1024 * 1024,
        MaxPdfBytes = 128L * 1024 * 1024,
        MaxTotalImageBytes = 256L * 1024 * 1024,
        MaxTemporaryBytes = 1024L * 1024 * 1024
    };
    /// <summary><para>One large worker with one GiB observed memory and a two GiB pool reservation budget.</para><para>Один большой worker с наблюдаемой памятью один ГиБ и общим бюджетом резерва два ГиБ.</para></summary>
    public static PdfWorkerOptions LargeWorker { get; } = new() { MaxWorkers = 1, MaxWorkingSetBytes = 1024L * 1024 * 1024, JobTimeout = TimeSpan.FromMinutes(5) };
    /// <summary><para>One active large conversion, four queued jobs, and a bounded queue wait.</para><para>Одно активное большое преобразование, четыре ожидающих задания и ограниченное ожидание.</para></summary>
    public static PdfSchedulerOptions LargeScheduler { get; } = new() { MaxActiveJobs = 1, MaxQueuedJobs = 4, QueueTimeout = TimeSpan.FromMinutes(2) };
}
