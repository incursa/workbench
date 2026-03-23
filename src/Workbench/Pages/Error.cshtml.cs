using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Workbench.Pages;

public class ErrorModel : PageModel
{
    public string RequestId { get; set; } = string.Empty;

    public bool ShowRequestId => !string.IsNullOrWhiteSpace(RequestId);

    public void OnGet()
    {
        RequestId = HttpContext.TraceIdentifier;
    }
}
