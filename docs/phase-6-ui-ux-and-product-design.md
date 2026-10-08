# Petro Trans — Phase 6 UI/UX & Product Design

**Status:** Proposed — awaiting approval  
**Date:** 16 August 2026  
**Scope:** Design/documentation only. No application code, migrations, scaffolding, or Phase 7.

Locked decisions from Phases 1–5 are preserved. Remaining business TBDs stay TBD. The UI may expose **Settings** for later configuration; it must not hardcode those policies.

---

## Product principle (LOCKED)

Two real users. Desktop Windows app. Arabic-first, native RTL. Fast daily work. Professional, not a generic card dashboard. No approval workflows. Audit in the background.

**v1 permission UI:** both users see the same operational screens. The **only** UI difference: price override controls appear only for `owner_manager` (father).

---

## Official assets (LOCKED)

| Rule | Detail |
|---|---|
| Source | Project `assets/` folder only |
| Logo | Official Petro Trans logo. **Do not invent, guess, redesign, or download.** Exact filename **TBD** until `assets/` is inspected |
| Product images | Only files already in `assets/`. Reference by path after inspection |
| Missing logo | Show company name text “Petro Trans” in the shell until the path is set in Settings |
| Public website | Not part of this UI |

---

## TBD in the UI (do not decide)

Show as optional fields, hidden sections, or Settings toggles **off/unset**. Do not build extra workflows for:

D1 invoice number format, D2 warehouse header vs line, D3 discounts, D4 sales orders, D5 credit limit, D7 negative stock, D8 posted edits, D10 price 0, D14 prepayment, D15 void payment, D16–D20 returns finance/details, D21–D26 purchasing extras, D27 warehouses, D28 min-stock action, D31 UOM, D33 Excel commit mode, D35 xls/xlsx, D37 profit, D48 historical import posting, tax, currency, costing.

**Locked money fields on invoice:** الإجمالي، المدفوع، المتبقي، تاريخ الاستحقاق / أجل الدفع، حالة الدفع.

---

# 1. Application shell / navigation

Windows desktop window (WebView2 host). Launch from Petro Trans icon. No URL bar.

```
┌─────────────────────────────────────────────────────────────┐
│ [شعار] Petro Trans    المستخدم ▼    عربية|EN    ◐    ─ □ ✕ │  شريط علوي
├──────────┬──────────────────────────────────────────────────┤
│ الرئيسية │                                                  │
│ المبيعات │              منطقة العمل                         │
│ العملاء  │                                                  │
│ الأصناف  │                                                  │
│ المخزون  │                                                  │
│ المشتريات│                                                  │
│ التقارير │                                                  │
│──────────│                                                  │
│ الإعدادات│                                                  │
└──────────┴──────────────────────────────────────────────────┘
```

**RTL:** navigation **on the right**. Top bar mirrors: window controls stay OS-native (Windows LTR chrome is acceptable); in-app content is RTL.

| Shell element | Behavior |
|---|---|
| Logo | From `assets/` when path known; else text |
| User menu | Name, role label (مالك / مستخدم), logout, lock |
| Language | Default العربية. EN optional (Phase 1) |
| Theme | Light / dark |
| Nav | Single level. No mega-menus |
| Status bar (bottom, optional) | User, date, unsaved draft hint |

**Keyboard:** Ctrl+N new invoice (when on sales), Esc close dialog, Ctrl+S save draft, F5 refresh list. Do not require a mouse-only flow.

**Permissions:** both users see all operational nav items. No “admin-only” shell at v1 except override on invoice lines.

---

# 2. Information architecture

| Nav | Primary screens |
|---|---|
| الرئيسية | Operational home |
| المبيعات | Invoice list, invoice editor, payments list, returns list |
| العملاء | Customer list, customer file |
| الأصناف | Product list, product + variants |
| المخزون | On-hand, movements, receive, adjust |
| المشتريات | Suppliers, receipts, purchase invoices |
| التقارير | Report picker + preview/export |
| الإعدادات | Company, lookups, numbering (TBD format), users, backup, Excel templates |

**Not in v1 nav:** sales orders, purchase orders, public website, profitability, tax engine, installment plans. If D4 later enables orders, add a tab under المبيعات — do not show now.

**Payments:** not a top-level item. Entry points: invoice (“تسجيل دفعة”), customer file, and مبيعات → المدفوعات.

**Returns:** مبيعات → المرتجعات.

---

# 3. Screen inventory

| ID | Screen | Nav |
|---|---|---|
| S0 | Login | Pre-shell |
| S0b | First-run setup | Pre-shell |
| S1 | الرئيسية | الرئيسية |
| S2 | قائمة الفواتير | المبيعات |
| S3 | فاتورة بيع (مسودة/مرحّلة) | المبيعات |
| S4 | تسجيل دفعة (حوار) | From S3 / S5 / S6 |
| S5 | قائمة المدفوعات | المبيعات |
| S6 | ملف العميل | العملاء |
| S7 | قائمة العملاء | العملاء |
| S8 | قائمة الأصناف | الأصناف |
| S9 | صنف + عبوات | الأصناف |
| S10 | المخزون الحالي | المخزون |
| S11 | حركة المخزون | المخزون |
| S12 | إذن إضافة / استلام | المخزون / المشتريات |
| S13 | تسوية مخزون | المخزون |
| S14 | الموردون | المشتريات |
| S15 | فواتير المشتريات | المشتريات |
| S16 | المرتجعات | المبيعات |
| S17 | مرتجع (إنشاء) | المبيعات |
| S18 | التقارير | التقارير |
| S19 | معالج استيراد Excel | From lists / Settings |
| S20 | تصدير Excel (حفظ) | From lists / reports |
| S21 | الإعدادات — الشركة | الإعدادات |
| S22 | الإعدادات — القوائم (أنواع عملاء، طرق دفع، أجل) | الإعدادات |
| S23 | المستخدمون | الإعدادات |
| S24 | النسخ الاحتياطي | الإعدادات |
| S25 | طباعة فاتورة (معاينة) | From S3 |
| S26 | سجل التدقيق (عرض) | الإعدادات |

---

# 4–12. Screens (purpose, actions, fields, tables, nav, states, validation, permissions)

Shared list pattern for S2, S5, S7, S8, S10, S14, S15, S16:

- Search box, simple filters, dense table, row click = open, primary button top-start (RTL: right).  
- Export Excel on the toolbar.  
- Empty: one sentence + primary action.  
- Loading: table skeleton.  
- Error: Arabic banner + retry.

---

## S0 — تسجيل الدخول

**Purpose:** Identify the user. No shared account.

**Fields:** اسم المستخدم، كلمة المرور.

**Actions:** دخول.

**Validation:** required; wrong credentials → Arabic error. No user enumeration extras.

**States:** loading on submit; first-run redirects to S0b if no users.

**Permissions:** none (unauthenticated).

---

## S0b — الإعداد لأول مرة

**Purpose:** Create the two launch users.

**Fields:** حساب المالك (الأب): اسم العرض، اسم المستخدم، كلمة المرور، تأكيد. حسابك: نفس الحقول.

**Actions:** إنشاء ودخول.

**Validation:** passwords hashed later (implementation); match confirm; usernames unique.

**Do not** invent default passwords on screen copy.

---

## S1 — الرئيسية

**Purpose:** Today’s work, not a decorative dashboard.

**Content (simple rows/tables, few numbers):**

- فواتير غير مدفوعة / جزئية (count + open list)  
- متأخرة عن تاريخ الاستحقاق **if due date set** (display only; no dunning engine)  
- أصناف تحت الحد الأدنى (display only; action TBD D28 — show list, do not block here)  
- آخر الفواتير، آخر الدفعات  

**Actions:** فاتورة جديدة، تسجيل دفعة، عميل جديد.

**No:** charts spam, fake profit, KPI card walls.

**Permissions:** both users.

---

## S2 — قائمة الفواتير

**Purpose:** Find and open sales invoices.

**Toolbar:** فاتورة جديدة، تصدير Excel، استيراد (opens S19 purpose=historical_sales but **does not post live** until D48 — if historical import is offered, label it “أرشيف” / TBD hide until D48). **Safer v1:** no historical import button here until D48. Only export.

**Filters:** عميل، حالة المستند (مسودة/مرحّلة)، حالة الدفع (غير مدفوعة/جزئية/مدفوعة)، من–إلى تاريخ. Credit limit filter: **no** (D5 TBD).

**Table columns:**

| Column | Notes |
|---|---|
| رقم الفاتورة | Empty/TBD format until D1; may show internal short id until series configured |
| التاريخ | |
| العميل | |
| الإجمالي | |
| المدفوع | |
| المتبقي | |
| الاستحقاق | blank if unset |
| حالة الدفع | unpaid / partial / paid |
| حالة المستند | draft / posted |

**Main actions:** open, new.

**Permissions:** both. `sales.create` / `sales.post` both have.

**Validation:** none on list.

---

## S3 — فاتورة بيع (core daily screen)

**Purpose:** Create, save draft, post. Credit sale is normal.

### Layout (RTL, one page, no wizard)

1. Header: عميل (search/select), التاريخ، تاريخ الاستحقاق (optional), أجل الدفع (optional dropdown from Settings — empty until owner adds terms).  
2. Lines table.  
3. Footer totals.  
4. Notes.  
5. Actions: حفظ مسودة، ترحيل، طباعة (posted), تسجيل دفعة (posted, remaining > 0), إغلاق.

**Sales order field:** hidden (D4).

**Warehouse:** one control on the invoice until D2 says per-line. If no warehouse exists, posting blocked with Arabic “أضف مخزناً من الإعدادات” — do not invent a name.

### Line table

| Column | Behavior |
|---|---|
| الصنف / العبوة | Search variant (show product name + packaging) |
| الكمية | |
| سعر الوحدة | Filled by resolution; **editable only for father** |
| ملاحظة السعر | Optional, only if price changed by owner; **not required** |
| الإجمالي | qty × price |
| حذف | |

Discount columns: **hidden until D3**.

Tax: **hidden**.

### Price field UX (LOCKED)

- Operator: read-only resolved price. If no price, empty + “لا يوجد سعر — لا يمكن الترحيل”.  
- Owner: editable in place. Change = override. No modal, no approval, no required reason. Optional note column/field. Background audit.

### Footer (LOCKED)

- الإجمالي  
- المدفوع (0 until payments)  
- المتبقي  
- حالة الدفع  

Document status badge: مسودة / مرحّلة. **Never** show “مدفوعة” just because posted.

### Validation

- Customer, ≥1 line, qty > 0, price present to **post**.  
- Oversell: until D7, block post with remaining stock shown.  
- Payment method not involved here.

### States

Draft: editable. Posted: header/lines locked (until D8). Payments still allowed if remaining > 0. Print enabled when posted (and optionally draft watermark — **technical default:** print posted only to avoid unofficial drafts; owner can still see on-screen).

### Permissions

Both: create, save, post. Override UI: father only.

### Navigation

Back to S2. Customer name → S6.

---

## S4 — حوار تسجيل دفعة

**Purpose:** Record money received. Separate from posting.

**Keep tiny.** No allocation engine.

**Fields:** العميل (if not from invoice), الفاتورة (posted, remaining > 0), المبلغ (default = remaining, user may enter less), التاريخ، طريقة الدفع (required; if Settings empty → “أضف طريقة دفع من الإعدادات”), المرجع، ملاحظات.

**Actions:** حفظ، إلغاء. No extra confirm.

**Validation:** amount > 0 and ≤ remaining (D14 unallocated extra: not offered). Method required.

**Success:** invoice paid/remaining/status update; stay on invoice or close dialog.

**Permissions:** both.

**Inventory:** none.

---

## S5 — قائمة المدفوعات

**Columns:** التاريخ، العميل، رقم الفاتورة، المبلغ، الطريقة، المستخدم.

**Actions:** open invoice, export. No void button until D15.

---

## S6 — ملف العميل

**Purpose:** Master + outstanding + history.

**Fields:** الكود (CUS-0001, read-only after create), الاسم، النوع (optional lookup, not seeded), مسؤول الاتصال، هاتف، واتساب، العنوان. Credit limit: **optional field hidden or unlabeled storage TBD D5 — do not show enforcement UI**. Tax IDs: hidden (D29).

**Blocks:**

- Outstanding: sum of remaining posted invoices  
- Table of invoices (same columns as S2 filtered)  
- Table of payments  
- Button: فاتورة جديدة، تسجيل دفعة  

**WhatsApp:** show number; optional `wa.me` link later — stored number is enough.

**Permissions:** both.

---

## S7 — قائمة العملاء

**Columns:** الكود، الاسم، النوع، الهاتف، المتبقي على الحساب.

**Actions:** جديد، تصدير، استيراد (S19 purpose=customers).

---

## S8 — قائمة الأصناف

**Grouped by product, variants as child rows** (or flatten with product name + عبوة). Prefer **one table**: الصنف، العبوة، SKU، السعر الأساسي، المخزون، حد أدنى، نشط.

**Actions:** جديد، تصدير، استيراد.

Do not list 3X4 and 12X1 as unrelated products.

---

## S9 — صنف + عبوات

**Product fields:** الاسم، العلامة، الفئة، المواصفة/API، نشط.

**Variants table:** نوع العبوة، الحجم، SKU (optional, unique if present), باركود (optional, no unique), سعر الجملة الأساسي (optional), حد أدنى، نشط. Image: pick from `assets/` files already present — no upload-from-internet.

**Actions:** حفظ.

**UOM conversion UI:** hidden (D31). Packaging is labels.

---

## S10 — المخزون الحالي

**Columns:** الصنف، العبوة، المخزن، الكمية، حد أدنى (highlight if below — display only).

**Actions:** حركة، تسوية، استلام، تصدير.

---

## S11 — حركة المخزون

**Filters:** variant, warehouse, type, dates.

**Columns:** الوقت، النوع، صنف، عبوة، مخزن، كمية، اتجاه، المستند، المستخدم، ملاحظات.

Read-only ledger. No inline edit of movements.

---

## S12 — استلام بضاعة

**Fields:** المورد، التاريخ، مرجع اختياري، مخزن، سطور (عبوة، كمية، تكلفة اختيارية).

**Actions:** ترحيل (stock in). Purchase invoice **not** required (D21 independent).

**ADNOC extra fields:** none. Optional notes / generic reference only.

---

## S13 — تسوية مخزون

**Fields:** عبوة، مخزن، إضافة أو خصم، الكمية، **سبب (مطلوب)**، ملاحظات.

**Actions:** ترحيل. No approval. Background audit.

**Oversell/negative:** follow D7 hold (block out below zero until decided).

---

## S14 — الموردون / S15 — فواتير المشتريات

Simple list + form: اسم المورد، فواتير، مدفوعات مورد **hidden if D25 no** — v1 show purchase invoices and receipts; supplier payment button **hidden until D25**.

Cost required: field optional until D22; do not force a number.

---

## S16 / S17 — المرتجعات

**List + create form:** فاتورة أصلية (preferred), سطور وكميات ≤ المتبقي للمرتجع، مخزن.

**Copy:** “يُعاد المخزون عند الترحيل. أثر الرصيد على العميل غير مفعّل حتى يُحدد المالك سياسة المرتجعات.” (D18)

No credit/refund/exchange radio that invents policy. If a type dropdown exists, it stays **disabled/unset**.

**Permissions:** both. Finance posting: none.

---

## S18 — التقارير

**Picker (v1):** مبيعات، مشتريات، مخزون، حركة مخزون، أرصدة عملاء، فواتير غير مسددة، مرتجعات، أداء أصناف (qty/amount sold — not margin).

**No** profitability until D37.

**Preview** table on screen + تصدير Excel + طباعة.

Filters: dates, customer, product. Language of export: Arabic (D38 TBD — default Arabic).

---

## S19 — معالج استيراد Excel (LOCKED flow)

Single wizard, reused. **Not** a rigid-template-only screen.

**Steps (one dialog, stepper, RTL):**

1. اختيار الملف (Windows dialog)  
2. تحليل: أوراق، أعمدة  
3. ربط الأعمدة: proposed map, user can change every field  
4. معاينة: VALID / WARNING / ERROR counts + row errors  
5. تأكيد → تنفيذ الصفوف المسموحة حسب D33 **unset:** UI offers “حفظ الصحيح فقط” vs “الكل أو لا شيء” as **disabled until D33**, or only show preview until Settings D33 is set. **Technical default for UI:** primary button “اعتماد الصفوف الصحيحة” is **not** assumed as policy — if D33 unset, require a Settings choice first **or** show both buttons labelled clearly as waiting for owner policy. Simplest without deciding D33: wizard **stops at preview** with “لا يمكن الاعتماد حتى تحديد سياسة الاعتماد في الإعدادات” **too blocking**. Better: **do not decide D33**; show owner both actions as **future Settings**. For design: Preview always; Commit uses a Settings value if configured; if not configured, father/you pick **this time only** in a single extra line: “اعتماد الصحيح وتجاهل الخطأ” vs “إلغاء إذا وُجد خطأ” — that would decide D33 per import which is actually OK as **not a permanent business rule**. Phase 4 said D33 is Settings later. **UI:** commit dropdown in Settings (unset); on import if unset, default control is **preview only + “حفظ الإعداد ثم الاعتماد”** is heavy.

**Practical UI without locking D33:** On step 5, two explicit choices the user picks **for this file** is still a business decision each time. The Phase 4 instruction was: configurable later, don’t invent. **Design:** Settings has D33 unset; import step 5 shows a note “سياسة الاعتماد تُحدد من الإعدادات” and until set, **Commit is disabled**. That might block go-live import.

Go-live **needs** import. So: Settings item “عند وجود صفوف خاطئة: اعتماد الصحيح / رفض الملف” exists **unset**. First import: Settings can be set in the wizard’s last step as a **one-line choice saved to Settings** — that’s configuration, not inventing a hidden default.

**Wizard step 5:** “كيف تريد الاعتماد؟” saves to Settings if empty, then commits. Owner configures by doing. Not a hardcoded rule in this doc as THE policy.

**Purpose selector:** منتجات، عبوات، عملاء، مخزون أول المدة، أرصدة أول المدة، موردون. Historical sales/purchases: available but labelled لن يُرحّل للمخزون أو الأرصدة (D48 archive-only hold).

**Permissions:** both.

**Do not** store original absolute path as source of truth.

---

## S20 — تصدير Excel

Windows save dialog. Professional workbook: title, date, filters, Arabic RTL headers, freeze, totals, logo **if path set**.

---

## S21 — إعدادات الشركة

**Fields:** اسم الشركة (Petro Trans), مسار الشعار (browse `assets/` only — do not guess filename), اللغة الافتراضية عربية, المظهر.

Currency: field **optional/TBD D13** — placeholder “يُحدد لاحقاً” or Settings text, not a fake ISO list presented as decided.

---

## S22 — قوائم الإعدادات

Editable lists, **empty at start**:

- أنواع العملاء  
- طرق الدفع (**required before first payment**)  
- آجال الدفع (optional terms for invoice dropdown)  
- المخازن (must create at least one before stock/sales post — **user names it**, we do not)  
- سياسة الاعتماد Excel (D33) — unset  
- سياسات أخرى TBD as simple dropdowns **unset** (negative stock D7, etc.) — do not pre-select Block as a labelled “company policy”, but post still blocks oversell per Phase 4 hold. **Do not show a misleading Settings value of Block as if the owner chose it.** Show “غير محدد — النظام يمنع البيع بأكثر من المتاح حتى يُقرر المالك”.

---

## S23 — المستخدمون

**v1:** two rows. Add user for the future.

**Fields:** اسم، مستخدم، دور (`owner_manager` not assignable to new users — technical default), نشط، إعادة كلمة المرور.

**Cannot** edit permission matrix (no `roles.manage`).

**Cannot** grant override except by being father on `owner_manager`.

---

## S24 — النسخ الاحتياطي

**Actions:** نسخ احتياطي (folder picker), استعادة (file picker + **one** Arabic confirm: “سيتم استبدال البيانات الحالية”). Not a multi-step approval ritual; restore is destructive so **one** confirm is justified (not an override-style extra screen). Audit background.

Both users (Phase 5).

---

## S25 — طباعة الفاتورة

Preview RTL Arabic. Header: logo if set, Petro Trans, invoice number, date, customer.

Lines: صنف، عبوة، كمية، سعر، إجمالي.

Footer: الإجمالي، المدفوع، المتبقي، حالة الدفع، استحقاق إن وُجد.

**Not** “paid in full” unless remaining = 0.

Windows print dialog. PDF via print-to-PDF.

Legal footer / tax ID: omitted until TBD.

---

## S26 — سجل التدقيق

Read-only table: وقت، مستخدم، إجراء، كيان. Filter by type. No manual “write audit” form.

---

# 13. Arabic RTL behavior (LOCKED)

- `dir=rtl`, `lang=ar` default.  
- Nav on the right.  
- Tables: first column on the right.  
- Forms: labels above or start-side.  
- Numbers: stay 0–9 unless D digit style is decided later.  
- Dates: display locale ar-EG; storage UTC.  
- English switch: `dir=ltr`, nav left; not the default.  
- Message dialogs Arabic-first.

---

# 14. Loading, empty, error, validation, success

| State | Pattern |
|---|---|
| Loading | Skeleton table / button spinner. No blank white. |
| Empty list | Short Arabic sentence + one button (e.g. “لا توجد فواتير — إنشاء فاتورة”) |
| Validation | Inline under field, Arabic |
| Permission deny | Arabic toast/banner: ليس لديك صلاحية لتعديل السعر |
| Post failure | Invoice stays draft; message explains (stock, missing price, missing warehouse) |
| Success | Quiet: list updates or status badge. No fireworks. Payment: totals refresh |
| Network/local API down | “تعذر الاتصال بالتطبيق المحلي — أعد فتح البرنامج” |
| Import | VALID/WARNING/ERROR counts; errors with row numbers |

---

# 15. Design system / components

**Tone:** dense desktop business app. High-contrast tables. Ample but not sparse padding. No gradient, no card grid dashboard, no illustration empty-states.

| Token | Guidance |
|---|---|
| Color | Neutrals + **one accent taken from the official logo after inspection**. Until then: neutral gray/slate UI, not an invented brand palette |
| Type | Arabic-capable UI font (e.g. system Segoe UI for Latin + a bundled Arabic UI font at implementation). Do not invent a logo font |
| Table | Frozen header, row hover, compact density, RTL |
| Button | Primary (one per screen), secondary, destructive (restore, adjust out) |
| Dialog | Narrow for payment; wide for invoice not in a dialog — invoice is a full page |
| Inputs | Text, number, date, combobox search (customers, variants) |
| Badge | مسودة، مرحّلة، غير مدفوعة، جزئية، مدفوعة |
| Dark/light | Both, follow user toggle |

Components: AppShell, DataTable, FilterBar, PageHeader, FormGrid, LineItemsGrid, TotalsBar, ConfirmDialog (restore/adjust only), Wizard (Excel only), PrintPreview.

---

# 16. Window / desktop behavior

- Resizable, maximizable, remember size/position (**technical default**).  
- Minimum width ~1280px comfortable; below that stack filters above table (usable tablet).  
- Desktop-first. Touch: larger hit targets if window is narrow.  
- File dialogs: native Windows.  
- Print: native.  
- Close with unsaved **draft** invoice: one Arabic “حفظ المسودة؟ نعم / لا / إلغاء”. Posted invoice: no prompt.  
- Single instance preferred (**technical default**) to protect SQLite.

---

# 17. Use of official assets

| Place | Asset |
|---|---|
| Title bar, login, print, Excel header | Logo from `assets/` when path set |
| Product form / invoice line optional thumbnail | Product images already in `assets/` |
| App icon (installer/desktop) | Logo-derived **from official file**, not a generated mark |

If `assets/` is not yet inspectable, UI copy uses text “Petro Trans” only.

---

# 18. Accessibility and usability

- Keyboard to all primary actions.  
- Visible focus.  
- Contrast in light and dark.  
- Do not rely on color alone for unpaid vs paid (use text badges).  
- Arabic error text, not codes.  
- Variant search by name, SKU, packaging.  
- Customer search by name, code, phone.  
- Two-user simplicity: no mode switcher, no “module licenses”.  
- Father: price cell editable. You: price cell locked.

---

## Primary daily path (happy path)

1. Login.  
2. المبيعات → فاتورة جديدة.  
3. Select customer, add 2 variants (e.g. two packaging lines), quantities.  
4. Prices resolve; father may type a new price.  
5. Optional due date.  
6. ترحيل → stock down, remaining = total, status غير مدفوعة.  
7. Later: تسجيل دفعة → partial or full → totals update.  
8. طباعة if needed.

Target: few clicks, one screen for the invoice, one small dialog for payment.

---

## Stop line

Phase 6 design documentation is complete. No Phase 7. No implementation. No invented TBD policies. Logo filename remains TBD until `assets/` is inspected.
