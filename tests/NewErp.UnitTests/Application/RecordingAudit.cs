namespace NewErp.UnitTests.Application;

/// <summary>Журнал аудиту для тестів: запам'ятовує рядки замість логера.</summary>
internal sealed class RecordingAudit : NewErp.Application.IAuditLog
{
    public List<string> Lines { get; } = [];

    public void Record(string text) => Lines.Add(text);
}
