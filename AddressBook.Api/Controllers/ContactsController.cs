using AddressBook.Api.Dtos;
using AddressBook.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AddressBook.Api.Controllers;

/// <summary>Address book contacts.</summary>
[ApiController]
[Route("api/contacts")]
public class ContactsController(IContactService contacts) : ControllerBase
{
    /// <summary>Lists contacts with search, tag and favorite filters, sorting and paging.</summary>
    /// <response code="200">One page of contacts.</response>
    /// <response code="400">A query value is invalid.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ContactDto>>> List([FromQuery] ContactQuery query) =>
        Ok(await contacts.ListAsync(query));

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

    /// <summary>Updates a contact by replacing every editable field (full update).</summary>
    /// <response code="200">The updated contact.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="404">No contact has this id.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContactDto>> Update(int id, UpdateContactDto dto) =>
        await contacts.UpdateAsync(id, dto) is { } contact ? Ok(contact) : NotFound();

    /// <summary>Edits a contact: only the fields present in the body change.</summary>
    /// <response code="200">The edited contact.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="404">No contact has this id.</response>
    [HttpPatch("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContactDto>> Patch(int id, PatchContactDto dto) =>
        await contacts.PatchAsync(id, dto) is { } contact ? Ok(contact) : NotFound();

    /// <summary>Deletes a contact.</summary>
    /// <response code="204">Deleted.</response>
    /// <response code="404">No contact has this id.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id) =>
        await contacts.DeleteAsync(id) ? NoContent() : NotFound();

    /// <summary>Deletes several contacts at once. Unknown ids are ignored.</summary>
    /// <response code="200">How many contacts were deleted.</response>
    /// <response code="400">The id list is empty or too long.</response>
    [HttpPost("bulk-delete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BulkDeleteResult>> BulkDelete(BulkDeleteDto dto) =>
        Ok(new BulkDeleteResult(await contacts.BulkDeleteAsync(dto.Ids)));

    /// <summary>Marks a contact as favorite. Calling it again changes nothing.</summary>
    /// <response code="204">The contact is a favorite.</response>
    /// <response code="404">No contact has this id.</response>
    [HttpPut("{id:int}/favorite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Favorite(int id) =>
        await contacts.SetFavoriteAsync(id, true) ? NoContent() : NotFound();

    /// <summary>Removes a contact from favorites. Calling it again changes nothing.</summary>
    /// <response code="204">The contact is not a favorite.</response>
    /// <response code="404">No contact has this id.</response>
    [HttpDelete("{id:int}/favorite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unfavorite(int id) =>
        await contacts.SetFavoriteAsync(id, false) ? NoContent() : NotFound();
}
