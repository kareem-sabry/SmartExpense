using System.Security.Claims;
using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartExpense.Application.Dtos.Transaction;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Constants;
using SmartExpense.Core.Models;

namespace SmartExpense.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[Authorize(Roles = IdentityRoleConstants.User)]
public class TransactionController : ControllerBase
{
    private readonly ITransactionService _transactionService;

    public TransactionController(ITransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    /// <summary>
    ///     Returns a paginated list of transactions for the authenticated user.
    ///     Supports filtering by date range, type, and category via query parameters.
    /// </summary>
    /// <param name="parameters">Pagination and filter options (page, pageSize, startDate, endDate, type, categoryId).</param>
    /// <returns>A paged result containing transactions that match the given parameters.</returns>
    /// <response code="200">Transactions retrieved successfully.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TransactionReadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<TransactionReadDto>>> GetTransactions(
        [FromQuery] TransactionQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _transactionService.GetPagedAsync(userId, parameters, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    ///     Returns a single transaction by its ID, scoped to the authenticated user.
    /// </summary>
    /// <param name="id">The unique identifier of the transaction.</param>
    /// <returns>The matching transaction.</returns>
    /// <response code="200">Transaction found and returned.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    /// <response code="404">No transaction with the given ID exists for this user.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TransactionReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransactionReadDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var transaction = await _transactionService.GetByIdAsync(id, userId, cancellationToken);
        return Ok(transaction);
    }

    /// <summary>
    ///     Returns the most recent transactions for the authenticated user, ordered by date descending.
    ///     Intended for dashboard widgets and quick-glance summaries.
    /// </summary>
    /// <param name="count">Maximum number of transactions to return. Defaults to 10.</param>
    /// <returns>A list of the most recent transactions.</returns>
    /// <response code="200">Recent transactions retrieved successfully.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(List<TransactionReadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<TransactionReadDto>>> GetRecent(
        [FromQuery] int count = 10,
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var transactions = await _transactionService.GetRecentAsync(userId, count, cancellationToken);
        return Ok(transactions);
    }

    /// <summary>
    ///     Returns an aggregated summary of the authenticated user's transactions within
    ///     an optional date range, including total income, total expenses, and net balance.
    /// </summary>
    /// <param name="startDate">Optional inclusive start of the date range.</param>
    /// <param name="endDate">Optional inclusive end of the date range.</param>
    /// <returns>A summary containing income, expense, net balance, and transaction count.</returns>
    /// <response code="200">Summary calculated and returned successfully.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(TransactionSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TransactionSummaryDto>> GetSummary(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var summary = await _transactionService.GetSummaryAsync(userId, startDate, endDate, cancellationToken);
        return Ok(summary);
    }

    /// <summary>
    ///     Exports transactions for the authenticated user within the given date range
    ///     as a UTF-8 encoded CSV file. Useful for personal finance tracking and
    ///     importing into spreadsheet tools.
    /// </summary>
    /// <param name="startDate">Inclusive start date of the export window.</param>
    /// <param name="endDate">Inclusive end date of the export window.</param>
    /// <param name="cancellationToken">Token to cancel the request if the client disconnects.</param>
    /// <returns>
    ///     A CSV file named <c>transactions_{startDate}_{endDate}.csv</c> with columns:
    ///     Date, Description, Category, Type, Amount, Notes.
    /// </returns>
    /// <response code="200">CSV file generated and returned.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    /// <response code="429">Rate limit exceeded.</response>
    [HttpGet("export")]
    [EnableRateLimiting("api")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var csv = await _transactionService.ExportToCsvAsync(userId, startDate, endDate, cancellationToken);
        var bytes = Encoding.UTF8.GetBytes(csv);
        var filename = $"transactions_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.csv";
        return File(bytes, "text/csv", filename);
    }

    /// <summary>
    ///     Creates a new transaction for the authenticated user.
    ///     The referenced category must belong to the user and be active.
    ///     Transaction dates in the future are rejected.
    /// </summary>
    /// <param name="dto">The transaction data to persist.</param>
    /// <returns>The newly created transaction, including its generated ID and audit fields.</returns>
    /// <response code="201">Transaction created. Location header points to the new resource.</response>
    /// <response code="400">Validation failed (e.g. future date, inactive category).</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    /// <response code="404">The specified category does not exist for this user.</response>
    [HttpPost]
    [ProducesResponseType(typeof(TransactionReadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransactionReadDto>> Create(
        TransactionCreateDto dto,
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var transaction = await _transactionService.CreateAsync(dto, userId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = transaction.Id }, transaction);
    }

    /// <summary>
    ///     Updates an existing transaction that belongs to the authenticated user.
    ///     The referenced category must belong to the user and be active.
    ///     Transaction dates in the future are rejected.
    /// </summary>
    /// <param name="id">The ID of the transaction to update.</param>
    /// <param name="dto">The updated transaction data.</param>
    /// <returns>The updated transaction with refreshed audit fields.</returns>
    /// <response code="200">Transaction updated successfully.</response>
    /// <response code="400">Validation failed (e.g. future date, inactive category).</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    /// <response code="404">No transaction with the given ID exists for this user.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(TransactionReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransactionReadDto>> Update(
        int id,
        TransactionUpdateDto dto,
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var transaction = await _transactionService.UpdateAsync(id, dto, userId, cancellationToken);
        return Ok(transaction);
    }

    /// <summary>
    ///     Permanently deletes a transaction that belongs to the authenticated user.
    ///     This operation is irreversible.
    /// </summary>
    /// <param name="id">The ID of the transaction to delete.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Transaction deleted successfully.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    /// <response code="404">No transaction with the given ID exists for this user.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _transactionService.DeleteAsync(id, userId, cancellationToken);
        return NoContent();
    }
}