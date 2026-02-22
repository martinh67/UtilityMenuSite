# Pro Modules

Pro modules require an active **Pro** or **Custom** licence. Activate your licence from the UtilityMenu ribbon or your [Dashboard](/dashboard).

---

## AdvancedData

**What it does:** Provides enhanced data inspection tools — data type profiling, null/blank counts, duplicate detection, and range statistics — displayed in a task pane.

**Usage:**
1. Select a range of cells.
2. Click **AdvancedData** on the UtilityMenu ribbon.
3. The AdvancedData task pane opens showing a profile of the selected range.

**Features:**
- Column data-type breakdown (text, number, date, blank)
- Count of unique vs. duplicate values
- Min, Max, Average for numeric columns
- Export profile to a new sheet

---

## BulkOperations

**What it does:** Apply transformations to large ranges in bulk — trim whitespace, change case, find & replace with regex, remove special characters.

**Usage:**
1. Select the range to transform.
2. Click **BulkOperations** on the UtilityMenu ribbon.
3. Choose an operation from the dialog and click **Apply**.

**Available operations:**
| Operation | Description |
|-----------|-------------|
| Trim Whitespace | Removes leading/trailing spaces from every cell |
| UPPER / lower / Title | Changes text case |
| Remove Special Chars | Strips non-alphanumeric characters |
| Find & Replace (Regex) | Full regex-powered find and replace |
| Number Normalise | Converts comma-decimal to dot-decimal and vice versa |

---

## DataExport

**What it does:** Export the active sheet or a selected range to CSV, TSV, JSON, or Markdown table format.

**Usage:**
1. Select a range (or leave nothing selected to export the used range).
2. Click **DataExport** on the UtilityMenu ribbon.
3. Choose format and destination (file or clipboard).

**Formats supported:**
- **CSV** — standard comma-separated, UTF-8 with BOM
- **TSV** — tab-separated
- **JSON** — array of objects (first row as keys)
- **Markdown** — GitHub-flavoured table

---

## SqlBuilder

**What it does:** Generates SQL `INSERT`, `CREATE TABLE`, or `SELECT` statements from your spreadsheet data.

**Usage:**
1. Select a range with a header row.
2. Click **SqlBuilder** on the UtilityMenu ribbon.
3. Choose a SQL dialect (ANSI, T-SQL, MySQL, PostgreSQL) and statement type.
4. The generated SQL is shown in a code viewer and copied to your clipboard.

**Example output (T-SQL INSERT):**
```sql
INSERT INTO [Sheet1] ([Name], [Age], [City])
VALUES ('Alice', 30, 'London'),
       ('Bob', 25, 'Manchester');
```

**Supported dialects:** ANSI SQL, T-SQL (SQL Server), MySQL, PostgreSQL, SQLite.
