using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AlwaysOk.Controllers;

[ApiController]
[Route("{*path}")]
public class AnyController : ControllerBase
{
    private readonly ILogger<AnyController> _logger;

    public AnyController(ILogger<AnyController> logger) => _logger = logger;

    [HttpGet]
    [HttpPost]
    [HttpPut]
    [HttpDelete]
    [HttpPatch]
    [HttpHead]
    [HttpOptions]
    public async Task<ActionResult> Action()
    {
        var body = string.Empty;
        try
        {
            using var reader = new StreamReader(Request.Body);
            body = await reader.ReadToEndAsync();
        }
        catch
        { }

        _logger.LogInformation("""
            Incoming Request
            Scheme: {scheme}
            Method: {method} 
            Path: {path}
            Query: {query}
            Headers: {headers}
            Body: {body}
            """,
            Request.Scheme, Request.Method, Request.Path, Request.QueryString,
            JsonSerializer.Serialize(Request.Headers),
            string.IsNullOrEmpty(body) ? "n/a" : body);

        return Ok("AlwaysOk");
    }
}
