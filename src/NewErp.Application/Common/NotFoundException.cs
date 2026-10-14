namespace NewErp.Application.Common;

/// <summary>Шуканого запису немає. Api перетворює його на відповідь 404.</summary>
public class NotFoundException(string message) : Exception(message);