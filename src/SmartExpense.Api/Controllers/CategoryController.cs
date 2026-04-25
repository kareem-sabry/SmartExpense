using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartExpense.Application.Dtos.Category;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Constants;

namespace SmartExpense.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[Authorize(Roles = IdentityRoleConstants.User)]
public class CategoryController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoryController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    /// <summary>
    ///     Returns all categories belonging to the authenticated user.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the request if the client disconnects.</param>
    /// <returns>A list of all categories for the authenticated user.</returns>
    /// <response code="200">Categories retrieved successfully.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<CategoryReadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<CategoryReadDto>>> GetAll(
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var categories = await _categoryService.GetAllAsync(userId, cancellationToken);
        return Ok(categories);
    }

    /// <summary>
    ///     Returns a single category by its ID, scoped to the authenticated user.
    /// </summary>
    /// <param name="id">The unique identifier of the category.</param>
    /// <param name="cancellationToken">Token to cancel the request if the client disconnects.</param>
    /// <returns>The matching category.</returns>
    /// <response code="200">Category found and returned.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    /// <response code="404">No category with the given ID exists for this user.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CategoryReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryReadDto>> GetById(int id,
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var category = await _categoryService.GetByIdAsync(id, userId, cancellationToken);
        return Ok(category);
    }

    /// <summary>
    ///     Creates a new category for the authenticated user.
    ///     Category names must be unique per user.
    /// </summary>
    /// <param name="dto">The category data to persist.</param>
    /// <param name="cancellationToken">Token to cancel the request if the client disconnects.</param>
    /// <returns>The newly created category, including its generated ID.</returns>
    /// <response code="201">Category created. Location header points to the new resource.</response>
    /// <response code="400">Validation failed (e.g. missing or invalid fields).</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    /// <response code="409">A category with the same name already exists for this user.</response>
    [HttpPost]
    [ProducesResponseType(typeof(CategoryReadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryReadDto>> Create(CategoryCreateDto dto,
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var category = await _categoryService.CreateAsync(dto, userId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = category.Id }, category);
    }

    /// <summary>
    ///     Updates an existing category that belongs to the authenticated user.
    ///     Only the owner of the category may modify it.
    ///     Category names must remain unique per user.
    /// </summary>
    /// <param name="id">The ID of the category to update.</param>
    /// <param name="dto">The updated category data.</param>
    /// <param name="cancellationToken">Token to cancel the request if the client disconnects.</param>
    /// <returns>The updated category.</returns>
    /// <response code="200">Category updated successfully.</response>
    /// <response code="400">Validation failed (e.g. missing or invalid fields).</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    /// <response code="403">The authenticated user does not own this category.</response>
    /// <response code="404">No category with the given ID exists for this user.</response>
    /// <response code="409">A category with the same name already exists for this user.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(CategoryReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryReadDto>> Update(int id, CategoryUpdateDto dto,
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var category = await _categoryService.UpdateAsync(id, dto, userId, cancellationToken);
        return Ok(category);
    }

    /// <summary>
    ///     Permanently deletes a category that belongs to the authenticated user.
    ///     Only the owner of the category may delete it.
    ///     This operation is irreversible.
    /// </summary>
    /// <param name="id">The ID of the category to delete.</param>
    /// <param name="cancellationToken">Token to cancel the request if the client disconnects.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Category deleted successfully.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    /// <response code="403">The authenticated user does not own this category.</response>
    /// <response code="404">No category with the given ID exists for this user.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id,
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _categoryService.DeleteAsync(id, userId, cancellationToken);
        return NoContent();
    }
}