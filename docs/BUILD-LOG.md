# Build Log

Keep real build rows below the example in date order, with the oldest Date first and the newest Date last.

Ako records exactly one row for each Checkpoint_Build on the same calendar day its build tag is created. While Ako is unavailable, Kyuri records the row.

For each Checkpoint_Build row, use a Build tag in the format `build-YYYY-MM-DD` and enter that same `YYYY-MM-DD` date in the Date cell.

Every cell in a Checkpoint_Build row must be non-empty. If a cell has nothing to report, write `none`; a blank cell means the record is missing rather than a clean result.

A tagged build that fails to open or produces Unity console errors is still logged as a row, with the failure named in its What broke cell. It is not a Checkpoint_Build until a later build opens and plays without console errors.

| Date | Build tag | What is in it | What broke | Who tested it |
| --- | --- | --- | --- | --- |
| Example 2025-01-15 | build-2025-01-15 | Vehicle steering and order pickup are playable | Cargo icon overlaps the delivery timer at 1280x720 | Ako |
