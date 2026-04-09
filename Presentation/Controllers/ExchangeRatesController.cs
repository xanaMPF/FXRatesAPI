using FxRatesApi.Api.Application.Dtos;
using FxRatesApi.Api.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace FxRatesApi.Api.Presentation.Controllers;

[ApiController]
[Route("rates")]
public class ExchangeRatesController : ControllerBase
{
    private readonly IExchangeRateService _exchangeRateService;

    public ExchangeRatesController(IExchangeRateService exchangeRateService)
    {
        _exchangeRateService = exchangeRateService ?? throw new ArgumentNullException(nameof(exchangeRateService));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var rates = await _exchangeRateService.GetAllAsync(cancellationToken);
        return Ok(rates);
    }

    [HttpGet("{baseCurrency}/{quoteCurrency}")]
    public async Task<IActionResult> GetByPair(string baseCurrency, string quoteCurrency, CancellationToken cancellationToken)
    {
        var rate = await _exchangeRateService.GetCurrentAsync(baseCurrency, quoteCurrency, cancellationToken);
        return Ok(rate);
    }

    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] UpsertExchangeRateRequest request, CancellationToken cancellationToken)
    {
        var result = await _exchangeRateService.UpsertAsync(request, cancellationToken);
        if (!result.Created)
        {
            return Ok(result.Rate);
        }

        return CreatedAtAction(
            nameof(GetByPair),
            new { baseCurrency = result.Rate.BaseCurrency, quoteCurrency = result.Rate.QuoteCurrency },
            result.Rate);
    }

    [HttpPut("{baseCurrency}/{quoteCurrency}")]
    public async Task<IActionResult> Update(string baseCurrency, string quoteCurrency, [FromBody] UpdateExchangeRateRequest request, CancellationToken cancellationToken)
    {
        var updated = await _exchangeRateService.UpdateAsync(baseCurrency, quoteCurrency, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{baseCurrency}/{quoteCurrency}")]
    public async Task<IActionResult> Delete(string baseCurrency, string quoteCurrency, CancellationToken cancellationToken)
    {
        var deleted = await _exchangeRateService.DeleteAsync(baseCurrency, quoteCurrency, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
