namespace NewErp.Domain.Common;

/// <summary>Порушення бізнес-правила. Api перетворює його на відповідь 400.</summary>
public class DomainException(string message) : Exception(message);