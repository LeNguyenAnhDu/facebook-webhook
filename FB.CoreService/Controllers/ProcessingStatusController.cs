using FB.CoreService.Services;
using Microsoft.AspNetCore.Mvc;

namespace FB.CoreService.Controllers;

[ApiController]
[Route("api/events")]
public sealed class ProcessingStatusController : ControllerBase
{
    [HttpGet("{eventId}")]
    public IActionResult Get(string eventId, [FromServices] IEventProcessingStatusStore store)
    {
        return store.TryGet(eventId, out var snapshot) ? Ok(snapshot) : NotFound();
    }
}
