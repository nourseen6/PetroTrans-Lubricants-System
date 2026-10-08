# Petro Trans — Phase 2 Architecture & Technology Decision Document

**Status:** Proposed — awaiting business owner and technical reviewer approval  
**Date:** 16 August 2026  
**Scope:** Architecture and technology evaluation only. No application code, schema implementation, scaffolding, dependencies, UI, or migrations.

This document does not invent prices, tax rules, payment methods, accounting rules, suppliers, customers, legal claims, or commercial policies. Phase 1 items that remain unresolved are marked **TBD**.

---

## 0. Architecture principles (locked from Phase 1)

1. Internal ERP is a **Windows desktop business application**, launched from a Petro Trans icon — not a browser URL workflow.
2. **Arabic-first, native RTL.** English is optional via a language switcher. Do not design English-first.
3. **Local-first.** The system must work without the public internet. Synchronization is a future phase, not v1 implementation.
4. **Inventory is a ledger.** Quantity changes only through movements.
5. **Product ≠ packaging variant.**
6. **Pricing is flexible.** Standard / customer-type / customer-specific / permissioned override.
7. **Payment methods are configurable** in Settings. The list is not invented here.
8. **Customer balances are derived from transactions.**
9. **Excel is a core business capability** (professional export, controlled intelligent import).
10. **Do not implement financial behaviour** that Phase 1 left undefined.
11. **Public website is separate** and must not be coupled to this architecture yet.

---

## 1. Windows desktop architecture

### Options considered

| Option | What it is |
|---|---|
| A. Cloud web app in a browser | Hosted site; user opens Chrome/Edge and types a URL |
| B. PWA / Edge “install app” | Web app installed as a shortcut; still a browser engine with web deployment |
| C. Electron + web UI | Chromium + Node bundled as `.exe` |
| D. Tauri 2 + web UI | Windows WebView2 shell; backend typically Rust |
| E. Native WPF / WinUI 3 (XAML) | Fully native Windows UI, no web UI |
| F. Blazor Hybrid only | .NET UI in WebView2; C# for UI and server |
| G. **.NET 8 WebView2 desktop host + local ASP.NET Core + React UI** | One `.exe` starts a localhost-only API and shows the Arabic React UI in a native window |

### Comparison against Petro Trans criteria

| Criterion | A Browser | B PWA | C Electron | D Tauri 2 | E Native XAML | F Blazor Hybrid | G .NET host + React (recommended) |
|---|---|---|---|---|---|---|---|
| Windows desktop icon / Start menu | Poor | Fair | Good | Good | Excellent | Excellent | Excellent |
| User does not open a URL | Fail | Weak | Pass | Pass | Pass | Pass | Pass |
| Arabic RTL | Good | Good | Excellent | Excellent | Fair | Good | Excellent |
| Professional Excel | Server-side | Server-side | Good (ExcelJS) | Good / mixed | Excellent (ClosedXML) | Excellent | Excellent (ClosedXML) |
| Printing | Browser print | Browser print | Good | Good | Excellent | Excellent | Excellent (WebView2 print + PDF) |
| Local file access | Weak | Weak | Strong | Strong | Strong | Strong | Strong |
| Offline readiness | Depends on host | Weak | Strong | Strong | Strong | Strong | Strong |
| Future sync | Natural if already online | Natural | Designable | Designable | Designable | Designable | Strong (API already exists) |
| Security | Network attack surface | Network | Large Chromium+Node surface | Smaller | Strong | Strong | Strong (localhost-only in v1) |
| Performance / RAM | N/A | Medium | Heavy | Light | Light | Light | Light (OS WebView2) |
| Maintainability | One web stack | One web stack | High if TS-only | Split TS+Rust | C# only; slower Arabic UI | C# only | Split C#+TS; clear UI/domain split |
| Deployment / updates | Server deploy | Server deploy | electron-updater | Tauri updater | MSIX / Velopack | MSIX / Velopack | MSIX / Velopack |

### Pros / cons of realistic options

**A. Browser SaaS — rejected**  
Pros: simple hosting. Cons: violates locked desktop requirement; weak offline; Excel/print/file access are second-class.

**B. PWA — rejected as primary**  
Pros: installable. Cons: still feels like a website; service-worker offline is the wrong model for an inventory ledger; file/Excel/print are constrained.

**C. Electron**  
Pros: TypeScript everywhere; mature ExcelJS; easy desktop packaging. Cons: ships Chromium (~150–300 MB RAM); larger installer; Node in the desktop process increases attack surface; chosen too often for popularity rather than fit. Acceptable fallback, not first choice.

**D. Tauri 2**  
Pros: small installer; uses Windows WebView2; better security than Electron; good Arabic web UI. Cons: serious ERP domain logic in Rust slows delivery and review; a Node sidecar to keep TypeScript domain logic creates two processes and two update paths. Strong runner-up if C# is declined.

**E. Native XAML**  
Pros: most “Windows” feel; excellent printing and files. Cons: dense Arabic business UI (mapping wizard, filterable tables, RTL forms) is slower and less proven than React; two UI worlds if a public website is later React.

**F. Blazor Hybrid**  
Pros: one language (C#); ClosedXML; WebView2. Cons: weaker ecosystem for the intelligent Excel mapping UI and dense RTL data tables; harder to share UI skill/components with a future public site.

**G. .NET 8 WebView2 host + local ASP.NET Core + React**  
Pros: native window, icon, installer; React for Arabic-first UI; ClosedXML for professional Excel; API is already the future sync/server surface; WebView2 is an OS component (Windows 10/11), not a bundled browser; Kestrel bound to `127.0.0.1` in v1. Cons: two languages (C# + TypeScript). That split is intentional: UI vs domain.

### Recommendation

**Option G:** a single Windows application:

1. User double-clicks the Petro Trans icon.
2. A .NET 8 desktop host opens a native window (WebView2).
3. The host starts a local ASP.NET Core API bound only to `127.0.0.1` (not the LAN, not the internet).
4. The React Arabic UI loads inside the window from local files — no URL typing, no public hosting required.
5. SQLite lives in the user’s application data folder.
6. Excel import/export and backups use native Windows file dialogs.

### Why it fits Petro Trans

- Matches the locked “Windows desktop identity” requirement.
- Uses the best RTL/UI stack (React) for Arabic-first forms, tables, and the Excel mapping wizard.
- Uses the best professional Excel stack on Windows (ClosedXML).
- Is already local-first (works with no internet).
- The same API can later run on a server for multi-user sync without rewriting the business layer.
- Avoids Electron’s weight and Tauri’s Rust-for-ERP cost.

**TBD:** exact host UI toolkit (WPF vs WinUI 3) is an implementation detail inside Option G; both are acceptable. Default: **WPF + WebView2** for maturity and installer simplicity unless WinUI 3 is preferred during implementation.

---

## 2. Frontend architecture

### Options considered

- Native XAML (WPF/WinUI)
- Blazor
- Angular
- Vue
- React + TypeScript + Vite

### Pros / cons

| Option | Pros | Cons |
|---|---|---|
| XAML | Native controls | Weak fit for Arabic-first dense ERP + Excel wizard |
| Blazor | C# shared with API | Smaller RTL/data-grid/wizard ecosystem |
| Angular | Structured | Heavier; slower for this team shape |
| Vue | Good RTL | Slightly smaller enterprise table/Excel-UI ecosystem than React |
| React + TS + Vite | Best RTL admin UI, i18n, data tables, wizard flows | Requires a desktop host (solved in §1) |

### Recommendation

**React + TypeScript + Vite**, Arabic-first:

- Root `dir="rtl"` and `lang="ar"` by default.
- i18n with **Arabic as the source language**; English as a second locale.
- Language switcher persists per user.
- Light and dark mode (Phase 1).
- Desktop-first layouts: high-density tables, clear hierarchy, not card-heavy dashboards.
- UI kit: a RTL-capable, data-dense library (evaluate Mantine vs Fluent UI React vs a headless + custom Petro Trans theme in Phase 6). **Do not lock a visual kit in Phase 2** beyond: RTL-native, accessible, table-strong, not decorative.

Frontend talks only to the local API. No direct SQLite from the UI. No business rules in React except display and client-side form UX; **server validates everything**.

### Why it fits Petro Trans

Arabic-first ERP screens, the intelligent Excel mapping UI, and bilingual switching are all proven in React. The UI never becomes the system of record.

---

## 3. Backend / application architecture

### Options considered

- Business logic in the React UI
- Rust commands inside Tauri
- NestJS / Node local API
- ASP.NET Core local API + application layer

### Recommendation

**ASP.NET Core 8 (or current LTS at implementation)**, layered:

```
Desktop Host
  └── ASP.NET Core (Kestrel 127.0.0.1)
        ├── API (HTTP endpoints, auth)
        ├── Application (use cases: issue invoice, receive stock, import Excel)
        ├── Domain (products, variants, ledger, pricing resolution)
        └── Infrastructure (EF Core, ClosedXML, files, PDF/print helpers)
```

Rules:

- One use-case per write operation (create invoice, post payment, commit import batch).
- Database transactions around ledger + document + audit.
- API is the only writer to SQLite.
- v1: localhost only. Future: same API hosted for LAN/cloud sync.

### Why it fits Petro Trans

The backend is the place for inventory, pricing, permissions, and import validation. Keeping it in ASP.NET Core makes ClosedXML, Windows files, and a future multi-user server natural. NestJS is the TypeScript equivalent and is the fallback if C# is declined — it is not preferred because Excel and Windows packaging are stronger in .NET.

---

## 4. Database choice

### Options considered

| Option | Pros | Cons |
|---|---|---|
| Excel as storage | Familiar | Not a system of record; no ledger integrity |
| Access / SQL Server Express | Windows-familiar | Ops burden; overkill for one PC; Express still a server |
| PostgreSQL on the PC | Strong later for multi-user | Service to install/manage; worse single-exe story |
| SQLite | One file; zero admin; excellent backup; offline-native | Concurrent multi-PC writers are limited |
| SQLite now, PostgreSQL later | Matches v1 and Phase 11 | Must keep SQL portable |

### Recommendation

**SQLite as v1 system of record**, stored under Windows AppData (not next to the `.exe` in Program Files).

- Access via EF Core with **portable types** (no SQLite-only features that block PostgreSQL).
- **UUIDv7** primary keys (time-orderable, sync-friendly).
- Human-readable codes (`CUS-0001`, invoice numbers) are **separate unique business keys**, not primary keys.
- `created_at`, `updated_at`, `created_by`, `updated_by` on business records.
- Soft delete (`deleted_at`) where history must remain; inventory movements are **not** silently deleted.
- Optional cached on-hand table, always rebuildable from the movement ledger.

**Later (Phase 11 / multi-user):** PostgreSQL as the central store; SQLite may remain a local replica. Not implemented now.

### Why it fits Petro Trans

One PC, unreliable internet, backup = copy a file, no DBA. The schema will be designed in Phase 3 to stay portable.

**TBD:** number of warehouses/branches (Phase 1). Data model will *allow* many warehouses even if v1 uses one.

---

## 5. Authentication + RBAC

### Options considered

- No login (single shared PC session)
- Windows OS account only
- Cloud identity (Microsoft Entra, Google)
- Local application users + roles + permissions

### Recommendation

**Local application users** (username/password hashed with a modern KDF, e.g. Argon2id or ASP.NET Identity defaults).

- Login screen inside the desktop app.
- Session for the local API (authenticated cookie or equivalent over localhost HTTPS/HTTP loopback). Prefer loopback + Strict transport as practical on a local app; **no binding to `0.0.0.0` in v1**.
- **Roles** (initial): Admin, Sales, Warehouse, Accountant, and **Owner/Manager** (price override). Branch Manager as a named role is **TBD** (Phase 1).
- **Permissions** are first-class (e.g. `pricing.override`, `inventory.adjust`, `invoice.edit`, `returns.create`, `payments.create`, `users.manage`, `excel.import`). Roles are bundles of permissions.
- Price override: **Owner/Manager only** until explicitly expanded.
- Failed logins and permission denials are auditable for sensitive modules.
- No assumption that every user sees everything.

Cloud SSO is out of scope until requested.

### Why it fits Petro Trans

Starts with one person, already has the owner/manager override rule, and can add staff without redesigning security.

---

## 6. Inventory ledger architecture

### Options considered

- Overwrite `quantity` on the product row
- Ledger of movements only (compute SUM always)
- Ledger of movements + cached on-hand, same transaction

### Recommendation

**Movement ledger is the source of truth.** Cached warehouse-variant on-hand is an optimization, updated in the **same database transaction** as the movement. Cache can be rebuilt from movements.

Every movement stores (Phase 1):

- Variant (not only parent product)
- Quantity (+ in / − out, or signed quantity with a direction rule — one convention, locked in Phase 3)
- Warehouse ID
- Movement type
- Reference document type + ID
- Date/time
- User
- Notes

**Movement types (technical, not extra business policy):**

- Opening stock
- Purchase receipt
- Sale
- Sales return
- Purchase return (supported in the model; use is **TBD**)
- Adjustment
- Transfer out / transfer in (supported in the model; multi-warehouse use is **TBD**)

**Rules:**

- Do not edit historical movements in place except via a controlled, audited correction path (prefer reversing movement + new movement).
- Sales confirmation **refuses** qty > available unless a backorder/negative-stock policy is approved. Phase 1 default: **block**. Policy remains confirmable as **TBD** only if the owner wants to change it.
- Adjustments require permission + reason.

### Why it fits Petro Trans

This is the difference between an ERP and an Excel quantity cell. It also makes stock movement reports and future sync possible.

**TBD:** unit of measure (carton vs inner unit vs liter); pack composition structure; opening-stock cut-over process.

---

## 7. Products vs variants

### Recommendation

Two levels:

- **Product:** commercial identity — name, brand, category, specification/API, active flag, shared attributes.
- **Variant:** sellable and stockable unit — packaging type, packaging size, SKU, min stock, active flag, prices (see §8).

Example: “فوايجير جولد SN API 5W40” is one product; “كرتونة 3X4” and “كرتونة 12X1” are variants.

The listed Petro Trans items are **variants of shared parents**, not unrelated products. Parent grouping will be confirmed when catalog data is imported — no prices invented.

### Why it fits Petro Trans

Matches the locked catalog rule and keeps stock/pricing/sales on the unit actually sold.

**TBD:** SKU uniqueness scope; barcodes; whether pack composition (e.g. 3×4 = 12 bottles) is structured data or a label only; who assigns codes.

---

## 8. Flexible pricing architecture

### Options considered

- Single price column on the variant
- Hard-coded price per customer type
- Full price-list engine with versioning from day one
- **Resolution chain with optional layers** (recommended)

### Recommendation

Price is always stored on the **invoice line at the time of sale** (historical accuracy). Live resolution for new documents:

1. Customer-specific price for that variant, if present  
2. Else customer-type price for that variant, if present  
3. Else standard wholesale price for that variant  

Missing layers are allowed. Not every customer type needs a price. Not every customer needs a special price.

**Manual override:**

- Allowed only with `pricing.override`
- Currently only Owner/Manager
- Store: original resolved price, overridden price, user, timestamp, reason (**reason required** — business confirmation requested in the approval section)
- Fully auditable

No price values are seeded in architecture. Discount structure (line vs invoice) remains **TBD**. Quantity breaks are **not assumed**.

### Why it fits Petro Trans

Matches the confirmed flexible pricing rule without forcing a policy the business does not use.

---

## 9. Payments and customer balances

### Recommendation

- **Payment methods:** table maintained in Settings. Empty/configurable. **No methods invented.**
- **Payment record:** customer or supplier, date, method, amount, reference, notes, user, timestamps.
- **Customer balance:** computed from posted documents (invoices, returns if financially posted, payments, opening balances). Not a freely edited number.
- Direct balance edits: only via a permissioned, audited **adjustment document**, if later approved. Not assumed for v1 until Phase 4.

**Allocation of a payment to specific invoices:** data model will *allow* allocations (payment ↔ invoice lines) so Phase 4 can turn the rule on. **Policy is TBD** (open item vs invoice matching).

Supplier payments: model supported; whether v1 includes them is **TBD** (Phase 1 B2).

### Why it fits Petro Trans

Traceable collections without inventing cash/cheque/Instapay lists or tax treatment.

**TBD:** partial payments (likely yes, but posting rules in Phase 4); credit limits; payment terms; refunds vs credit notes.

---

## 10. Purchasing

### Recommendation

Model, without inventing ADNOC fields:

- Suppliers
- Purchase orders (**optional** documents — use is TBD)
- Incoming shipments (generic header: supplier, dates, business reference numbers the owner later confirms, notes)
- Goods receipts → inventory movements
- Purchase invoices → supplier payable
- Supplier balance derived from purchase invoices, purchase returns, supplier payments, opening balances

**ADNOC-specific columns:** not invented. Use a small set of generic shipment identifiers plus an extensible attributes store (key/value or JSON **documented fields only once the owner lists them**). Until then, architecture allows extension; Phase 3 will not lock fake customs/BL/container fields.

### Why it fits Petro Trans

Purchasing and stock-in exist; ADNOC paperwork is not guessed.

**TBD:** ADNOC-only vs multiple suppliers; PO required or not; actual shipment Excel columns; purchase returns in live use.

---

## 11. Sales and returns

### Sales

Flow (Phase 1): Customer → (Sales Order if used) → Invoice → stock deduction → payment → remaining balance.

- Sales order is an **optional** document type in the model. **TBD** whether v1 UI exposes it (Phase 1 A5). Architecture does not require an order to invoice.
- Invoice stores: number, customer, date, lines (variant, qty, unit price, discount **if later approved**), totals, paid/remaining **as derived or stored snapshots — Phase 4**, status, notes, user.
- Stock deducts only when the invoice is **posted** (not while drafting), in one transaction with movements.
- Invoice edit after posting: **TBD** (Phase 1 B1). Architecture: prefer credit/adjust documents over silent mutation of posted invoices.

Invoice number format: **TBD**.

### Returns

- Reference original invoice where possible.
- Restore inventory via sales-return movements.
- Financial effect: **not implemented until Phase 4**. Architecture will have a return document + links; posting to customer balance is a switchable policy, default **off** until approved.
- Audit trail required.

### Why it fits Petro Trans

Wholesale invoicing and stock safety without inventing credit-note law or VAT.

**TBD:** tax/VAT/ETA; invoice numbering; print layout legal text; delivery/driver; returns as credit vs refund vs exchange; partial returns.

---

## 12. Excel import/export architecture

### Export

A dedicated **Excel export service** in Infrastructure (ClosedXML):

- Used by modules and reports (products, customers, inventory, sales, purchases, balances, movements, reports).
- Workbook quality bar (Phase 1 locked): branding, title, date, filters/period, Arabic RTL sheet, Arabic headers, column widths, number/currency formats, totals, tables/filters, frozen headers, professional sheet names, multiple sheets when useful.
- Not CSV.

Branding assets (logo, legal name on the sheet) are **TBD**.

### Import

A dedicated **import pipeline**, not ad hoc parsers per screen:

1. Upload / pick local `.xlsx` via desktop file dialog  
2. Analyze workbook  
3. Detect sheets and header rows  
4. Profile columns  
5. Propose mapping  
6. User edits mapping  
7. Validate  
8. Preview VALID / WARNING / ERROR  
9. User confirms  
10. Commit valid rows only (partial vs all-or-nothing is **TBD**)  
11. Import batch audit log  

Templates remain available as the guided path. They are not the only path.

### Why it fits Petro Trans

Excel is how the business already works. The system must speak Excel professionally in Arabic, on the desktop.

**TBD:** `.xls` vs `.xlsx` only; who may import; historical sales/purchases affecting live stock/balances.

---

## 13. Intelligent Excel mapping / validation

### Recommendation (no LLM)

Deterministic engine:

1. **Workbook reader** — ClosedXML (and a read-optimized library if needed for large files). Detect sheets, header row, merged headers, data start.
2. **Column profiler** — header text, sample values, inferred type.
3. **Mapping suggester** — synonym dictionary (Arabic + English), plus conservative heuristics (e.g. values matching `CUS-####` → customer code). **Propose only.**
4. **Mapping UI** — user confirms or changes every field map. Saved **mapping profiles** per import purpose (and later per recurring layout).
5. **Validator** — per import purpose: products, variants, customers, opening inventory, opening balances, suppliers, historical sales, historical purchases.
6. **Classifier** — VALID / WARNING / ERROR. Errors never commit. Warnings commit only with explicit acknowledgement.
7. **Commit** — one import batch ID; transactional per batch or per approved partial-commit rule (**TBD**).
8. **Audit** — user, time, file name, purpose, mapping snapshot, counts, errors, created IDs.

Detection includes: missing required fields, duplicates, unknown products/customers, invalid qty/date/number, missing identifiers, conflicts, unsupported values, possible duplicates.

**No external AI API.** A later normalization plugin can replace the suggester behind the same interface.

### Why it fits Petro Trans

Real Excel files will not match a perfect template. The owner stays in control of mapping. Nothing silent enters the database.

---

## 14. Arabic-first RTL + English localization

### Recommendation

- Default locale: **`ar-EG`** (or `ar` with Egyptian date/number formatting). Exact digit style (0–9 vs Arabic-Indic) is **TBD**.
- UI strings authored in Arabic first; `en` as translation files.
- `dir` switches with language (RTL for `ar`, LTR for `en`).
- Layout uses logical CSS (`margin-inline-start`, not `margin-left`) so English does not break.
- Dates, numbers, currency formatting via locale (currency **TBD**, proposed EGP in Phase 1 — not locked).
- Database stores Unicode text; product names as entered (Arabic). Optional English product name column only if the business later wants it — **not assumed**.
- Excel export: sheet RTL + Arabic headers when UI language is Arabic (and still Arabic headers on internal reports unless the user switched — **TBD** whether export follows UI language).
- Printed documents: Arabic-first templates.

Do not build English screens and translate later.

### Why it fits Petro Trans

Locked product requirement, not a theme.

---

## 15. Printing and document generation

### Options considered

- Only Excel print
- JS PDF libraries only (Arabic shaping is historically fragile)
- QuestPDF / similar
- HTML/CSS print templates rendered in WebView2 (print dialog + print to PDF)

### Recommendation

**HTML/CSS print templates** (Arabic RTL, same design language as the app) rendered in the desktop WebView2 print preview/dialog for invoices and reports the user prints daily.

**PDF file save:** WebView2 print-to-PDF and/or QuestPDF if pixel-perfect files are needed. Prefer HTML print first because RTL invoices match the screen.

Excel remains the export path for analysis; print is for documents.

**TBD:** invoice legal footer, tax ID on print, logo file.

### Why it fits Petro Trans

Warehouse and sales staff need to print from the desktop app, in Arabic, without a browser.

---

## 16. Backup and restore

### Recommendation

- System of record = SQLite file + any attachments folder (future product images, import originals).
- **Backup:** native SQLite backup API (safe while app is open) to a user-chosen path via Windows dialog. Include a manifest (app version, timestamp, user).
- **Restore:** choose backup file, confirm destructive restore, replace DB, restart session. Permission: Admin / Owner.
- Keep the last import files inside an audited imports folder (for dispute/replay), not as the database.
- Remind in UI that backups should also live off the PC (USB / other drive). Not a cloud backup product unless later requested.

Scheduled backup: **TBD**.

No invented cloud backup vendor.

### Why it fits Petro Trans

Replacing Excel must not make data *harder* to copy. One-file backup is a feature of SQLite.

---

## 17. Security

### v1 posture

- API listens on **loopback only**.
- No public ports, no remote admin, no default router exposure.
- Authentication required after install (even for the first admin — created by a first-run setup wizard).
- Passwords hashed; no secrets in source control.
- RBAC on every mutating endpoint.
- Audit log for: inventory adjustments, invoice edits (if allowed), returns, payments, user/permission changes, price overrides, Excel import commits, backup/restore.
- Desktop DB file ACL under the Windows user profile.
- Disable WebView2 remote debugging in production builds.
- Updates signed if a certificate is approved (**TBD** — SmartScreen).
- SQL injection avoided via EF Core parameterized queries.
- Excel files treated as untrusted input (size limits, no macro execution; `.xlsx` is zip+xml, not VBA).
- Do not log sensitive tokens; do log who did what.

**Encryption at rest (SQLCipher):** optional later, **TBD**. Not required to start if the PC is already Windows-user locked; recommend OS BitLocker at the business level (owner decision, not an app feature).

### Why it fits Petro Trans

This is confidential wholesale pricing, customer balances, and stock. Local-first reduces internet attack surface; it does not reduce the need for users, audit, and backup.

---

## 18. Future offline capability

v1 **is already offline** for a single PC: no internet is required for daily work.

What must be in the data model **now** so Phase 11 is possible:

- UUIDv7 IDs generated on the device
- `created_at` / `updated_at` (UTC)
- `updated_by` / device or installation ID
- Soft deletes where needed
- Idempotency keys on posted documents (prevent duplicate invoices on retry)
- Import batch IDs
- No server-generated-only identity for business documents

**Do not implement** a sync client, queue, or conflict UI now.

---

## 19. Future synchronization

**Not implemented in this phase.** Design constraints only.

When Phase 11 starts, the document must explain before coding:

- Conflict resolution
- IDs
- Timestamps
- Sync state
- Duplicate prevention
- Transaction ordering
- Failure recovery

**Intended direction (not a locked Phase 11 design):**

- v1: one desktop, one SQLite.
- Later: central server (same ASP.NET domain + PostgreSQL).
- Clients push/pull document batches (invoices, payments, movements), not row-level Excel-style cell sync.
- Inventory conflicts: last-write-wins is **unsafe**; prefer document-level sync + server-side ledger append.
- Price override and permission changes: server-authoritative.

Exact conflict rules: **TBD in Phase 11**, not now.

Public website will consume a **read-only published catalog** later, not the live ERP database.

---

## 20. Deployment and updates

### Recommendation

- Windows 10/11 x64 installer (NSIS or MSIX).
- **Velopack** (or MSIX appinstaller) for in-app updates.
- WebView2 Evergreen runtime (already on current Windows 11; installer can bootstrap on older 10).
- First-run: create data directory, create first Admin / Owner user, optional restore from backup.
- Update channel: replace app binaries; **never overwrite the SQLite file**.
- Migrations run on startup (after Phase 3/7 exists), versioned, backward-compatible where possible.

**TBD:** code-signing certificate (recommended so Windows SmartScreen does not scare staff). Paid certificate is an owner cost decision.

No cloud hosting required for v1.

### Why it fits Petro Trans

Staff launch an icon; IT is not a second job; updates must not wipe stock.

---

## 21. Testing strategy

No implementation now. Planned layers:

1. **Domain unit tests** — pricing resolution order; stock availability; movement+cache consistency; import VALID/WARNING/ERROR classification; permission checks (override denied for Sales).
2. **Application tests** — posting an invoice creates movements and cannot oversell (given TBD policy = block).
3. **Import tests** — synonym mapping; user mapping override; invalid rows excluded.
4. **Excel export tests** — RTL, headers, frozen row, totals (snapshot or Open XML assertions).
5. **API tests** — RBAC 403s; localhost-only binding in v1 config.
6. **Manual UAT** — Arabic UI, print, backup/restore, desktop icon, sample **anonymized** Excel from the business (when provided).

Do not test fake profitability. Do not seed invented prices/customers as “production” data.

---

## 22. Project / module structure

Proposed monorepo (names illustrative):

```
petro-trans/
  docs/                          # Phase documents (this file)
  src/
    PetroTrans.Desktop/          # WPF/WinUI WebView2 host, icon, updater, file dialogs
    PetroTrans.Api/              # ASP.NET Core endpoints
    PetroTrans.Application/      # use cases
    PetroTrans.Domain/           # entities, ledger, pricing, RBAC contracts
    PetroTrans.Infrastructure/   # EF Core, SQLite, ClosedXML, files, audit
    PetroTrans.Web/              # React + TypeScript + Vite (Arabic-first UI)
  tests/
    PetroTrans.Domain.Tests/
    PetroTrans.Application.Tests/
    PetroTrans.Web/              # later, if needed
```

Logical product modules (UI + application services), not separate deployables:

- Dashboard, Catalog, Pricing, Inventory, Purchasing, Customers, Sales, Returns, Payments, Reports, Users, Settings
- Cross-cutting: Auth/RBAC, Audit, Excel, i18n, Printing, Backup

Public website: **out of this repo’s runtime**. A future separate project may read a published catalog API.

---

## Recommended stack summary

| Layer | Choice |
|---|---|
| Desktop shell | .NET 8 + WebView2 (WPF host default) |
| UI | React + TypeScript + Vite, Arabic-first RTL |
| API / domain | ASP.NET Core, localhost-only in v1 |
| Database | SQLite (portable toward PostgreSQL) |
| Excel | ClosedXML |
| Print | HTML/CSS in WebView2; PDF as needed |
| Auth | Local users + RBAC + audit |
| Updates | Velopack or MSIX |
| Sync / public site | Not in v1 |

**Runner-up if C# is declined:** Tauri 2 + React + NestJS sidecar + SQLite + ExcelJS. Weaker Excel and a messier future-server story; acceptable only if the owner rejects C#.

**Rejected for v1:** browser-only, PWA-as-primary, Electron-as-primary, native XAML-as-primary, cloud-required SaaS.

---

## Phase 1 TBD register (unchanged — not invented)

The following remain open. Architecture allows them; it does not decide them.

- SKU uniqueness, barcodes, pack composition, code assignment
- Units of measure (carton vs piece vs liter)
- Warehouse/branch count; v1 one warehouse vs many
- Opening stock/balance cut-over
- Sales order in/out of v1
- Tax / VAT / Egyptian e-invoicing
- Payment-to-invoice allocation vs account-level
- Credit limits, payment terms
- Costing method; profitability reports
- Invoice numbering and print legal text
- Returns financial model (credit note vs refund vs exchange)
- ADNOC shipment field list; suppliers besides ADNOC
- Currency (EGP proposed, not locked)
- Negative stock (block proposed by Phase 1 brief)
- Customer type list closed or not; multiple contacts
- Invoice edit-after-post
- Supplier payments in v1
- Delivery/driver documents
- Discount line vs document
- Min-stock: alert vs block
- Excel partial commit vs all-or-nothing
- Historical import vs live ledger
- Creating unknown masters during import
- `.xls` support
- Import permission by role
- Branding assets
- Date/number/digit locale details
- Payment method names (configurable, list empty until provided)
- Actual prices, customers, suppliers
- “Authorized ADNOC distributor” as public/legal wording (unconfirmed)
- Branch Manager as a distinct role
- Encryption at rest; code signing; scheduled backup

---

## DECISIONS REQUIRING BUSINESS OWNER APPROVAL

These are architecture choices, not invented commercial policies. Please approve or reject before Phase 3 (database schema).

1. **Desktop + backend stack**  
   Approve **.NET 8 WebView2 host + ASP.NET Core + React + SQLite + ClosedXML** as the v1 architecture?  
   If no: the runner-up is Tauri 2 + React + NestJS + SQLite + ExcelJS.

2. **Single-PC local-first v1**  
   Approve that the first production deployment is one Windows application + one local SQLite file (no cloud server, no always-on internet)?  
   Multi-user/sync remains a later phase.

3. **Local application login**  
   Approve app users (username/password) rather than relying only on the Windows account, even if only one person uses it at first?

4. **Owner/Manager role**  
   Approve a distinct role for your father with exclusive price-override permission (and later other sensitive actions as you define)?

5. **Price override reason**  
   Should a written reason be **required** on every manual price override? (Recommended: yes.)

6. **Posted invoice immutability**  
   Until Phase 4 accounting rules exist: approve that posted invoices are not silently edited — corrections go through a later return/credit process?

7. **Negative stock**  
   Confirm launch rule: **cannot sell more than available stock**?

8. **Currency**  
   Confirm **EGP only** for v1?

9. **Excel file type**  
   v1 import/export **`.xlsx` only** (modern Excel), not old `.xls`?

10. **Partial Excel import**  
    If some rows are valid and some are errors: after confirmation, **commit valid rows and skip errors**, or **reject the whole file**? (Recommendation: commit valid rows, with a clear summary — not silent.)

11. **Windows code signing**  
    Approve purchasing a code-signing certificate later so Windows SmartScreen does not block the Petro Trans icon? (Can be after first internal builds.)

12. **BitLocker / PC disk encryption**  
    Confirm this is an OS practice for the business PC, not something the app must encrypt at rest in v1?

13. **Public website**  
    Confirm it stays a **separate later project**, not part of this desktop architecture?

14. **C# + TypeScript split**  
    Accept two languages (C# domain/Excel/desktop, TypeScript UI) as the cost of the recommended stack?

No schema, code, scaffolding, or Phase 3 work will start until this Phase 2 document is explicitly approved.
