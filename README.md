# FxRatesApi

A .NET 8 ASP.NET Core Web API for CRUD operations on foreign exchange rates.

## Features
- CRUD endpoints for currency pairs with `bid` and `ask` prices
- SQLite persistence via EF Core
- `GET /api/ExchangeRates/pair/{baseCurrency}/{quoteCurrency}` checks the database first
- If missing, it fetches the rate from Alpha Vantage, stores it, and returns it
- Bonus: a lightweight in-memory queue publishes an event whenever a new rate is added

## Tech stack
- .NET 8
- ASP.NET Core Web API
- Entity Framework Core + SQLite
- Swagger / OpenAPI

## Configuration
Update `FxRatesApi.Api/appsettings.json` with your Alpha Vantage API key:

```json
"AlphaVantage": {
  "BaseUrl": "https://www.alphavantage.co/query",
  "ApiKey": "YOUR_API_KEY_HERE"
}
```

You can also use an environment variable:

```powershell
$env:ALPHAVANTAGE_API_KEY="your_key_here"
```

## Run
```powershell
cd C:\Users\xanam\FxRatesApi\FxRatesApi.Api
dotnet run
```

Swagger UI will be available at:
- `https://localhost:xxxx/swagger`
- `http://localhost:xxxx/swagger`

## Main endpoints
- `GET /api/ExchangeRates`
- `GET /api/ExchangeRates/{id}`
- `GET /api/ExchangeRates/pair/USD/EUR`
- `POST /api/ExchangeRates`
- `PUT /api/ExchangeRates/{id}`
- `DELETE /api/ExchangeRates/{id}`
