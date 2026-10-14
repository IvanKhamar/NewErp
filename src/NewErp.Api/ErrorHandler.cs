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
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Не знайдено"),
            DomainException => (StatusCodes.Status400BadRequest, "Порушено бізнес-правило"),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Дані щойно змінив хтось інший, повторіть дію"),
            _ => (0, ""),
        };

        if (status == 0)
            return false; // інші помилки обробить стандартний обробник (500)

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = exception.Message },
        });
    }
}