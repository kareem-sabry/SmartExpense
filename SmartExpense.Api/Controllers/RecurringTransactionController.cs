using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartExpense.Application.Dtos.RecurringTransaction;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Constants;

namespace SmartExpense.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[Authorize(Roles = IdentityRoleConstants.User)]
public class RecurringTransactionController : ControllerBase
{
    private readonly IRecurringTransactionService _recurringTransactionService;

    public RecurringTransactionController(IRecurringTransactionService recurringTransactionService)
    {
        _recurringTransactionService = recurringTransactionService;
    }

    /// <summary>
    ///     Returns all recurring transactions for the authenticated user,
    ///     optionally filtered by their active status.
    /// </summary>
    /// <param name="isActive">
    ///     When provided, filters results to active (<c>true</c>) or inactive (<c>false</c>) recurring
    ///     transactions.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the request if the client disconnects.</param>
    /// <returns>A list of recurring transactions matching the filter.</returns>
    /// <response code="200">Recurring transactions retrieved successfully.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<RecurringTransactionReadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<RecurringTransactionReadDto>>> GetAll(
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var recurring = await _recurringTransactionService.GetAllAsync(userId, isActive, cancellationToken);
        return Ok(recurring);
    }

    /// <summary>
    ///     Returns a single recurring transaction by its ID, scoped to the authenticated user.
    /// </summary>
    /// <param name="id">The unique identifier of the recurring transaction.</param>
    /// <param name="cancellationToken">Token to cancel the request if the client disconnects.</param>
    /// <returns>The matching recurring transaction.</returns>
    /// <response code="200">Recurring transaction found and returned.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    /// <response code="404">No recurring transaction with the given ID exists for this user.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(RecurringTransactionReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecurringTransactionReadDto>> GetById(int id,
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var recurring = await _recurringTransactionService.GetByIdAsync(id, userId, cancellationToken);
        return Ok(recurring);
    }

    /// <summary>
    ///     Creates a new recurring transaction for the authenticated user.
    ///     The referenced category must belong to the user and be active.
    /// </summary>
    /// <param name="dto">The recurring transaction data to persist.</param>
    /// <param name="cancellationToken">Token to cancel the request if the client disconnects.</param>
    /// <returns>The newly created recurring transaction, including its generated ID.</returns>
    /// <response code="201">Recurring transaction created. Location header points to the new resource.</response>
    /// <response code="400">Validation failed (e.g. missing fields or invalid recurrence settings).</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    /// <response code="404">The specified category does not exist for this user.</response>
    [HttpPost]
    [ProducesResponseType(typeof(RecurringTransactionReadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecurringTransactionReadDto>> Create(
        RecurringTransactionCreateDto dto,
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var recurring = await _recurringTransactionService.CreateAsync(dto, userId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = recurring.Id }, recurring);
    }

    /// <summary>
    ///     Updates an existing recurring transaction that belongs to the authenticated user.
    /// </summary>
    /// <param name="id">The ID of the recurring transaction to update.</param>
    /// <param name="dto">The updated recurring transaction data.</param>
    /// <param name="cancellationToken">Token to cancel the request if the client disconnects.</param>
    /// <returns>The updated recurring transaction.</returns>
    /// <response code="200">Recurring transaction updated successfully.</response>
    /// <response code="400">Validation failed (e.g. invalid recurrence settings).</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    /// <response code="404">No recurring transaction with the given ID exists for this user.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(RecurringTransactionReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecurringTransactionReadDto>> Update(
        int id,
        RecurringTransactionUpdateDto dto,
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var recurring = await _recurringTransactionService.UpdateAsync(id, dto, userId, cancellationToken);
        return Ok(recurring);
    }

    /// <summary>
    ///     Permanently deletes a recurring transaction that belongs to the authenticated user.
    ///     This operation is irreversible and does not affect already-generated transactions.
    /// </summary>
    /// <param name="id">The ID of the recurring transaction to delete.</param>
    /// <param name="cancellationToken">Token to cancel the request if the client disconnects.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Recurring transaction deleted successfully.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    /// <response code="404">No recurring transaction with the given ID exists for this user.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id,
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _recurringTransactionService.DeleteAsync(id, userId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    ///     Toggles the active status of a recurring transaction that belongs to the authenticated user.
    ///     Deactivating a recurring transaction prevents future automatic generation without deleting it.
    /// </summary>
    /// <param name="id">The ID of the recurring transaction to toggle.</param>
    /// <param name="cancellationToken">Token to cancel the request if the client disconnects.</param>
    /// <returns>The updated recurring transaction reflecting the new active status.</returns>
    /// <response code="200">Active status toggled successfully.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    /// <response code="404">No recurring transaction with the given ID exists for this user.</response>
    [HttpPost("{id:int}/toggle")]
    [ProducesResponseType(typeof(RecurringTransactionReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecurringTransactionReadDto>> ToggleActive(int id,
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var recurring = await _recurringTransactionService.ToggleActiveAsync(id, userId, cancellationToken);
        return Ok(recurring);
    }

    /// <summary>
    ///     Triggers generation of pending transactions for all active recurring transactions
    ///     belonging to the authenticated user. Transactions are only created for due dates
    ///     that have not yet been generated.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the request if the client disconnects.</param>
    /// <returns>A result summary indicating how many transactions were generated.</returns>
    /// <response code="200">Generation completed. Result summary returned.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(GenerateTransactionsResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<GenerateTransactionsResultDto>> GenerateTransactions(
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _recurringTransactionService.GenerateTransactionsAsync(userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    ///     Triggers generation of pending transactions for a specific recurring transaction
    ///     belonging to the authenticated user. Only due dates that have not yet been generated are processed.
    /// </summary>
    /// <param name="id">The ID of the recurring transaction to generate transactions for.</param>
    /// <param name="cancellationToken">Token to cancel the request if the client disconnects.</param>
    /// <returns>A result summary indicating how many transactions were generated.</returns>
    /// <response code="200">Generation completed. Result summary returned.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    /// <response code="404">No recurring transaction with the given ID exists for this user.</response>
    [HttpPost("{id:int}/generate")]
    [ProducesResponseType(typeof(GenerateTransactionsResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GenerateTransactionsResultDto>> GenerateForRecurring(int id,
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result =
            await _recurringTransactionService.GenerateForRecurringTransactionAsync(id, userId, cancellationToken);
        return Ok(result);
    }
}