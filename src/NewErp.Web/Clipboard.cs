using Microsoft.JSInterop;

namespace NewErp.Web;

/// <summary>Копіювання тексту в буфер обміну браузера (наприклад, текст замовлення для месенджера).</summary>
public sealed class Clipboard(IJSRuntime js)
{
    public ValueTask CopyAsync(string text) => js.InvokeVoidAsync("navigator.clipboard.writeText", text);
}
