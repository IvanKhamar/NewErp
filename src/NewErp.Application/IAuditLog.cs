namespace NewErp.Application;

/// <summary>
/// Журнал важливих змін: хто що зробив із грошима чи доступами. Лише номери й суми, без персональних даних і паролів.
/// Реалізація пише в логер NewErp.Audit (Application завантажується в браузер, тож сама нічого не логує).
/// </summary>
public interface IAuditLog
{
    void Record(string text);
}
