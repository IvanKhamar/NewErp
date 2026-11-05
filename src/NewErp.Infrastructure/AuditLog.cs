using Microsoft.Extensions.Logging;

using NewErp.Application;

namespace NewErp.Infrastructure;

/// <summary>Окрема категорія логів, щоб рядки журналу легко було відфільтрувати й зберігати довше за решту.</summary>
public sealed partial class AuditLog(ILoggerFactory loggers) : IAuditLog
{
    public const string Category = "NewErp.Audit";

    private readonly ILogger _logger = loggers.CreateLogger(Category);

    public void Record(string text) => Write(_logger, text);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Text}")]
    private static partial void Write(ILogger logger, string text);
}
