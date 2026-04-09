# FxRatesApi.Api

This API now validates currency codes before hitting the database or any external rate provider.

## Why this exists

Invalid currency inputs should fail fast with HTTP 400 so we do not waste provider calls, database work, or log noise on obviously bad requests.

## Current validation scope

For simplicity, the project currently validates against this limited in-memory subset of currencies:

- USD
- EUR
- GBP
- JPY
- CHF
- CAD
- AUD
- NZD
- CNY
- HKD
- SGD
- SEK
- NOK
- DKK
- PLN
- CZK

The lookup uses a `HashSet<string>` so membership checks are fast.

## Broader currency coverage

The official ISO 4217 maintenance data currently publishes 180 active codes in List One as of 2026-01-01. Source:

- SIX Group ISO 4217 List One XML: https://www.six-group.com/dam/download/financial-information/data-center/iso-currrency/lists/list-one.xml

This project does not validate against the full ISO 4217 dataset yet. The current subset exists purely to keep the implementation simple while still blocking invalid or unsupported codes early.

## Better follow-up options

Later, we could replace the hard-coded subset with a better approach such as:

- Loading the full ISO 4217 list from a maintained local data file checked into the repo.
- Generating the allowed-code set from the official SIX Group list during build or release.
- Syncing the official list on a schedule and caching it locally.
- Storing supported currencies in a reference-data table so operations can manage them without code changes.