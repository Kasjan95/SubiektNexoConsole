# Custom Fields and Flags

## Discovery first

Custom fields are configured independently in every nexo installation. Clients must discover definitions instead of hard-coding names, types or dictionary values.

```http
GET /Additional-field-definitions/basic?target=product
GET /Additional-field-definitions/basic?target=party
GET /Additional-field-definitions/advanced?target=product
GET /Additional-field-definitions/advanced?target=party
GET /Additional-field-definitions/flags
```

Supported targets are `product` and `party`. Advanced definitions include top-level fields, groups, required/editable/visible metadata and supported dictionary options.

`editable: false` describes editing through the nexo GUI. It does not mean that the field is read-only through Sfera. Such fields are commonly intended for integration-controlled values.

## Value shape

Basic and advanced values share one transport shape:

```json
{
  "id": "I0",
  "value": 100003
}
```

Use JSON values matching the discovered definition:

| Definition type | JSON value |
| --- | --- |
| `text`, `longText` | string or `null` |
| `integer` | integer number or `null` |
| `decimal` | JSON number or `null` |
| `boolean` | `true`, `false` or `null` |
| `date` | ISO 8601 string or `null` |
| integer-key dictionary | integer key from `dictionary.options` |
| GUID-key dictionary | GUID string from the discovered dictionary |

Send dictionary keys, not display labels. The adapter resolves the definition and calls the matching Sfera accessor.

## Current write support

| Operation | Basic fields | Advanced fields | Flag |
| --- | --- | --- | --- |
| Create product | No | Yes | No |
| Patch product | Yes | Yes | Yes |
| Create party | No | Yes | No |
| Patch party | Yes | Yes | Yes |

Create applies advanced values to the new business object before its first `Zapisz()`. This is required when nexo marks an advanced field as mandatory; creating first and patching later would fail validation.

PATCH preserves omitted/value/null semantics. Omitting a collection leaves current values unchanged. Supplying an entry with `value: null` clears it when the field type and nexo rules allow clearing.

## Product create example

The following example deliberately exercises a required custom dictionary, an integration-controlled date, boolean, integer and text values, a system warehouse dictionary and a decimal:

```json
{
  "name": "Advanced field integration test",
  "sku": "TEST-ADV-001",
  "ean": null,
  "advancedFields": [
    { "id": "I0", "value": 100003 },
    { "id": "T0", "value": "2026-10-20T00:00:00" },
    { "id": "B0", "value": true },
    { "id": "I2", "value": 25 },
    { "id": "S0", "value": "TEST/DOC/2026/001" },
    { "id": "S1", "value": "Integration test" },
    { "id": "I1", "value": 100000 },
    { "id": "D0", "value": 12.34 }
  ]
}
```

IDs and keys above are examples from one installation. They are not portable configuration defaults.

## Party create example

```json
{
  "displayName": "Advanced field B2B test",
  "type": 2,
  "subtype": 7,
  "signature": "TEST-ADV-PARTY-001",
  "firstName": null,
  "lastName": null,
  "companyName": "Advanced field B2B test Ltd.",
  "taxId": null,
  "euTaxId": null,
  "businessRegistryNumber": null,
  "nationalCourtRegisterNumber": null,
  "partyGroupId": null,
  "industryIds": [],
  "featureIds": [],
  "notes": "Party created for an advanced-field integration test",
  "addresses": [],
  "contacts": [],
  "advancedFields": [
    { "id": "B0", "value": true },
    { "id": "T0", "value": "2026-10-14T00:00:00" },
    { "id": "B1", "value": true },
    { "id": "B2", "value": false },
    { "id": "I0", "value": 100007 }
  ]
}
```

Party type, subtype, field IDs and dictionary keys must be taken from the target installation.

## Errors and validation

Unknown IDs, incompatible JSON types and unsupported dictionaries are rejected before the business object is saved. nexo validation messages are translated into API problem responses. A required field must be provided during Create unless its nexo definition supplies an acceptable default.

Definitions themselves are materialized for products and parties. Their GET endpoints use freshness metadata and read-through refresh, so clients can build dynamic forms without opening a separate live Sfera session on every request.

