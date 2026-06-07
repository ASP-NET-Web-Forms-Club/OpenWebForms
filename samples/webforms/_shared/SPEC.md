# Shared CRUD Spec — "Book Library" (all 3 projects)

A small **Books** CRUD that looks near-identical across the 3 projects (same schema, same
fields, same `site.css`). Each project implements it in its own architecture.

## Database (SQLite, one file per project under `App_Data\library.db`)

Connection string: `Data Source=|App_Data|library.db;Version=3;` — resolve `|App_Data|` to
`HttpContext.Current.Server.MapPath("~/App_Data/library.db")` at runtime. Ensure the
`App_Data` folder exists. Create + seed the table on first use (idempotent).

```sql
CREATE TABLE IF NOT EXISTS books (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    title        TEXT    NOT NULL DEFAULT '',
    author       TEXT    NOT NULL DEFAULT '',
    genre        TEXT    NOT NULL DEFAULT '',
    year         INTEGER NOT NULL DEFAULT 0,
    format       TEXT    NOT NULL DEFAULT '',   -- project 1 only (RadioButtonList)
    tags         TEXT    NOT NULL DEFAULT '',   -- project 1 only (CheckBoxList, comma-joined)
    availability TEXT    NOT NULL DEFAULT '',   -- project 1 only (RadioButton group)
    in_stock     INTEGER NOT NULL DEFAULT 0,    -- 0/1 boolean (CheckBox)
    date_created TEXT    NOT NULL DEFAULT ''
);
```

Projects 2 (Vanilla) and 3 (Pageless) use ONLY the core fields: `title, author, genre,
year, in_stock`. They still target the same table (the extra columns just stay default).

### Seed (insert only if `SELECT COUNT(*) FROM books = 0`)
| title | author | genre | year | in_stock |
|---|---|---|---|---|
| The Pragmatic Programmer | Andrew Hunt | Non-Fiction | 1999 | 1 |
| Dune | Frank Herbert | Sci-Fi | 1965 | 1 |
| Clean Code | Robert C. Martin | Non-Fiction | 2008 | 0 |
| The Hobbit | J.R.R. Tolkien | Fantasy | 1937 | 1 |

## Data model (`obBook`) — private snake_case fields + public PascalCase properties

```csharp
public class obBook
{
    int id = 0;
    string title = "";
    string author = "";
    string genre = "";
    int year = 0;
    string format = "";
    string tags = "";
    string availability = "";
    int in_stock = 0;
    string date_created = "";

    public int Id { get { return id; } set { id = value; } }
    public string Title { get { return title; } set { title = value; } }
    public string Author { get { return author; } set { author = value; } }
    public string Genre { get { return genre; } set { genre = value; } }
    public int Year { get { return year; } set { year = value; } }
    public string Format { get { return format; } set { format = value; } }
    public string Tags { get { return tags; } set { tags = value; } }
    public string Availability { get { return availability; } set { availability = value; } }
    public int InStock { get { return in_stock; } set { in_stock = value; } }
    public string DateCreated { get { return date_created; } set { date_created = value; } }
}
```
Use `SQLiteExpress` (namespace `System.Data.SQLite`) for all data access:
`s.GetObjectList<obBook>(sql)`, `s.GetObject<obBook>(sql, params)`, `s.Insert`, `s.Update`,
`s.Execute`, `s.ExecuteScalar<int>`. Open one `SQLiteConnection` per request/operation.

## Field option sets (the SAME everywhere they appear)
- **Genre** (DropDownList / `<select>`): Fiction, Non-Fiction, Sci-Fi, Fantasy, Biography, Mystery
- **Format** (project 1 RadioButtonList): Hardcover, Paperback, eBook
- **Tags** (project 1 CheckBoxList): Bestseller, Award-Winning, Classic, New Release
- **Availability** (project 1 RadioButton group): Available, Reserved

## CRUD behavior (identical semantics)
- **List**: all books, newest id first (`ORDER BY id DESC`), shown in a table/grid with columns:
  Title, Author, Genre, Year, In Stock (badge), Actions (Edit / Delete).
- **Create**: empty/zero id → INSERT (set `date_created` to `DateTime.Now` "yyyy-MM-dd HH:mm:ss").
- **Update**: id > 0 → UPDATE that row.
- **Delete**: by id (confirm before delete).
- **Validation** (server-side, all projects): Title required; Author required; Year between
  1400 and (current year + 1). On failure show an error message, do not save.

## Page layout (use shared `site.css`, copied to each project's `css/site.css`)
```
<header class="app-header"><div class="inner">
    <h1>📚 Book Library</h1><span class="tag">{PROJECT LABEL}</span>
</div></header>
<div class="container">
    <div class="two-col">
        <div class="card">  <h2>Add / Edit Book</h2>  ...form...  </div>
        <div class="card">  <h2>Books</h2>  ...list/grid...  </div>
    </div>
</div>
```
`{PROJECT LABEL}` = `Server Controls` / `Vanilla (API + Fetch)` / `Pageless`.
Form fields top-to-bottom: Title, Author, Genre, Year, In&nbsp;Stock, [Save] [Clear].
(Project 1 also adds Format, Tags, Availability between Year and In Stock.)

## Namespaces
Use each project's default namespace: `WebFormsServerControls`, `WebFormsVanilla`,
`WebFormsPageless`. Do NOT put new app classes in namespace `System`. No LINQ — use for/foreach.

## Build / file inclusion
These are classic .NET Framework 4.8 Web Application Projects (non-SDK). Every new `.cs`
file (code-behind, designer, models, handlers, Global.asax.cs) MUST be added to the
`.csproj` as `<Compile Include=... />`; `.aspx`/`.master`/`Global.asax` as `<Content Include=... />`.
Build with mcp2 `msbuild` (Debug). The IIS sites are already live:
7001 = ServerControls, 7002 = Vanilla, 7003 = Pageless.
