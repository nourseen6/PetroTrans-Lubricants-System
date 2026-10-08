# Petro Trans — Phase 5 Permissions, RBAC & Security Rules

**Status:** Simplified for v1 two-user launch (16 August 2026)  
**Scope:** Design/documentation only. No application code, migrations, UI, scaffolding, or Phase 6.

Phases 1–4 remain the source of truth. This phase does **not** decide remaining Phase 4 business TBDs (tax, currency, D8, D18, etc.).

---

## Product principle (LOCKED)

Small B2B. Professional and secure. Daily use simple and fast.

- Permission ≠ approval workflow.  
- A user may do an action or may not.  
- Audit is background. No extra confirmation screens for normal work.  
- Price override: type the new price, optional note, no mandatory reason.

---

## How to read this document

| Marker | Meaning |
|---|---|
| **LOCKED** | Owner rule or prior phase. |
| **Technical default** | Keeps the one v1 restriction (`pricing.override`) enforceable. Not a commercial policy. |
| **Future only** | Role exists for later employees. Not used at launch. |

---

## 0. v1 launch (LOCKED)

There are **two users**, each with their **own login**. No shared account.

| Person | Role | Difference |
|---|---|---|
| Father | `owner_manager` | Same day-to-day work **plus** `pricing.override` |
| You | `operator` | Same day-to-day work **without** `pricing.override` |

That is the **only** permission difference at launch.

Both can do the same operational work: sales, payments, products, customers, inventory, purchasing, returns, Excel import/export, reports, settings needed for daily work, backup.

Audit still records **which login** did each action. No approval steps.

RBAC stays `Users → Roles → Permissions` so future staff can be added later without redesign.

---

## 1. Permission catalog

Authorization checks **permission codes** on the backend, never `if (role == admin)`.

| Code | Protects |
|---|---|
| `sales.view` | View invoices, paid/remaining |
| `sales.create` | Create/edit **draft** invoices |
| `sales.post` | Post invoice (stock out + outstanding). Not an approval step |
| `sales.edit_posted` | Edit **posted** invoice — **exists but granted to nobody until D8** |
| `payments.view` | View payments |
| `payments.create` | Record payment against an invoice |
| `payments.void` | Void posted payment — **exists but granted to nobody until D15** |
| `customers.view` / `customers.edit` | Customers and balances |
| `products.view` / `products.edit` | Products and variants |
| `pricing.view` | See prices |
| `pricing.edit_masters` | Edit standard / type / customer prices (both users) |
| `pricing.override` | Change a price on a document line — **father only** |
| `inventory.view` / `inventory.receive` / `inventory.adjust` | Stock |
| `inventory.transfer` | Transfers — same grant as other stock ops; only useful if D27 has 2+ warehouses |
| `purchasing.view` / `purchasing.edit` | Suppliers, receiving, purchase invoices |
| `returns.create` | Return document + stock restore. Does **not** invent D18 finance |
| `excel.import` / `excel.export` | Excel |
| `reports.view` | Reports (no fake profit) |
| `settings.manage` | Payment methods, lookups, numbering, company, policies |
| `users.manage` | Create users, reset passwords, assign **non-owner** future roles |
| `roles.manage` | Change the permission matrix — **neither launch user** (see §4) |
| `audit.view` | Read audit log |
| `backup.create` / `backup.restore` | Backup / restore |

No approval permissions. No god-mode permission.

---

## 2. Roles

| Role | When used |
|---|---|
| `owner_manager` | Launch — father |
| `operator` | Launch — you. Same operational permissions, no override |
| `admin` | Future only. **No** automatic `pricing.override` |
| `sales` | Future only |
| `warehouse` | Future only |
| `accountant` | Future only |
| `branch_manager` | Not created |

v1 does **not** assign admin/sales/warehouse/accountant.

---

## 3. Initial users

| Rule | Kind |
|---|---|
| Two separate logins | **LOCKED** |
| Local username/password, hashed, no cloud IdP | **LOCKED** |
| Loopback API only | **LOCKED** |
| First-run creates **both** users (father = `owner_manager`, you = `operator`) | **Technical default** so launch needs no extra user-admin ritual |
| Passwords never plaintext | **Technical default** |

---

## 4. v1 permission matrix (simple)

**Operational pack** = everything in the catalog except:

- `pricing.override` (father only)  
- `roles.manage` (neither — see below)  
- `sales.edit_posted` / `payments.void` (neither until Phase 4 D8 / D15)

| Permission | Father `owner_manager` | You `operator` |
|---|---|---|
| Operational pack (sales, payments, products, customers, inventory, purchasing, returns, Excel import/export, reports, settings, users.manage, audit, backup/restore, pricing masters) | **Yes** | **Yes** |
| `pricing.override` | **Yes LOCKED** | **No LOCKED** |
| `roles.manage` | No | No |
| `sales.edit_posted` | No until D8 | No until D8 |
| `payments.void` | No until D15 | No until D15 |

`roles.manage` is denied to **both** so the override rule cannot be removed from the matrix by either user. That is a **technical default**, not a difference between you and your father.

`users.manage` is granted to **both** so either of you can add a future employee. **Technical default:** `owner_manager` cannot be assigned to a new user without an explicit later decision (keeps override exclusive). New staff get `operator`, `sales`, `warehouse`, `accountant`, or `admin`.

Future role matrices (unused at launch) stay as a simple starting point in the appendix of this file’s earlier draft intent:

- Future `sales`: invoices/customers/payments view+create+post; no stock adjust, no import, no override  
- Future `warehouse`: inventory receive/adjust/view; no payments, no override  
- Future `accountant`: payments, reports, audit view; no override  
- Future `admin`: operational pack + `roles.manage`; **still no** `pricing.override`

Those future rows are **not** launch configuration.

---

## 5. Owner / manager

- Daily work: same as you.  
- Exclusive: `pricing.override` (simple, fast, background audit).  
- Not god-mode: cannot rewrite the permission matrix (`roles.manage` = no).

---

## 6. Your permissions

**Same operational pack as your father.**  
**Only restriction:** no `pricing.override`.

This closes the previous P5-2 TBD.

---

## 7. Backend authorization

**LOCKED**

1. Authenticate.  
2. Load effective permissions from roles (deny-by-default).  
3. Check document state (draft vs posted, payment ≤ remaining, etc.).  
4. Allow or deny. Deny = no partial write. Arabic message.  
5. Sensitive success → `audit_log` in the same transaction.

Do not trust client user id, client role, or UI hiding.

---

## 8. UI

Arabic-first.

If the user is not the owner: **no price-override control**. Price follows resolution unless he is logged in as father.

Hide restore/import/etc. only if a **future** user lacks those permissions. At v1 both of you see the same operational UI except override.

Hiding is usability. Backend is the security control.

---

## 9. Audit (background)

Who, when, what. No audit form.

Must audit: price override, inventory adjustment, returns, payments, user/permission changes, Excel import commit, backup, restore, posted-document corrections if ever allowed, settings changes that affect methods/series/policies.

Override note remains **optional**.

D44 (log denials): still TBD. Not required for v1 simplicity.

---

## 10. Authentication / security

Login required. Local users. Loopback only. Hashed passwords. No cloud auth. No extra network exposure. No approval product.

---

## 11. Later employees

Create a login (`users.manage`). Assign `operator` or a future role. Do not share passwords. Do not grant `pricing.override` unless the owner explicitly changes that later.

---

## 12. Remaining decisions

v1 two-user split is **closed**: same operations; only father overrides prices.

Still **not** decided (Phase 4 — unchanged):

D5, D7, D8, D14, D15, D18, D27, D33, D48, currency, tax, costing, discounts, invoice numbering, warehouses, UOM, supplier payments, historical import posting, D44.

**P5-1 through P5-6** from the previous Phase 5 draft are **closed** by this simplification (both users, same ops, first-run creates both, both import/settings/backup).

Optional later (not required to proceed): whether a new user may ever receive `owner_manager`. **Technical default: no.**

---

## Stop line

Phase 5 is updated and simplified. No Phase 6. No implementation.
