using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartExpense.Application.Dtos.Auth;
using SmartExpense.Application.Dtos.Budget;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Constants;

namespace SmartExpense.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[Authorize(Roles = IdentityRoleConstants.User)]
public class BudgetController : ControllerBase
{
    private readonly IBudgetService _budgetService;

    public BudgetController(IBudgetService budgetService)
    {
        _budgetService = budgetService;
    }

    /// <summary>
    ///     Returns all budgets for the authenticated user, optionally filtered by month and year.
    /// </summary>
    /// <param name="month">Optional month (1–12) to filter budgets by.</param>
    /// <param name="year">Optional year to filter budgets by.</param>
    /// <param name="cancellationToken">Token to cancel the request if the client disconnects.</param>
    /// <returns>A list of budgets matching the specified filters.</returns>
    /// <response code="200">Budgets retrieved successfully.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<BudgetReadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<BudgetReadDto>>> GetAll(
        [FromQuery] int? month,
        [FromQuery] int? year,
        CancellationToken cancellationToken = default)
    {
        if (month < 1 || month > 12)
            return BadRequest(new BasicResponse
            {
                Succeeded = false,
                Message = "Month must be between 1 and 12."
            });
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var budgets = await _budgetService.GetAllAsync(userId, month, year, cancellationToken);
        return Ok(budgets);
    }

    /// <summary>
    ///     Returns a single budget by its ID, scoped to the authenticated user.
    /// </summary>
    /// <param name="id">The unique identifier of the budget.</param>
    /// <param name="cancellationToken">Token to cancel the request if the client disconnects.</param>
    /// <returns>The matching budget.</returns>
    /// <response code="200">Budget found and returned.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    /// <response code="404">No budget with the given ID exists for this user.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(BudgetReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BudgetReadDto>> GetById(int id, CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var budget = await _budgetService.GetByIdAsync(id, userId, cancellationToken);
        return Ok(budget);
    }

    /// <summary>
    ///     Returns an aggregated budget summary for the authenticated user for the specified month and year,
    ///     including total budgeted amount, total spent, and remaining balance.
    /// </summary>
    /// <param name="month">The calendar month (1–12) for the summary.</param>
    /// <param name="year">The calendar year for the summary.</param>
    /// <param name="cancellationToken">Token to cancel the request if the client disconnects.</param>
    /// <returns>A summary of the user's budget for the given month and year.</returns>
    /// <response code="200">Budget summary calculated and returned successfully.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(BudgetSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<BudgetSummaryDto>> GetSummary(
        [FromQuery] int month,
        [FromQuery] int year,
        CancellationToken cancellationToken = default)
    {
        if (month < 1 || month > 12)
            return BadRequest(new BasicResponse
            {
                Succeeded = false,
                Message = "Month must be between 1 and 12."
            });
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var summary = await _budgetService.GetSummaryAsync(userId, month, year, cancellationToken);
        return Ok(summary);
    }

    /// <summary>
    ///     Creates a new budget for the authenticated user.
    ///     Only one budget per category per month is allowed.
    /// </summary>
    /// <param name="dto">The budget data to persist.</param>
    /// <param name="cancellationToken">Token to cancel the request if the client disconnects.</param>
    /// <returns>The newly created budget, including its generated ID.</returns>
    /// <response code="201">Budget created. Location header points to the new resource.</response>
    /// <response code="400">Validation failed (e.g. invalid amount or missing fields).</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    /// <response code="404">The specified category does not exist for this user.</response>
    /// <response code="409">A budget for this category and month already exists.</response>
    [HttpPost]
    [ProducesResponseType(typeof(BudgetReadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BudgetReadDto>> Create(
        BudgetCreateDto dto,
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var budget = await _budgetService.CreateAsync(dto, userId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = budget.Id }, budget);
    }

    /// <summary>
    ///     Updates an existing budget that belongs to the authenticated user.
    /// </summary>
    /// <param name="id">The ID of the budget to update.</param>
    /// <param name="dto">The updated budget data.</param>
    /// <param name="cancellationToken">Token to cancel the request if the client disconnects.</param>
    /// <returns>The updated budget.</returns>
    /// <response code="200">Budget updated successfully.</response>
    /// <response code="400">Validation failed (e.g. invalid amount or missing fields).</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    /// <response code="404">No budget with the given ID exists for this user.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(BudgetReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BudgetReadDto>> Update(
        int id,
        BudgetUpdateDto dto,
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var budget = await _budgetService.UpdateAsync(id, dto, userId, cancellationToken);
        return Ok(budget);
    }

    /// <summary>
    ///     Permanently deletes a budget that belongs to the authenticated user.
    ///     This operation is irreversible.
    /// </summary>
    /// <param name="id">The ID of the budget to delete.</param>
    /// <param name="cancellationToken">Token to cancel the request if the client disconnects.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Budget deleted successfully.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    /// <response code="404">No budget with the given ID exists for this user.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _budgetService.DeleteAsync(id, userId, cancellationToken);
        return NoContent();
    }
}