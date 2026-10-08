# Petro Trans — Phase 4 Business Rules & Workflows

**Status:** Updated with credit-sale / payment clarification (16 August 2026). D9 and credit-sale payment rules locked. Remaining TBDs unchanged unless listed as affected. Phase 5 not started.  
**Date:** 16 August 2026  
**Scope:** Analysis and documentation only. No application code, migrations, database changes, UI, or Phase 5.

Source of truth: locked requirements from Phases 1–3, plus the D9 clarification in this revision. Unknown rules stay **TBD**. Nothing below invents VAT, payment method names, currency, costing, return accounting, discount policy, invoice numbering format, warehouse names, UOM, ADNOC fields, or historical-import posting.

---

## How to read this document

| Marker | Meaning |
|---|---|
| **LOCKED** | Already decided in Phases 1–3. Workflows must follow it. |
| **TBD** | Not decided. Options are listed. Do not implement a default as if it were policy. |
| **Phase 5** | Who holds a permission (except `pricing.override`) is assigned in Phase 5. This phase names the **permission**, not the full role matrix. |

Roles that exist: `admin`, `sales`, `warehouse`, `accountant`, `owner_manager`.  
**LOCKED:** only `owner_manager` has `pricing.override`. `admin` does **not** get it automatically.

---

## Product principle (LOCKED)

Petro Trans is a real small B2B business. The system must match the simplicity of the actual workflow.

- Professional internally.
- Secure and **auditable in the background**.
- Very simple and fast for the actual user.
- Flexible enough for the business owner to decide remaining policies later.

Do **not** over-engineer a process because an ERP “best practice” exists. Do **not** add approval chains, mandatory justifications, extra confirmation screens, or extra steps unless the owner explicitly asks for them.

### Remaining TBDs (LOCKED handling)

- Do **not** decide remaining Phase 4 TBDs automatically.
- Keep them **TBD** until the owner explicitly provides the rule.
- Where a later choice is appropriate, expose it as a **simple Settings / `business_policies` option** — not as a hardcoded complicated workflow.
- Until a TBD is set, keep the already-stated **safe operational behaviour** (for example: do not invent prices, do not post return finance, do not treat historical imports as live ledgers). That is a hold, not a hidden business policy.

---

## Cross-cutting rules (apply to every workflow)

### LOCKED

1. Arabic-first validation and error messages in the UI.  
2. One database transaction per posting action. On failure: **full rollback** — no partial stock, no partial balance, no committed import rows from a failed transaction.  
3. Quantity changes **only** via `inventory_movements`. Never overwrite `inventory_balances` alone.  
4. Every movement has `variant_id`, `warehouse_id`, type, quantity, direction, source document, datetime, user, notes.  
5. **Draft** documents do not move stock and do not change customer/supplier balances.  
6. Customer balance is derived from posted transactions (plus opening balances when committed). It is not a free-typed field.  
7. Payment methods are configured in Settings. The list is empty until the owner adds methods. A payment cannot be posted if no method is selected from that list.  
8. Sales orders and purchase orders are **not mandatory**.  
9. Product ≠ variant. Sales, stock, and prices are at **variant** level.  
10. If a SKU is entered, it must be unique per variant. Barcode is optional and not unique until a barcode policy exists.  
11. No tax engine. No costing method. No ADNOC-specific columns. No public-website coupling.  
12. Sensitive actions write `audit_log` as specified in Phase 3. Audit is background; it must not add friction to the user’s steps.  
13. Excel invalid rows never enter the database silently.  
14. **D9 LOCKED:** price override reason/note is **optional**. The owner types the new price directly. No mandatory justification, approval workflow, or extra confirmation screen.  
15. **Credit sale LOCKED:** posting an invoice records the sale and reduces stock. It does **not** mean the customer paid. Payment is a separate later action. Do not treat a posted invoice as paid.

### TBD (do not assume)

VAT, currency display/code, negative-stock exception, return finance, discounts, invoice number format, warehouse count/names, UOM conversions, historical import posting, credit-limit **enforcement**, posted-invoice edits, supplier payments in v1, Excel commit mode, `.xls` support, unallocated prepayment (D14).

**Closed by this clarification (no longer TBD):** D12 (simple apply-to-invoice), D6 as a reminder/blocking engine (v1 is due date / payment term only). See Workflows 1, 2, 4.

### Document statuses (technical)

`draft` → `posted` → (`voided` only if later approved).  
`voided` is ALLOWED in the model. **Whether anyone may void, and how, is TBD.**

---

# 1. Sales lifecycle

**LOCKED flow (credit sale is normal):**

Customer → invoice for agreed prices → **post** (sale recorded, stock out, full amount outstanding) → customer may pay **later** → payment(s) recorded separately → remaining decreases → when remaining is 0, invoice is fully paid.

Payment is **not** required when goods are received or when the invoice is posted.

### v1 invoice money fields (**LOCKED**, keep simple)

| Concept | Meaning |
|---|---|
| Invoice Total | Agreed line totals after any later-approved discount policy (discount itself still **TBD D3**) |
| Paid Amount | Sum of posted payments applied to this invoice. **0 at post.** |
| Remaining Amount | Total − Paid |
| Due Date / Payment Term | Optional expectation of **when** the customer should pay. Owner may set it when needed. Not hardcoded. |
| Payment Status | `unpaid` (paid = 0) / `partial` (0 < paid < total) / `paid` (remaining = 0) |

**Document status** (`draft` / `posted`) is not payment status. Posted + unpaid is a normal credit sale.

Do **not** add: GL, installment plans, approval of credit, dunning/reminder engines, or “mark as paid” without a payment record.

### Trigger

User starts a wholesale sale for an existing customer.

### User / role

A user with `sales.draft` (create/edit draft) and, to complete the sale, `sales.post`. Exact role assignment: **Phase 5**.

### Preconditions

- Authenticated user.  
- Customer exists and is not soft-deleted.  
- At least one active product **variant**.  
- At least one warehouse exists (setup data — **name/count TBD**, but a movement cannot post without a warehouse).  
- Sales order is **not** required.

### Steps

1. Select customer.  
2. Optionally attach a sales order if one exists (**TBD D4** whether the UI shows orders in v1).  
3. Add lines: variant, warehouse, quantity.  
4. System proposes unit price via pricing resolution (Workflow 3).  
5. User may change price only with `pricing.override` (`owner_manager` only).  
6. Optionally set **due date** and/or **payment term** (from Settings terms if any, or a date the owner types). Not required to post.  
7. Save as **draft** (repeatable).  
8. Post invoice (Workflow 2) — this is **not** a payment.  
9. Later, when money is actually received: record payment(s) (Workflow 4). Partial payments allowed. Remaining stays visible until 0.

### Validation

- Customer required.  
- At least one line.  
- Variant active.  
- Quantity > 0.  
- Warehouse required on each line (or document-level warehouse — **TBD** document vs line warehouse; until decided, **each stock effect needs a warehouse**).  
- Unit price required to **post** (may be missing on draft). Do not invent a price.  
- Discount: **TBD** — unused until a discount policy is approved.  
- Tax: **OUT**.  
- Oversell: see Workflow 2.

### State changes

Draft invoice + lines, price snapshots. Status `draft`.

### Inventory effect

None until post.

### Financial / balance effect

None until post. After post, see Workflow 2: outstanding on the customer, **not** paid.

### Audit effect

Draft save is ordinary data change. Posting, override, later edits, payments: see those workflows.

### Failure / rollback

Draft save failure: no document. No ledgers involved.

### Open business decisions

- **D1** Invoice numbering format (when to assign: on draft vs on post).  
- **D2** Warehouse on header vs per line.  
- **D3** Discount: none / line / document / both.  
- **D4** Show sales orders in v1 UI at all?  
- **D5** Credit limit: ignore / warn / block posting. **Unchanged TBD** — credit *sales* are allowed; whether a *credit limit* is enforced is still unknown.  
- **D6 closed for v1:** due date / payment term on the invoice, optional, configurable. No reminder engine, no overdue-blocking workflow unless the owner later asks.

---

# 2. Invoice posting and stock deduction

Posting = **record the sale + reduce inventory**. It is **not** recording money received.

### Trigger

User posts a draft sales invoice.

### User / role

`sales.post`. **Phase 5** who holds it.

### Preconditions

- Invoice is `draft`.  
- All post validations in this section pass.  
- **No payment is required.** Credit sale is valid.

### Steps (single transaction)

1. Re-validate lines, prices, stock.  
2. Assign invoice number from `number_series` **if a series is configured**. If not configured, **cannot post** until numbering is decided (D1) — do not invent `INV-0001`.  
3. Insert `sale` movements (`direction = out`) per line: variant, warehouse, qty, source = this invoice.  
4. Update `inventory_balances` in the same transaction.  
5. Insert `party_ledger_entries` type `sales_invoice` for the customer (increases amount owed by **Invoice Total**).  
6. Set document status `posted`.  
7. Set money fields: Paid Amount = 0, Remaining Amount = Invoice Total, Payment Status = `unpaid`.  
8. Keep Due Date / Payment Term if the user entered them; they do not change stock or paid amount.  
9. Write audit `sales.post` (and `pricing.override` if any line was overridden).

Do **not** create a payment row. Do **not** set Payment Status to `paid`.

### Validation

- Same as Workflow 1, plus:  
- Every line has a unit price > 0 (or = 0 only if owner later allows free goods — **TBD D10**, default: price required, do not assume free goods).  
- **Stock:** Phase 1 locked that the system must not sell more than available **unless** an explicit negative-stock/backorder policy is approved. Until **D7** is answered, **posting that would take on-hand below zero is rejected**.  
- Idempotency key: posting twice must not double stock or double the balance.  
- Due date / payment term: optional; invalid date rejected; **not** required.

### State changes

`draft` → `posted`. Line prices frozen. Payment Status = `unpaid`. Paid = 0. Remaining = Total.

### Inventory effect

**Yes.** `sale` / out. On-hand decreases **immediately** because the goods left the warehouse — whether or not the customer paid.

### Financial / balance effect

**Yes — outstanding only.** Customer owed increases by Invoice Total. Invoice is **unpaid**. Customer account remaining increases by the same amount. No cash/bank movement.

### Audit effect

Required: invoice post. Required: price override if used.

### Failure / rollback

Any validation or write error: **rollback entire post**. Invoice remains `draft`. No movements. No ledger. Number series is not consumed (or is consumed only after successful post — **implementation must not skip numbers on failed post**).

### Posted invoice edits — TBD (D8)

| Option | Meaning |
|---|---|
| A. Forbid | Posted invoices cannot be edited. Corrections via return / later credit process. |
| B. Allow with permission | `sales.edit_posted` + audit; **stock/finance restatement rules must then be defined** (not defined today). |
| C. Void + replace | Void (TBD) then new invoice. |

Until D8 is chosen, **do not silently edit posted invoices.** Changing Payment Status without a payment record is **never** allowed.

### Open business decisions

- **D1** Number format and when assigned.  
- **D7** Negative stock: keep **block** (Phase 1 default) or allow backorder.  
- **D8** Posted invoice edits.  
- **D3** Discounts (affects Invoice Total).  
- Due date / payment term: **locked as optional fields**, not TBD. Term *names* are Settings (empty until the owner adds them), same pattern as payment methods.

---

# 3. Pricing resolution and owner price override

### Trigger

A sales line is added or its variant/customer changes; or `owner_manager` changes the proposed price.

### User / role

- Viewing / using resolved price: users who can draft sales.  
- **Changing** a line away from the resolved price: **only `owner_manager`** (`pricing.override`). **LOCKED.**  
- Editing master prices (`standard`, customer-type, customer-specific): `pricing.edit_masters` — **Phase 5** who holds it. Not the same as override.

### Preconditions

- Customer and variant known for resolution.  
- Override: caller has `pricing.override`.

### Steps — resolution (**LOCKED** order)

1. If `customer_prices` exists for (customer, variant) → use it.  
2. Else if customer has a type **and** `customer_type_prices` exists for (type, variant) → use it.  
3. Else if `product_variants.standard_wholesale_price` is not null → use it.  
4. Else **no price**. Do not invent one. Draft may be saved; **post is blocked** until a price exists (master or override).

Missing layers are valid. Not every type or customer needs a special price.

### Steps — override (**LOCKED** — keep it fast)

The owner/manager changes a price in place. The process must stay simple.

1. `owner_manager` types the new unit price directly on the line (or price field).  
2. An **optional** note/reason field may be visible. It is **not** required. Empty is allowed.  
3. Store on the line: `unit_price` (new), `price_source = manual_override`, `resolved_unit_price` (old/proposed price), `override_by_user_id`, optional `override_reason`.  
4. **No** approval workflow, **no** mandatory justification, **no** extra confirmation screen, **no** extra steps.  
5. In the **background**, write `audit_log` (`pricing.override`) with:
   - user  
   - date/time  
   - old price (`resolved_unit_price` / previous unit price)  
   - new price  
   - affected product, variant, customer, and invoice (when in context)

### Validation

- Override by anyone except `owner_manager` → deny. Admin without this permission → deny.  
- Override price must be a valid number. **Whether 0 is allowed is TBD (D10).**  
- **Do not** reject an override because the note is empty.  
- Master price rows: if price lists are edited, that does **not** rewrite already posted invoice lines (snapshots).

### State changes

Draft line updated. Posted lines: not changed by later master-price edits.

### Inventory effect

None.

### Financial / balance effect

None until the invoice is posted (then the overridden price is what is owed).

### Audit effect

**Background, automatic, required.** Every successful override records user, date/time, old price, new price, product, variant, customer, invoice. This must not add UI steps. Master price create/update should be auditable as `pricing.edit_masters` the same way.

### Failure / rollback

Denied override: line price unchanged. No success audit.

### Open business decisions

- **D10** Allow unit price 0? (configurable later if useful; not decided)  
- **D11** May `owner_manager` also edit price masters, or is that a separate permission only? **Phase 5** / Settings later — not decided.  
- **D9 is closed:** reason optional.

---

# 4. Payments and customer balances

Recording a payment = **money was actually received**. It is a different action from posting the invoice.

### Trigger

The customer pays some or all of an outstanding invoice (same day or later). Not automatic at invoice post.

### User / role

`payments.create`. **Phase 5.**

### Preconditions

- Customer exists.  
- There is a **posted** invoice to apply the payment to (credit-sale remaining > 0), unless D14 later allows unallocated prepayment — **D14 still TBD**; v1 daily path is pay against an invoice.  
- At least one `payment_methods` row exists and is selected. **If Settings has no methods, payment cannot be posted.** Do not invent cash/transfer/cheque.  
- Amount > 0.

### Steps (**LOCKED** — keep simple; D12 closed)

No allocation engine, no installment schedule, no approval.

1. Select customer.  
2. Select the posted invoice being paid (the normal daily path is **one invoice**).  
3. Enter amount (may be **partial** — less than Remaining Amount), date, method (Settings), optional reference/notes.  
4. Optional: if the customer pays several invoices at once, repeat the same simple apply (or a short list of invoice + amount). Do **not** build a complex allocation/FIFO engine.  
5. Post in one transaction:
   - payment row applied to that invoice  
   - customer ledger `payment` (owed decreases)  
   - invoice Paid Amount += this amount  
   - invoice Remaining Amount = Total − Paid  
   - Payment Status = `unpaid` / `partial` / `paid` from those numbers  
6. Background audit `payments.create`.

When Remaining Amount reaches 0, Payment Status = `paid`. Customer outstanding for that invoice is 0.

### Validation

- Method must be an active configured method.  
- Amount > 0.  
- Invoice must be posted, same customer, Remaining Amount > 0.  
- Amount cannot exceed that invoice’s Remaining Amount (simple check — not a prepayment product). **D14** (pay more than the customer owes overall / unallocated extra) stays **TBD**; do not invent on-account credit wallets.  
- Currency: **TBD D13** — amounts stored as numbers; no FX.

### State changes

Payment posted. Invoice Paid / Remaining / Payment Status updated. Customer outstanding = sum of Remaining Amount on posted invoices (+ opening balances if any).

### Inventory effect

**None.** Stock already moved at invoice post.

### Financial / balance effect

**Yes.** Money received. Invoice remaining down. Customer account remaining down. Does not change Invoice Total.

Customer outstanding (simple):

`sum(Remaining Amount of posted invoices) + opening balances`  
(returns **excluded** until D18).

### Audit effect

Required. Background: user, date/time, customer, invoice, amount, method.

### Failure / rollback

Full rollback. Invoice Paid/Remaining unchanged. No half-applied payment.

### Open business decisions

- **D12 closed:** apply payment to the invoice (simple). Not account-only without invoice remaining. Not a complex allocation engine.  
- **D13** Currency code.  
- **D14** Unallocated prepayment / pay more than owed — still TBD.  
- **D15** Who may cancel/void a posted payment? Until then, no silent delete.

---

# 5. Sales returns

### Trigger

User records a sales return, preferably against an original invoice.

### User / role

`returns.create`. **Phase 5.**

### Preconditions

- Original invoice preferred (**LOCKED:** reference where possible). If missing, **TBD D16**.  
- Variants being returned exist.

### Steps

1. Select original posted invoice if known.  
2. Select lines/qty to return (cannot exceed original line qty minus already returned — **recommended LOCKED integrity**, not a financial policy).  
3. Save draft.  
4. Post **stock** effect: `sales_return` movements `direction = in`, same warehouse as the original sale **if known**, else warehouse required on the return (**TBD D17** if original warehouse missing).  
5. **Financial effect: not posted until D18 is chosen.** `balance_posting_status = not_posted`.  
6. Audit `returns.create` (and post).

### Validation

- Quantity > 0.  
- If linked to invoice: variant must appear on that invoice; qty ≤ remaining returnable qty.  
- Time window for returns: **TBD D19** (none / N days).  
- Resolution type credit vs refund vs exchange: **TBD D18**.

### State changes

Return document `posted` for **inventory** (if stock post succeeds). Finance flag remains not posted until D18.

### Inventory effect

**Yes, when posted for stock.** On-hand increases. This is LOCKED capability from Phase 1 (“restore inventory”).

### Financial / balance effect

**None until D18.** Options:

| Option | Balance effect | Typical document |
|---|---|---|
| A. Credit | Decrease customer owed (credit note) | No cash out |
| B. Refund | Decrease owed **and** record money out / negative payment | Cash/bank out |
| C. Exchange | Stock in + new sale/out; money only if prices differ | |
| D. Stock-only | Inventory restore; customer balance unchanged | |

Until you choose, the system **must not** change customer balance on return.

### Audit effect

Required.

### Failure / rollback

Full rollback of that post. No stock in without the return document.

### Open business decisions

- **D16** Allow return without original invoice?  
- **D17** Warehouse if original unknown.  
- **D18** Return accounting (A–D above).  
- **D19** Time limit.  
- **D20** Restocking condition / damaged goods (different warehouse or adjustment instead)? **Do not invent;** if unused, all returns go to the chosen warehouse as good stock.

---

# 6. Purchasing and receiving

**LOCKED:** suppliers, receiving, purchase invoices, quantities, purchase cost as **document data**. PO **not mandatory**. ADNOC fields **not invented** (optional key/value only if the owner later lists keys).

### Trigger

Inbound goods from a supplier and/or a supplier bill.

### User / role

`purchasing.manage` and `inventory.receive`. **Phase 5.** May be the same or different people.

### Preconditions

- Supplier exists (created in Settings/Purchasing masters — **none seeded**).  
- Warehouse exists.  
- PO not required.

### Steps — receive stock (goods receipt)

1. Optional: link inbound shipment (generic reference, date, notes; extra attributes only when provided).  
2. Optional: link PO if one exists.  
3. Lines: variant, qty, warehouse, optional unit cost **as entered** (not a costing method).  
4. Post: `purchase_receipt` movements `in` + cache + audit.

### Steps — purchase invoice (supplier bill)

1. Supplier, date, lines (variant, qty, unit cost), optional link to receipt.  
2. Post: supplier `party_ledger_entries` type `purchase_invoice` (increases payable).  
3. **Does not** by itself move stock unless you later require invoice-and-receive as one document (**TBD D21**). Default from Phase 1 modules: **receiving and purchase invoice are separate**. That is a workflow split, not an invented accounting standard.

### Validation

- Qty > 0.  
- Variant required.  
- Warehouse required on receipt.  
- Purchase cost: optional on receipt; **required or optional on purchase invoice is TBD D22**. Do not invent costs.  
- Purchase returns: tables ALLOWED; **use in v1 TBD D23**.

### State changes

Shipment / receipt / purchase invoice posted as applicable.

### Inventory effect

Receipt **yes**. Purchase invoice **no** (unless D21 merges them).

### Financial / balance effect

Purchase invoice **yes** (supplier owed increases). Receipt **no** (stock only), unless D21 says otherwise.

### Audit effect

Receiving and purchase-invoice post.

### Failure / rollback

Per document, full rollback.

### Open business decisions

- **D21** Must every receipt have a purchase invoice (or vice versa), or keep them independent?  
- **D22** Is unit cost required on the bill?  
- **D23** Purchase returns in v1?  
- **D24** Shipment extra fields the business actually uses (list them; otherwise none).

---

# 7. Supplier balances and supplier payments

### Trigger

Need to see what Petro Trans owes a supplier, and/or record a payment to a supplier.

### User / role

`payments.create` and/or `purchasing.manage`. **Phase 5.** Inclusion of this workflow in **v1 is TBD D25**.

### Preconditions

- If D25 = no: do not expose supplier payments in v1; supplier **balance from purchase invoices** can still be reported.  
- If D25 = yes: payment method from Settings required, same as customer payments.

### Steps (only if D25 = yes)

Analogous to Workflow 4, party = supplier. Allocation to purchase invoices **TBD** (same D12 family or separate **D26**).

### Validation

Same pattern as customer payments. No invented methods.

### State changes / inventory / finance / audit / rollback

Same pattern as Workflow 4, opposite party. Inventory none.

### Open business decisions

- **D25** Include supplier payments in v1?  
- **D26** Allocate supplier payments to bills vs account-level?

---

# 8. Inventory adjustments and transfers

### 8.1 Adjustments

#### Trigger

Physical count difference, damage, or correction. **Not** a substitute for sales/purchases.

#### User / role

`inventory.adjust` only. **Phase 5** who holds it. Sensitive.

#### Preconditions

- Variant, warehouse, reason **required** (Phase 1/3: adjustments need reason).  
- Quantity > 0; user chooses in or out.

#### Steps

1. Enter variant, warehouse, direction, qty, reason, notes.  
2. Post: `adjustment_in` or `adjustment_out` + cache.  
3. Audit `inventory.adjust` **and** the movement (ledger).

#### Validation

- Reason required.  
- Permission required.  
- Adjustment **out** that would take on-hand below zero: follow **D7** (until allow-negative is approved: **block**).

#### State changes

Adjustment document posted.

#### Inventory effect

Yes.

#### Financial / balance effect

**None** (no costing method; do not post “inventory loss” to money). **OUT** of GL.

#### Audit effect

Required.

#### Failure / rollback

Full rollback.

### 8.2 Transfers

#### Trigger

Move stock between warehouses.

#### User / role

`inventory.adjust` or a future `inventory.transfer` — **Phase 5**.  

#### Preconditions

- **D27** Multi-warehouse in use. If only one warehouse exists, **transfer UI is hidden / rejected**. Do not invent a second warehouse.

#### Steps (when two+ warehouses exist)

1. From warehouse, to warehouse, variant, qty.  
2. One transaction: `transfer_out` + `transfer_in` + two cache updates + `stock_transfers` header.  
3. Audit.

#### Validation

- From ≠ to.  
- Qty > 0.  
- Source on-hand: follow D7.

#### Inventory / finance / audit / rollback

Net company stock unchanged. No customer/supplier money. Full rollback on failure.

#### Open business decisions

- **D7** Negative stock (also applies here).  
- **D27** How many warehouses in v1, and their names (setup data — not invented here).  
- **D28** Min-stock: alert only / ignore / block sales when at or below min.

---

# 9. Customer management

### Trigger

Create/update a B2B customer.

### User / role

`customers.manage`. **Phase 5.**

### Preconditions

Authenticated user with permission.

### Steps

1. Enter name (required), type (optional; types come from editable Settings lookup — **not seeded**), contact person, phone, WhatsApp, address.  
2. Credit limit: **nullable**; enforcement **TBD D5**. Credit *sales* do not require a credit limit.  
3. Customer-level default payment term: optional convenience later; **not required**. Invoice due date / term is set on the sale when needed (Workflow 1).  
4. Tax fields: **not required**; **TBD D29** if needed later.  
5. System assigns `CUS-0001` style code from `number_series` (**LOCKED** format).  
6. Soft-delete deactivates; history remains. Hard delete of a customer with posted documents: **forbidden**.

### Validation

- Name required.  
- Code unique.  
- Type, if set, must exist in `customer_types`.  
- Do not type `current_balance`. Balance is computed.  
- WhatsApp: stored number only (no API).

### State changes

Customer master row. No stock. Opening balance is Workflow 17, not this screen’s free field.

### Inventory / finance

None (except later opening-balance import).

### Audit effect

Not in the Phase 3 “must audit” list as a named action; recommend audit of create/update/delete of customers as `customers.manage`. **Not a new business policy.**

### Failure / rollback

No customer row.

### Open business decisions

- **D5** Credit limit enforcement.  
- **D6 closed** at invoice level (optional due date / term). Customer default term is optional later, not a v1 engine.  
- **D29** Tax registration fields later?  
- **D30** Multiple contacts vs the single contact person already on the record (Phase 1 listed one). Recommendation: one contact unless you ask for more.

---

# 10. Product / variant management

### Trigger

Maintain catalog: parent product and packaging variants.

### User / role

`catalog.manage`. **Phase 5.** Linking images: only files already in `assets/` after inspection. Logo path **TBD**. Do not download images.

### Preconditions

Permission.

### Steps

1. Create **product**: name, brand, category, specification/API, active.  
2. Create **variant(s)**: packaging type, packaging size, optional SKU (unique if present), optional barcode, min stock, optional standard price, active.  
3. Do not treat كرتونة 3X4 and 12X1 as two products.  
4. Deactivate instead of deleting if movements exist.

### Validation

- Product name required.  
- Variant packaging type and size required (as labels; **UOM conversion TBD D31** — no inner-unit math until then).  
- SKU unique if provided.  
- Barcode optional, not unique.  
- Standard price optional; do not seed prices.  
- Min stock stored; **action on breach TBD D28**.

### State changes

Masters only.

### Inventory / finance

None until opening stock / receipts / sales.

### Audit effect

Recommend `catalog.manage` on create/update. Price master edits: `pricing.edit_masters`.

### Failure / rollback

No partial product+variant if created as one action.

### Open business decisions

- **D31** UOM / pack composition structured vs label only.  
- **D32** Who assigns SKUs (user vs system).  
- **D28** Min-stock behaviour.

---

# 11. Excel import workflow

**LOCKED pipeline:** upload → analyze workbook → detect sheets/columns → propose mapping → user corrects mapping → validate → preview VALID/WARNING/ERROR → user confirms → commit **valid** rows only according to **D33** → import summary / audit.

No LLM. No silent invalid inserts. Original **absolute path is not** the source of truth.

### Trigger

User imports products, variants, customers, opening stock, opening balances, suppliers, or (separately) historical files (Workflow 18).

### User / role

`excel.import`. **Phase 5 / D34** who may import.

### Preconditions

- Permission.  
- File readable as Excel (**D35** `.xlsx` only vs also `.xls`).  
- User confirms mapping before commit.

### Steps

1. Pick file (Windows dialog).  
2. Analyze: sheets, header row, columns, sample types.  
3. Propose mapping via synonym dictionary (e.g. اسم المنتج / Product Name → product name).  
4. User edits mapping. Optionally save profile.  
5. Validate all data rows.  
6. Show counts and row-level messages (Arabic).  
7. User confirms.  
8. Commit in a transaction (or per D33).  
9. Store batch metadata: purpose, file **name**, mapping snapshot, counts, user, time, row classifications. Optional implementation copy of the file — not a locked path.  
10. Audit `excel.import.commit`.

### Validation (LOCKED classes)

Missing required fields, duplicates, unknown products/customers, invalid qty/date/number, missing identifiers, conflicts, unsupported values, possible duplicates.

| Class | Commit? |
|---|---|
| VALID | Yes, if user confirms |
| WARNING | Only with explicit acknowledgement |
| ERROR | **Never** |

Creating unknown masters during a *sales* import: **TBD D36** (error vs create-after-confirm). For dedicated Products/Customers import, creating masters **is** the purpose.

### State changes

Masters and/or opening documents per purpose. Historical: Workflow 18.

### Inventory / finance

Only for opening inventory / opening balances (Workflow 17). Not for a products-only import.

### Audit effect

Required on commit. Include mapping snapshot and counts.

### Failure / rollback

If the commit transaction fails: no committed rows. Preview data may remain as an uncommitted batch.

### Open business decisions

- **D33** Commit valid rows and skip errors vs reject entire file.  
- **D34** Which roles may import.  
- **D35** `.xlsx` only?  
- **D36** Unknown product/customer on non-master imports.

---

# 12. Excel export / reporting behavior

### Trigger

User exports a module list or a report.

### User / role

`excel.export` and/or `reports.view`. **Phase 5.**

### Preconditions

Permission. Filters chosen by user.

### Steps

1. Apply current filters / period.  
2. Generate `.xlsx` (not CSV) via the export service.  
3. Include, where appropriate: Petro Trans branding **if logo path is set** (else omit logo — **do not invent a file**), title, date, filters, Arabic RTL, Arabic headers, column widths, number formats, totals, tables/filters, frozen headers, professional sheet names, multiple sheets if useful.  
4. Optional `excel_export_jobs` audit.  
5. Open/save via Windows dialog.

### Validation

User authorized. Empty result still produces a titled sheet with “no rows” rather than a fake number.

### State changes

None in ERP masters. File is an output.

### Inventory / finance

None. **Profitability reports are OUT** until a costing method exists (**D37**). Do not show fake margins.

### Audit effect

Optional job record; not as sensitive as import commit.

### Failure / rollback

No DB writes to undo. User sees an error; no partial corrupt file presented as success.

### Open business decisions

- **D13** Currency formatting.  
- **D37** Costing method — until set, no profit report.  
- **D38** Export language: always Arabic vs follow UI switcher.

---

# 13. Permissions and sensitive actions

### Trigger

Any attempt to perform a sensitive or module action.

### User / role

Enforced by permission codes. **LOCKED exception:** `pricing.override` = `owner_manager` only.

### Preconditions

Logged-in, active user.

### Steps

1. Check permission (deny wins if overrides exist later).  
2. Allow or reject with an Arabic message.  
3. Do not hide audit of denials for sensitive modules (recommended).

### Validation

Missing permission → no state change.

### State changes / inventory / finance

None on deny.

### Audit effect

Permission **changes** (`users.manage` / `roles.manage`) **must** audit. Successful sensitive operations audit per Phase 3 list.

### Failure / rollback

N/A (denied before transaction).

### Open business decisions

- **D39** Full role matrix — **Phase 5** (not duplicated as invented grants here).  
- **D40** May `admin` later receive `pricing.override`? Today: **no**.

---

# 14. Audit requirements

### Trigger

Sensitive operations listed in Phase 3.

### User / role

Actor is the logged-in user. `audit.view` to read the log — **Phase 5**.

### Preconditions

None beyond the originating action.

### Steps

Append-only `audit_log`: who, when, action, entity, before/after (no passwords), correlation id.

**Must audit:** inventory adjustments, invoice edits, returns, payments, user/permission changes, price overrides, Excel import commits, backup/restore.

### Validation

Audit write is part of the same business transaction when possible. If audit insert fails, **the business action fails** (do not post silently without audit for those actions).

### State changes

Log row only (+ originating document).

### Inventory / finance

None extra.

### Failure / rollback

Business action rolls back if required audit cannot be written.

### Open business decisions

- **D41** Retention period of audit log? (keep forever vs TBD)

---

# 15. Backup / restore business behavior

### Trigger

User requests backup or restore.

### User / role

`backup.restore` for restore. Backup may be available to `owner_manager` / `admin` — **Phase 5 / D42**.

### Preconditions

- Restore: user understands it **replaces** current data. Confirm in UI (Arabic).  
- Backup: destination chosen via Windows dialog (USB/other disk recommended in copy — not a cloud vendor).

### Steps — backup

1. SQLite safe backup to chosen path + manifest (app version, time, user).  
2. Does not overwrite the live DB.  
3. Audit if treated as a sensitive export of the whole company file (**recommended**).

### Steps — restore

1. Choose backup file.  
2. Confirm destructive restore.  
3. Replace DB, restart session.  
4. Audit `backup.restore`.

### Validation

File looks like a Petro Trans backup (manifest/version). Wrong file → refuse.

### State changes

Restore: entire database replaced.

### Inventory / finance

Whatever was in the backup. Not a business document.

### Audit effect

Restore **required**. After restore, the restored DB’s log is the restored history; keep a note outside if possible (implementation).

### Failure / rollback

Failed backup: live DB untouched. Failed restore: do not leave a half-replaced file; keep previous live DB.

### Open business decisions

- **D42** Who may backup vs restore.  
- **D43** Scheduled backup? (unset)

---

# 16. Error handling and validation

### Trigger

Any user action or import.

### User / role

All.

### Preconditions

N/A.

### Steps / rules (**LOCKED** spirit)

- Validate **before** commit.  
- Show exact errors (import: row numbers).  
- Arabic-first messages.  
- No silent skip of invalid records.  
- Posting uses a transaction: all or nothing **for that action**.  
- Do not display fake profitability or invented prices.  
- Insufficient permission: deny, no partial write.

### Inventory / finance

Unchanged on validation failure.

### Audit effect

Failed attempts on sensitive actions may be logged as denials (**D44**).

### Open business decisions

- **D44** Log permission denials?  
- **D33** Import partial commit (also Workflow 11).

---

# 17. Opening stock and opening balances

### Trigger

Go-live cut-over from Excel: starting quantities and starting customer/supplier owed amounts.

### User / role

`excel.import` and/or a setup permission — **Phase 5**. Sensitive: these **do** hit ledgers.

### Preconditions

- Variants exist (or same import batch creates them **only** in a products+stock combined flow if mapping says so — avoid hidden creates: **D36**).  
- Warehouse exists for stock.  
- Opening **customer** balances: customers exist.  
- Cut-over date: **TBD D45** (user-entered business date).

### Steps — opening stock

1. Import or manual opening document.  
2. Commit: `opening_stock` movements `in` + cache.  
3. **Not** an overwrite of on-hand without a movement.  
4. Audit.

### Steps — opening customer/supplier balances

1. Import/manual opening document per party, amount, date.  
2. Commit: `party_ledger_entries` type `opening_balance`.  
3. Do **not** type the amount into a `current_balance` field as source of truth.  
4. Audit.

### Validation

- Qty/amount valid numbers.  
- Duplicate opening for same variant+warehouse: **TBD D46** (block vs add vs replace via reversing movement).  
- Do not invent quantities.

### State changes

Posted opening documents.

### Inventory effect

Opening stock: yes. Opening AR/AP: no.

### Financial / balance effect

Opening AR/AP: yes. Opening stock: no money (no costing).

### Audit effect

Required (import commit and/or opening post).

### Failure / rollback

Full rollback of that commit.

### Open business decisions

- **D45** Cut-over date handling.  
- **D46** Second opening for the same stock key.  
- **D47** Opening supplier balances in v1?

---

# 18. Historical Excel imports

### Trigger

User imports old sales or purchase rows from Excel for history/reporting.

### User / role

`excel.import`. **Phase 5.**

### Preconditions

Same mapping/preview/confirm pipeline as Workflow 11.

### Steps

1. Map and validate.  
2. Store imported rows on the batch.  
3. **`posted_to_ledgers = false` until D48 is chosen.**  
4. Do **not** move stock or change balances on historical import unless D48 explicitly says so.  
5. Audit the import commit as data load, not as live invoices, until D48.

### Validation

Same classification. Unknown customer/product: **D36**.

### State changes

Imported history records. **Not** live posted invoices unless D48 = post.

### Inventory effect

**None** until D48.

### Financial / balance effect

**None** until D48.

### Audit effect

Import commit required.

### Failure / rollback

Uncommitted batch remains preview; no live ledgers.

### Open business decisions

| **D48** option | Stock | Customer/supplier balance | Use |
|---|---|---|---|
| A. Archive only (**safe default until you choose**) | No | No | Lookup / reports marked “imported history, not live” |
| B. Post as live documents | Yes (sales out / purchase in) | Yes | Dangerous if opening stock already includes those qty |
| C. Post finance only, no stock | No | Yes | If stock was already set by opening qty |
| D. Post stock only, no finance | Yes | No | Rare |

**Do not implement B/C/D without an explicit choice.** Mixing B with opening stock that already includes sold goods will **double-count**.

---

# Business Owner Decision Checklist

Answer only what Petro Trans still must decide. Unanswered items stay **TBD**. Do not treat this list as a request to invent complex workflows: where useful, the later implementation should be a **simple Settings option** for the owner, not a hardcoded process.

**D9, D6 (v1 scope), and D12 are closed** and removed from this list.

| ID | Topic | Options | Later as Settings? |
|---|---|---|---|
| **D1** | Invoice number format and when assigned | You specify prefix/padding; assign on draft vs on post | Yes (number series) |
| **D2** | Warehouse on sales invoice | One warehouse per invoice vs per line | Yes |
| **D3** | Discounts | None in v1 / line / document / both | Yes |
| **D4** | Sales orders in v1 UI | Hide / optional | Yes |
| **D5** | Credit **limit** enforcement | Ignore / warn / block post | Yes |
| **D7** | Negative stock | Block (Phase 1 default until you change it) / allow backorder | Yes |
| **D8** | Posted invoice edits | Forbid / permissioned edit / void+replace | Yes (keep simple) |
| **D10** | Unit price 0 | Allow / reject | Yes |
| **D11** | Who edits price *masters* | `owner_manager` only / also `pricing.edit_masters` | Phase 5 |
| **D13** | Currency | e.g. EGP only / other | Yes |
| **D14** | Unallocated prepayment / pay more than owed | Allow / block / warn | Yes; do not build a wallet unless you ask |
| **D15** | Void posted payment | No / who + rules | Yes; keep simple |
| **D16** | Return without original invoice | Allow with warning / reject | Yes |
| **D17** | Return warehouse if original unknown | User must choose / reject | Yes |
| **D18** | Return accounting | Credit / refund / exchange / stock-only | Yes |
| **D19** | Return time limit | None / N days | Yes |
| **D20** | Damaged returns | Treat as good stock / other (describe) | Yes if needed |
| **D21** | Receipt vs purchase invoice | Independent / must link | Yes |
| **D22** | Cost on purchase invoice | Required / optional | Yes |
| **D23** | Purchase returns in v1 | Yes / no | Yes |
| **D24** | Real shipment fields | List them, or none | Only if you list fields |
| **D25** | Supplier payments in v1 | Yes / no | Yes |
| **D26** | Supplier payment allocation | Account / bill matching | Yes — **not** decided by the customer credit-sale rule |
| **D27** | Warehouses in v1 | How many and names (setup; not invented here) | Setup data |
| **D28** | Min stock | Ignore / alert / block sales | Yes |
| **D29** | Customer tax fields | Later / none in v1 | Later |
| **D30** | Extra customer contacts | Single person only / multiple | Yes |
| **D31** | UOM / pack composition | Labels only / structured conversion | Yes |
| **D32** | SKU assignment | User typed / system generated | Yes |
| **D33** | Excel commit | Valid rows only / reject whole file | Yes |
| **D34** | Who may Excel-import | Phase 5 roles | Phase 5 |
| **D35** | Excel file types | `.xlsx` only / also `.xls` | Yes |
| **D36** | Unknown product/customer on non-master import | Error / create after confirm | Yes |
| **D37** | Costing / profit reports | None until you choose a method | Later; do not fake profit |
| **D38** | Export language | Always Arabic / follow UI | Yes |
| **D39** | Full permission matrix | Phase 5 | Phase 5 |
| **D40** | Admin price override later | Remains **no** unless you change it | Only if you later say yes |
| **D41** | Audit retention | Forever / other | Yes |
| **D42** | Who backup vs restore | Phase 5 | Phase 5 |
| **D43** | Scheduled backup | No / yes (frequency) | Yes |
| **D44** | Log permission denials | Yes / no | Yes |
| **D45** | Opening cut-over date | How you want it entered | Setup |
| **D46** | Duplicate opening stock | Block / add / reverse+replace | Yes |
| **D47** | Opening supplier balances | Yes / no | Yes |
| **D48** | Historical import | Archive only / post live / finance-only / stock-only | Yes |

**Already locked (not TBD):** product vs variant, ledger stock, `CUS-0001`, pricing resolution order, `owner_manager`-only price override, **override reason optional (D9)**, **credit sale (post ≠ paid)**, **invoice Total / Paid / Remaining / Payment Status**, **optional due date / payment term (D6 v1)**, **simple payment-to-invoice apply + partial payments (D12)**, empty payment methods, unique SKU if present, optional barcode, non-mandatory SO/PO, Excel mapping preview, no silent invalid import, no invented tax/ADNOC/costing, returns do not change balance until D18, logo filename TBD until `assets/` is inspected, no extra override confirmation screens, no installment/GL/dunning engines.

---

## What changed in this Phase 4 clarification

### Previous (D9) — still in force

1. **D9 closed:** price override reason is **optional**.  
2. Override UX: type the new price; optional note; background audit only.  
3. Small-business product principle: do not over-engineer.  
4. Remaining TBDs stay TBD unless the owner explicitly decides them.

### This revision (credit sales / payments)

5. **Credit sale is the normal sale.** Goods can leave the warehouse and the invoice can be posted **without** receiving money.  
6. **Two separate actions:** (1) create/post invoice = sale + stock out + outstanding; (2) record payment = money actually received.  
7. Posted invoice is **unpaid** until payments are recorded. Never mark paid just because it was posted.  
8. v1 money concepts locked: Invoice Total, Paid Amount, Remaining Amount, Due Date / Payment Term, Payment Status (`unpaid` / `partial` / `paid`).  
9. Partial payments locked. Remaining visible. Fully paid when remaining = 0.  
10. Due date / payment term: optional, owner sets when needed, **configurable not hardcoded**. No reminder/installment/dunning engine.  
11. **D6** closed for v1 (simple due date/term only). **D12** closed (simple apply payment to the invoice).  
12. **D5** (credit *limit* enforcement) is **not** decided — credit sales ≠ credit-limit policy.  
13. **D14** (prepayment / pay more than owed) still TBD. **D26** (supplier side) still TBD.  
14. Forbidden: GL, installment engines, credit-approval workflows, treating post as payment.  
15. Phase 5 still not started.

---

## Stop line

Phase 4 documentation is updated. No Phase 5 work, no code, no migrations.

Remaining TBDs are not decided. Finance-sensitive items still open: **D7, D8, D18, D33, D48** (and **D14**). **D12 is no longer open.**
