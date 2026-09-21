# Third-Party Asset Credits

The team member who downloads a third-party asset must add one row for that asset on the same calendar day as the download and before the asset is committed.

Every asset recorded in the table must be stored under `Assets/_Project/ThirdParty`. The Asset cell must name the file or folder exactly as it appears under that path.

A row counts as complete only when each of its five cells contains non-empty text. Complete any incomplete row before committing the asset it describes.

| Asset | Author | Source URL | Licence | Where it is used |
| --- | --- | --- | --- | --- |
| Example: delivery van model | Example author | https://example.com/delivery-van | Example licence | Example: player vehicle |

## Attribution guard

Before staging any path under `Assets/_Project/ThirdParty`, pair every file other than `.gitkeep` with a complete credits row. For every file without a complete row, report its path and the missing Author, Source URL, and Licence values, and request those values from Ako before staging. Leave every unattributed file unstaged and unchanged on disk.

At setup time, `Assets/_Project/ThirdParty` contains only `.gitkeep`, so the guard reports nothing.
