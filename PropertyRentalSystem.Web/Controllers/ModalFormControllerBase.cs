using Microsoft.AspNetCore.Mvc;

namespace PropertyRentalSystem.Web.Controllers;

// Shared by controllers whose Create/Edit/Delete actions follow the modal-forms.js contract:
// a successful POST returns this instead of a redirect or a rendered view, and modal-forms.js
// treats the X-Form-Success header as the "close the modal and refresh" signal.
public abstract class ModalFormControllerBase : Controller
{
    protected IActionResult FormSuccess()
    {
        Response.Headers["X-Form-Success"] = "true";
        return NoContent();
    }
}
