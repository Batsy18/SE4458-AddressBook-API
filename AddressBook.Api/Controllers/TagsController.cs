using AddressBook.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AddressBook.Api.Controllers;

/// <summary>Tags used by contacts (for the "All Tags" filter).</summary>
[ApiController]
[Route("api/tags")]
public class TagsController(IContactService contacts) : ControllerBase
{
    /// <summary>Lists every tag in use, without duplicates, sorted A–Z.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetAll() =>
        Ok(await contacts.GetTagsAsync());
}
