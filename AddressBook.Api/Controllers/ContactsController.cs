using AddressBook.Api.Dtos;
using AddressBook.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AddressBook.Api.Controllers;

/// <summary>Address book contacts.</summary>
[ApiController]
[Route("api/contacts")]
public class ContactsController(IContactService contacts) : ControllerBase
{
    /// <summary>Gets one contact by id.</summary>
    /// <response code="200">The contact.</response>
    /// <response code="404">No contact has this id.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContactDto>> GetById(int id) =>
        await contacts.GetAsync(id) is { } contact ? Ok(contact) : NotFound();

    /// <summary>Creates a contact.</summary>
    /// <response code="201">Created. The Location header points to the new contact.</response>
    /// <response code="400">Validation failed.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ContactDto>> Create(CreateContactDto dto)
    {
        var created = await contacts.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
