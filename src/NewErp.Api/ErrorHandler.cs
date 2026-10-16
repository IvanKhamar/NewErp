using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using NewErp.Application.Common;
using NewErp.Domain.Common;

namespace NewErp.Api;

/// <summary>Перетворює винятки бізнес-логіки на зрозумілі HTTP-відповіді у форматі ProblemDetails.</summary>
public sealed class ErrorHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title, detail) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Не знайдено", exception.Message),
            DomainException => (StatusCodes.Status400BadRequest, "Порушено бізнес-правило", exception.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Дані щойно змінив хтось інший, повторіть дію", exception.Message),
            // Тіло запиту не прочиталось (зламаний JSON, не UTF-8, не те поле). Це помилка клієнта, а не сервера.
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Некоректний запит",
                "Не вдалося прочитати тіло запиту: перевірте, що це правильний JSON у кодуванні UTF-8."),
            _ => (0, "", ""),
        };

        if (status == 0)
            return false; // інші помилки обробить стандартний обробник (500)

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail },
        });
    }
}