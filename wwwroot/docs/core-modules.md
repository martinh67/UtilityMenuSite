# Core Modules

Core modules are available to all users — no Pro licence required.

---

## GetLastRow

**What it does:** Finds the last non-empty row in the currently selected column and returns its row number.

**Usage:**
1. Click the cell in the column you want to inspect (or select the whole column).
2. Click **GetLastRow** on the UtilityMenu ribbon.
3. The row number is displayed in a dialog and copied to your clipboard.

**VBA equivalent:**
```vba
Dim lastRow As Long
lastRow = Cells(Rows.Count, ActiveCell.Column).End(xlUp).Row
```

**Notes:**
- Returns `1` if the column is entirely empty.
- Works on the **active sheet** only.

---

## GetLastColumn

**What it does:** Finds the last non-empty column in the currently selected row and returns its column letter and number.

**Usage:**
1. Click a cell in the row you want to inspect.
2. Click **GetLastColumn** on the UtilityMenu ribbon.
3. The column letter (e.g., `AZ`) and number (e.g., `52`) are shown in a dialog and copied to your clipboard.

**VBA equivalent:**
```vba
Dim lastCol As Long
lastCol = Cells(ActiveCell.Row, Columns.Count).End(xlToLeft).Column
```

---

## UnhideRows

**What it does:** Unhides all hidden rows on the active sheet in a single click.

**Usage:**
1. Activate the sheet that has hidden rows.
2. Click **UnhideRows** on the UtilityMenu ribbon.
3. All hidden rows are revealed. A confirmation message shows the count of rows unhidden.

**VBA equivalent:**
```vba
ActiveSheet.Rows.Hidden = False
```

**Notes:**
- Does not affect column visibility.
- Cannot unhide rows within a protected sheet unless you unprotect first.
