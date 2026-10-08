using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PetroTrans.Application.Assistant;
using PetroTrans.Application.Catalog;
using PetroTrans.Application.Identity;
using PetroTrans.Application.Operations;
using PetroTrans.Application.Sales;
using PetroTrans.Domain.Identity;
using PetroTrans.Domain;
using PetroTrans.Domain.Assistant;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Infrastructure.Assistant;

public sealed class AssistantService : IAssistantService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly AppDbContext _db;
    private readonly ISalesInvoiceService _sales;
    private readonly ICustomerService _customers;
    private readonly IPaymentService _payments;
    private readonly IReportService _reports;
    private readonly IPricingService _pricing;
    private readonly ICatalogService _catalog;
    private readonly IInventoryService _inventory;
    private readonly IAuthService _auth;
    private readonly IAssistantProvider _provider;

    public AssistantService(
        AppDbContext db,
        ISalesInvoiceService sales,
        ICustomerService customers,
        IPaymentService payments,
        IReportService reports,
        IPricingService pricing,
        ICatalogService catalog,
        IInventoryService inventory,
        IAuthService auth,
        IAssistantProvider provider)
    {
        _db = db;
        _sales = sales;
        _customers = customers;
        _payments = payments;
        _reports = reports;
        _pricing = pricing;
        _catalog = catalog;
        _inventory = inventory;
        _auth = auth;
        _provider = provider;
    }

    public async Task<OperationResult<AssistantDraftPreview>> ParseAsync(
        AssistantParseRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var text = (request.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return OperationResult<AssistantDraftPreview>.Fail("اكتب الطلب أولاً.");
        }

        var intent = _provider.DetectIntent(text);
        var tool = AssistantToolCatalog.ForIntent(intent);
        if (tool?.RequiredPermission is { } permission
            && !await _auth.HasPermissionAsync(actorUserId, permission, cancellationToken))
        {
            return OperationResult<AssistantDraftPreview>.Fail("غير مسموح بهذه العملية.");
        }

        _db.AuditLogs.Add(Audits.Create(actorUserId, "assistant.tool", "assistant", null, null, new { intent, tool = tool?.Name, text }));
        await _db.SaveChangesAsync(cancellationToken);

        return intent switch
        {
            AssistantIntentTypes.RecordPayment => await ParsePaymentAsync(text, actorUserId, cancellationToken),
            AssistantIntentTypes.UpdateBasePrice or AssistantIntentTypes.UpdateCustomerPrice => await ParsePriceAsync(text, intent, actorUserId, cancellationToken),
            AssistantIntentTypes.CreateCustomer => await ParseCreateCustomerAsync(text, actorUserId, cancellationToken),
            AssistantIntentTypes.CustomerBalance => await ParseReadCustomerBalanceAsync(text, actorUserId, cancellationToken),
            AssistantIntentTypes.CustomerInvoices => await ParseReadCustomerInvoicesAsync(text, actorUserId, cancellationToken),
            AssistantIntentTypes.CustomerStatement or AssistantIntentTypes.PrintStatement => await ParseReadStatementAsync(text, intent, actorUserId, cancellationToken),
            AssistantIntentTypes.ListOwing => await ParseListOwingAsync(text, actorUserId, cancellationToken),
            AssistantIntentTypes.SalesReport or AssistantIntentTypes.PrintReport => await ParseSalesReportAsync(text, intent, actorUserId, cancellationToken),
            AssistantIntentTypes.InventoryLow => await ParseInventoryLowAsync(text, actorUserId, cancellationToken),
            AssistantIntentTypes.InventoryLookup => await ParseInventoryLookupAsync(text, actorUserId, cancellationToken),
            AssistantIntentTypes.GetPrices => await ParseGetPricesAsync(text, actorUserId, cancellationToken),
            AssistantIntentTypes.PrintInvoice => await ParsePrintInvoiceAsync(text, actorUserId, cancellationToken),
            AssistantIntentTypes.PrintPayment => await ParsePrintPaymentAsync(text, actorUserId, cancellationToken),
            AssistantIntentTypes.ArchiveCustomer => await ParseArchiveCustomerAsync(text, actorUserId, cancellationToken),
            AssistantIntentTypes.SalesInvoice => await ParseSalesInvoiceAsync(text, actorUserId, cancellationToken),
            _ => OperationResult<AssistantDraftPreview>.Fail("لم أتعرف على الطلب. جرّب فاتورة، تحصيل، مديونية، كشف حساب، مخزون، أسعار، أو تعديل سعر.")
        };
    }

    public async Task<AssistantDraftPreview?> GetDraftAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var draft = await _db.AssistantDrafts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return draft is null ? null : FromStored(draft);
    }

    public async Task<OperationResult<AssistantApproveResult>> ApproveAsync(
        Guid id,
        Guid actorUserId,
        bool canOverridePrice,
        CancellationToken cancellationToken = default)
    {
        var draft = await _db.AssistantDrafts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (draft is null)
        {
            return OperationResult<AssistantApproveResult>.Fail("المسودة غير موجودة.", 404);
        }

        if (draft.Status != AssistantDraftStatuses.Pending)
        {
            return OperationResult<AssistantApproveResult>.Fail("هذه المسودة تمت معالجتها مسبقاً.");
        }

        var tool = AssistantToolCatalog.ForIntent(draft.IntentType);
        if (tool?.RequiredPermission is { } permission
            && !await _auth.HasPermissionAsync(actorUserId, permission, cancellationToken))
        {
            return OperationResult<AssistantApproveResult>.Fail("غير مسموح بهذه العملية.");
        }

        var preview = FromStored(draft);
        if (preview is null || !preview.CanApprove)
        {
            return OperationResult<AssistantApproveResult>.Fail("لا يمكن التنفيذ قبل حل الغموض.");
        }

        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(draft.DraftJson) ? "{}" : draft.DraftJson);
        var root = doc.RootElement;

        OperationResult<AssistantApproveResult> result = draft.IntentType switch
        {
            AssistantIntentTypes.SalesInvoice => await ApproveSalesInvoiceAsync(draft, preview, actorUserId, canOverridePrice, cancellationToken),
            AssistantIntentTypes.RecordPayment => await ApprovePaymentAsync(draft, root, actorUserId, cancellationToken),
            AssistantIntentTypes.UpdateBasePrice => await ApproveBasePriceAsync(draft, root, actorUserId, cancellationToken),
            AssistantIntentTypes.UpdateCustomerPrice => await ApproveCustomerPriceAsync(draft, root, actorUserId, cancellationToken),
            AssistantIntentTypes.CreateCustomer => await ApproveCreateCustomerAsync(draft, root, actorUserId, cancellationToken),
            AssistantIntentTypes.ArchiveCustomer => await ApproveArchiveCustomerAsync(draft, root, actorUserId, cancellationToken),
            _ => OperationResult<AssistantApproveResult>.Fail("هذا النوع من الطلبات للقراءة فقط ولا يحتاج موافقة.")
        };

        return result;
    }

    public async Task<OperationResult<AssistantApproveResult>> RejectAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var draft = await _db.AssistantDrafts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (draft is null)
        {
            return OperationResult<AssistantApproveResult>.Fail("المسودة غير موجودة.", 404);
        }

        if (draft.Status != AssistantDraftStatuses.Pending)
        {
            return OperationResult<AssistantApproveResult>.Fail("هذه المسودة تمت معالجتها مسبقاً.");
        }

        draft.Status = AssistantDraftStatuses.Rejected;
        draft.UpdatedByUserId = actorUserId;
        _db.AuditLogs.Add(Audits.Create(actorUserId, "assistant.reject", "assistant_draft", draft.Id, null, new { rejected = true }));
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<AssistantApproveResult>.Ok(new AssistantApproveResult(draft.Id, draft.Status, null, "تم إلغاء المسودة."));
    }

    private async Task<OperationResult<AssistantDraftPreview>> ParseSalesInvoiceAsync(string text, Guid actorUserId, CancellationToken cancellationToken)
    {
        var customers = await _db.Customers.AsNoTracking().Where(x => x.IsActive).ToListAsync(cancellationToken);
        var variants = await _db.ProductVariants.AsNoTracking().Include(x => x.Product).Where(x => x.IsActive).ToListAsync(cancellationToken);
        var ambiguities = new List<AssistantAmbiguity>();
        var warnings = new List<string>();

        var customerMatches = MatchCustomers(text, customers);
        Guid? customerId = null;
        string? customerName = null;
        if (customerMatches.Count == 1)
        {
            customerId = customerMatches[0].Id;
            customerName = customerMatches[0].Name;
        }
        else if (customerMatches.Count > 1)
        {
            ambiguities.Add(new AssistantAmbiguity(
                "customer",
                "وجدت أكثر من عميل. اختر العميل المطلوب.",
                customerMatches.Select(x => new AssistantChoice(x.Id, $"{x.Name} — {x.Code}")).ToList()));
        }
        else
        {
            ambiguities.Add(new AssistantAmbiguity("customer", "لم أتعرف على العميل. اذكر اسم العميل بوضوح.", []));
        }

        var qty = ExtractQuantity(text) ?? 1m;
        var productMatches = MatchVariants(text, variants);
        var lines = new List<AssistantDraftLine>();
        if (productMatches.Count == 1)
        {
            var variant = productMatches[0];
            decimal? unitPrice = variant.StandardWholesalePrice;
            if (customerId is { } cid)
            {
                var special = await _db.CustomerVariantPrices.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.CustomerId == cid && x.VariantId == variant.Id, cancellationToken);
                if (special is not null)
                {
                    unitPrice = special.UnitPrice;
                }
            }

            lines.Add(new AssistantDraftLine(
                variant.Id,
                $"{variant.Product.Name} {variant.PackagingType} {variant.PackagingSize}",
                qty,
                unitPrice,
                $"{variant.PackagingType} {variant.PackagingSize}"));
        }
        else if (productMatches.Count > 1)
        {
            ambiguities.Add(new AssistantAmbiguity(
                "product",
                "وجدت أكثر من عبوة. اختر الصنف المطلوب.",
                productMatches.Select(x => new AssistantChoice(x.Id, $"{x.Product.Name} — {x.PackagingType} {x.PackagingSize}")).ToList()));
        }
        else
        {
            ambiguities.Add(new AssistantAmbiguity("product", "لم أتعرف على الصنف. اذكر اسم المنتج والعبوة.", []));
        }

        var manualTotal = ExtractMoney(text, "اجمالي", "إجمالي", "كليها", "خليها");
        string? manualReason = manualTotal is null ? null : "طلب المستخدم تعديل الإجمالي";
        if (manualTotal is not null)
        {
            warnings.Add("يوجد طلب لتعديل إجمالي الفاتورة يدوياً.");
        }

        var estimated = lines.Sum(x => (x.UnitPrice ?? 0m) * x.Quantity);
        if (manualTotal is not null)
        {
            estimated = manualTotal.Value;
        }

        var canApprove = ambiguities.Count == 0 && customerId is not null && lines.Count > 0 && lines.All(x => x.VariantId is not null);
        warnings.Add(canApprove
            ? "سيتم إنشاء مسودة فاتورة فقط. الترحيل والمخزون يحتاجان ترحيلاً يدوياً بعد المراجعة."
            : "المسودة تحتاج توضيحاً قبل التأكيد.");

        return await PersistDraftAsync(
            text,
            AssistantIntentTypes.SalesInvoice,
            actorUserId,
            new
            {
                intentType = AssistantIntentTypes.SalesInvoice,
                customerId,
                customerName,
                invoiceDate = DateTime.Today,
                lines,
                discountAmount = 0m,
                manualTotal,
                manualTotalReason = manualReason,
                estimatedTotal = estimated,
                warnings,
                ambiguities
            },
            customerId,
            customerName,
            lines,
            0m,
            manualTotal,
            manualReason,
            estimated,
            warnings,
            ambiguities,
            canApprove,
            null,
            null,
            null,
            "إنشاء مسودة فاتورة",
            cancellationToken);
    }

    private async Task<OperationResult<AssistantDraftPreview>> ParsePaymentAsync(string text, Guid actorUserId, CancellationToken cancellationToken)
    {
        var customers = await _db.Customers.AsNoTracking().Where(x => x.IsActive).ToListAsync(cancellationToken);
        var ambiguities = new List<AssistantAmbiguity>();
        var warnings = new List<string>();
        var customerMatches = MatchCustomers(text, customers);
        Guid? customerId = null;
        string? customerName = null;
        if (customerMatches.Count == 1)
        {
            customerId = customerMatches[0].Id;
            customerName = customerMatches[0].Name;
        }
        else if (customerMatches.Count > 1)
        {
            ambiguities.Add(new AssistantAmbiguity("customer", "وجدت أكثر من عميل. اختر العميل.", customerMatches.Select(x => new AssistantChoice(x.Id, $"{x.Name} — {x.Code}")).ToList()));
        }
        else
        {
            ambiguities.Add(new AssistantAmbiguity("customer", "لم أتعرف على العميل للتحصيل.", []));
        }

        var amount = ExtractMoney(text, "مبلغ", "تحصيل", "دفعة", "دفعت", "دفع", "سداد", "قبض", "ايداع", "إيداع") ?? ExtractTrailingMoney(text);
        if (amount is null or <= 0)
        {
            ambiguities.Add(new AssistantAmbiguity("amount", "اذكر مبلغ التحصيل بوضوح.", []));
        }

        var methods = await _db.PaymentMethods.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var cash = methods.FirstOrDefault(x => Normalize(x.Name).Contains("نقد", StringComparison.Ordinal)) ?? methods.FirstOrDefault();
        Guid? methodId = cash?.Id;
        if (methodId is null)
        {
            ambiguities.Add(new AssistantAmbiguity("paymentMethod", "أضف طريقة دفع من الإعدادات أولاً.", []));
        }

        var canApprove = ambiguities.Count == 0 && customerId is not null && amount is > 0 && methodId is not null;
        if (canApprove)
        {
            warnings.Add("بعد الموافقة سيتم تسجيل دفعة تحصيل على حساب العميل.");
        }

        var summary = canApprove
            ? $"مسودة تحصيل {amount:0.##} من {customerName}"
            : "مسودة تحصيل تحتاج توضيحاً";

        return await PersistDraftAsync(
            text,
            AssistantIntentTypes.RecordPayment,
            actorUserId,
            new
            {
                intentType = AssistantIntentTypes.RecordPayment,
                customerId,
                customerName,
                amount,
                paymentMethodId = methodId,
                paidOn = DateTime.Today,
                warnings,
                ambiguities
            },
            customerId,
            customerName,
            [],
            null,
            null,
            null,
            amount,
            warnings,
            ambiguities,
            canApprove,
            null,
            amount,
            methodId,
            summary,
            cancellationToken,
            summaryOverride: summary);
    }

    private async Task<OperationResult<AssistantDraftPreview>> ParsePriceAsync(string text, string intent, Guid actorUserId, CancellationToken cancellationToken)
    {
        var ambiguities = new List<AssistantAmbiguity>();
        var warnings = new List<string> { "تعديل السعر لا يغيّر فواتير سابقة." };
        var variants = await _db.ProductVariants.AsNoTracking().Include(x => x.Product).Where(x => x.IsActive).ToListAsync(cancellationToken);
        var productMatches = MatchVariants(text, variants);
        Guid? variantId = null;
        string? productLabel = null;
        if (productMatches.Count == 1)
        {
            variantId = productMatches[0].Id;
            productLabel = $"{productMatches[0].Product.Name} {productMatches[0].PackagingType} {productMatches[0].PackagingSize}";
        }
        else if (productMatches.Count > 1)
        {
            ambiguities.Add(new AssistantAmbiguity("product", "اختر العبوة لتعديل السعر.", productMatches.Select(x => new AssistantChoice(x.Id, $"{x.Product.Name} — {x.PackagingType} {x.PackagingSize}")).ToList()));
        }
        else
        {
            ambiguities.Add(new AssistantAmbiguity("product", "لم أتعرف على الصنف لتعديل السعر.", []));
        }

        var price = ExtractMoney(text, "سعر", "خلي", "اجعل", "خليه") ?? ExtractTrailingMoney(text);
        if (price is null or < 0)
        {
            ambiguities.Add(new AssistantAmbiguity("unitPrice", "اذكر السعر الجديد.", []));
        }

        Guid? customerId = null;
        string? customerName = null;
        var useCustomerPrice = intent == AssistantIntentTypes.UpdateCustomerPrice
            || text.Contains("عميل", StringComparison.Ordinal)
            || text.Contains("خاص", StringComparison.Ordinal);
        if (useCustomerPrice)
        {
            intent = AssistantIntentTypes.UpdateCustomerPrice;
            var customers = await _db.Customers.AsNoTracking().Where(x => x.IsActive).ToListAsync(cancellationToken);
            var matches = MatchCustomers(text, customers);
            if (matches.Count == 1)
            {
                customerId = matches[0].Id;
                customerName = matches[0].Name;
            }
            else if (matches.Count > 1)
            {
                ambiguities.Add(new AssistantAmbiguity("customer", "اختر العميل للسعر الخاص.", matches.Select(x => new AssistantChoice(x.Id, $"{x.Name} — {x.Code}")).ToList()));
            }
            else
            {
                ambiguities.Add(new AssistantAmbiguity("customer", "اذكر العميل للسعر الخاص.", []));
            }
        }
        else
        {
            intent = AssistantIntentTypes.UpdateBasePrice;
        }

        var canApprove = ambiguities.Count == 0 && variantId is not null && price is >= 0 && (!useCustomerPrice || customerId is not null);
        var summary = useCustomerPrice
            ? $"تعديل سعر خاص {productLabel} للعميل {customerName} إلى {price:0.##}"
            : $"تعديل السعر الأساسي {productLabel} إلى {price:0.##}";

        return await PersistDraftAsync(
            text,
            intent,
            actorUserId,
            new
            {
                intentType = intent,
                customerId,
                customerName,
                variantId,
                unitPrice = price,
                reason = "طلب المساعد الذكي",
                warnings,
                ambiguities
            },
            customerId,
            customerName,
            [],
            null,
            null,
            null,
            price,
            warnings,
            ambiguities,
            canApprove,
            variantId,
            price,
            null,
            summary,
            cancellationToken,
            summaryOverride: summary);
    }

    private async Task<OperationResult<AssistantDraftPreview>> ParseCreateCustomerAsync(string text, Guid actorUserId, CancellationToken cancellationToken)
    {
        var ambiguities = new List<AssistantAmbiguity>();
        var warnings = new List<string>();
        var name = ExtractCustomerNameForCreate(text);
        if (string.IsNullOrWhiteSpace(name))
        {
            ambiguities.Add(new AssistantAmbiguity("name", "اذكر اسم العميل الجديد.", []));
        }

        var canApprove = ambiguities.Count == 0;
        var summary = canApprove ? $"إنشاء عميل جديد: {name}" : "مسودة عميل جديد تحتاج اسماً";
        return await PersistDraftAsync(
            text,
            AssistantIntentTypes.CreateCustomer,
            actorUserId,
            new
            {
                intentType = AssistantIntentTypes.CreateCustomer,
                name,
                warnings,
                ambiguities
            },
            null,
            name,
            [],
            null,
            null,
            null,
            null,
            warnings,
            ambiguities,
            canApprove,
            null,
            null,
            null,
            summary,
            cancellationToken,
            summaryOverride: summary);
    }

    private async Task<OperationResult<AssistantDraftPreview>> ParseReadCustomerBalanceAsync(string text, Guid actorUserId, CancellationToken cancellationToken)
    {
        var customers = await _db.Customers.AsNoTracking().Where(x => x.IsActive).ToListAsync(cancellationToken);
        var matches = MatchCustomers(text, customers);
        var ambiguities = new List<AssistantAmbiguity>();
        var warnings = new List<string>();
        if (matches.Count != 1)
        {
            if (matches.Count > 1)
            {
                ambiguities.Add(new AssistantAmbiguity("customer", "اختر العميل لعرض المديونية.", matches.Select(x => new AssistantChoice(x.Id, $"{x.Name} — {x.Code}")).ToList()));
            }
            else
            {
                ambiguities.Add(new AssistantAmbiguity("customer", "لم أتعرف على العميل.", []));
            }

            return await PersistReadAsync(text, AssistantIntentTypes.CustomerBalance, actorUserId, "تعذر تحديد العميل للمديونية.", warnings, ambiguities, cancellationToken);
        }

        var statement = await _reports.CustomerStatementAsync(matches[0].Id, null, null, cancellationToken);
        var balance = statement?.ClosingBalance ?? 0m;
        var summary = $"مديونية {matches[0].Name}: {balance:0.##}";
        warnings.Add(summary);
        return await PersistReadAsync(text, AssistantIntentTypes.CustomerBalance, actorUserId, summary, warnings, ambiguities, cancellationToken, matches[0].Id, matches[0].Name, new { balance });
    }

    private async Task<OperationResult<AssistantDraftPreview>> ParseReadCustomerInvoicesAsync(string text, Guid actorUserId, CancellationToken cancellationToken)
    {
        var customers = await _db.Customers.AsNoTracking().Where(x => x.IsActive).ToListAsync(cancellationToken);
        var matches = MatchCustomers(text, customers);
        var ambiguities = new List<AssistantAmbiguity>();
        var warnings = new List<string>();
        if (matches.Count != 1)
        {
            ambiguities.Add(new AssistantAmbiguity("customer", "حدد العميل لعرض فواتيره.", matches.Select(x => new AssistantChoice(x.Id, $"{x.Name} — {x.Code}")).ToList()));
            return await PersistReadAsync(text, AssistantIntentTypes.CustomerInvoices, actorUserId, "تعذر تحديد العميل.", warnings, ambiguities, cancellationToken);
        }

        var invoices = await _sales.ListAsync(null, matches[0].Id, cancellationToken);
        var summary = invoices.Count == 0
            ? $"لا توجد فواتير لـ {matches[0].Name}"
            : $"فواتير {matches[0].Name}: {invoices.Count} — آخرها {invoices[0].Number ?? "مسودة"} بمبلغ {invoices[0].GoodsTotal:0.##}";
        warnings.Add(summary);
        foreach (var inv in invoices.Take(5))
        {
            warnings.Add($"{inv.Number ?? "مسودة"} | {inv.InvoiceDate:yyyy-MM-dd} | {inv.GoodsTotal:0.##} | {inv.Status}/{inv.PaymentStatus}");
        }

        return await PersistReadAsync(text, AssistantIntentTypes.CustomerInvoices, actorUserId, summary, warnings, ambiguities, cancellationToken, matches[0].Id, matches[0].Name);
    }

    private async Task<OperationResult<AssistantDraftPreview>> ParseReadStatementAsync(string text, string intent, Guid actorUserId, CancellationToken cancellationToken)
    {
        var customers = await _db.Customers.AsNoTracking().Where(x => x.IsActive).ToListAsync(cancellationToken);
        var matches = MatchCustomers(text, customers);
        var ambiguities = new List<AssistantAmbiguity>();
        var warnings = new List<string>();
        if (matches.Count != 1)
        {
            ambiguities.Add(new AssistantAmbiguity("customer", "حدد العميل لكشف الحساب.", matches.Select(x => new AssistantChoice(x.Id, $"{x.Name} — {x.Code}")).ToList()));
            return await PersistReadAsync(text, intent, actorUserId, "تعذر تحديد العميل لكشف الحساب.", warnings, ambiguities, cancellationToken);
        }

        var statement = await _reports.CustomerStatementAsync(matches[0].Id, null, null, cancellationToken);
        var summary = statement is null
            ? "لا يوجد كشف"
            : $"كشف حساب {statement.CustomerName}: رصيد ختامي {statement.ClosingBalance:0.##} (مدين {statement.TotalDebits:0.##} / دائن {statement.TotalCredits:0.##}) — {statement.Lines.Count} حركة";
        warnings.Add(summary);
        object? payload = intent == AssistantIntentTypes.PrintStatement
            ? new { path = $"/print/statement/{matches[0].Id}", closingBalance = statement?.ClosingBalance }
            : new { closingBalance = statement?.ClosingBalance, path = $"/customers/{matches[0].Id}" };

        return await PersistReadAsync(text, intent, actorUserId, summary, warnings, ambiguities, cancellationToken, matches[0].Id, matches[0].Name, payload);
    }

    private async Task<OperationResult<AssistantDraftPreview>> ParseListOwingAsync(string text, Guid actorUserId, CancellationToken cancellationToken)
    {
        var report = await _reports.CustomerBalancesAsync(new ReportFilters(null, null, null, null, null, null), cancellationToken);
        var owing = report.Rows
            .Select(r => new { Code = r[0], Name = r[1], Balance = decimal.TryParse(r[2], out var b) ? b : 0m })
            .Where(x => x.Balance > 0)
            .OrderByDescending(x => x.Balance)
            .Take(15)
            .ToList();
        var warnings = owing.Select(x => $"{x.Name} ({x.Code}): {x.Balance:0.##}").ToList();
        var summary = owing.Count == 0 ? "لا يوجد عملاء عليهم مديونية حالياً." : $"عدد المدينين: {owing.Count}";
        warnings.Insert(0, summary);
        return await PersistReadAsync(text, AssistantIntentTypes.ListOwing, actorUserId, summary, warnings, [], cancellationToken);
    }

    private async Task<OperationResult<AssistantDraftPreview>> ParseSalesReportAsync(string text, string intent, Guid actorUserId, CancellationToken cancellationToken)
    {
        var range = AssistantIntentDetector.DetectDateRange(text, DateTime.Today);
        var report = await _reports.SalesAsync(new ReportFilters(range.From, range.To, null, null, null, null), cancellationToken);
        var total = report.Rows.Sum(r => decimal.TryParse(r[3], out var v) ? v : 0m);
        var period = range.From is { } from && range.To is { } to
            ? $" من {from:yyyy-MM-dd} إلى {to:yyyy-MM-dd}"
            : "";
        var summary = $"تقرير المبيعات{period}: {report.Rows.Count} فاتورة بإجمالي {total:0.##}";
        var warnings = new List<string> { summary };
        foreach (var row in report.Rows.Take(8))
        {
            warnings.Add($"{row[0]} | {row[1]} | {row[2]} | {row[3]}");
        }

        var fromValue = range.From?.ToString("yyyy-MM-dd");
        var toValue = range.To?.ToString("yyyy-MM-dd");
        var path = "/print/report?type=sales"
            + (fromValue is null ? "" : $"&from={fromValue}")
            + (toValue is null ? "" : $"&to={toValue}");
        return await PersistReadAsync(text, intent, actorUserId, summary, warnings, [], cancellationToken, actionPayload: new { path });
    }

    private async Task<OperationResult<AssistantDraftPreview>> ParseInventoryLowAsync(string text, Guid actorUserId, CancellationToken cancellationToken)
    {
        var stock = await _inventory.ListOnHandAsync(null, cancellationToken);
        var low = stock.Where(x => x.BelowMin).Take(20).ToList();
        var warnings = low.Select(x => $"{x.ProductName} {x.PackagingType} {x.PackagingSize}: المتاح {x.OnHand:0.##} / الحد {x.MinStock:0.##}").ToList();
        var summary = low.Count == 0 ? "لا يوجد أصناف تحت الحد الأدنى." : $"أصناف تحت الحد: {low.Count}";
        warnings.Insert(0, summary);
        return await PersistReadAsync(text, AssistantIntentTypes.InventoryLow, actorUserId, summary, warnings, [], cancellationToken);
    }

    private async Task<OperationResult<AssistantDraftPreview>> ParsePrintInvoiceAsync(string text, Guid actorUserId, CancellationToken cancellationToken)
    {
        var invoices = await _sales.ListAsync(null, null, cancellationToken);
        var numberMatch = Regex.Match(text, @"\d{3,}");
        var hit = numberMatch.Success
            ? invoices.FirstOrDefault(x => x.Number != null && x.Number.Contains(numberMatch.Value, StringComparison.Ordinal))
            : invoices.FirstOrDefault();
        var warnings = new List<string>();
        var summary = hit is null
            ? "لم أجد فاتورة للطباعة."
            : $"فاتورة جاهزة للطباعة: {hit.Number ?? hit.Id.ToString()} — {hit.CustomerName} — {hit.GoodsTotal:0.##}";
        warnings.Add(summary);
        if (hit is not null)
        {
            warnings.Add("افتح شاشة الطباعة لمراجعة الفاتورة ثم اختر الطابعة.");
        }

        return await PersistReadAsync(text, AssistantIntentTypes.PrintInvoice, actorUserId, summary, warnings, [], cancellationToken, actionPayload: hit is null ? null : new { path = $"/print/invoice/{hit.Id}", invoiceId = hit.Id, number = hit.Number });
    }

    private async Task<OperationResult<AssistantDraftPreview>> ParsePrintPaymentAsync(string text, Guid actorUserId, CancellationToken cancellationToken)
    {
        var payments = await _payments.ListAsync(null, cancellationToken);
        var hit = payments.FirstOrDefault();
        var summary = hit is null ? "لا يوجد تحصيل للطباعة." : $"إيصال تحصيل {hit.CustomerName} بمبلغ {hit.Amount:0.##}";
        return await PersistReadAsync(text, AssistantIntentTypes.PrintPayment, actorUserId, summary, [summary], [], cancellationToken, actionPayload: hit is null ? null : new { path = $"/print/payment/{hit.Id}", paymentId = hit.Id });
    }

    private async Task<OperationResult<AssistantDraftPreview>> ParseInventoryLookupAsync(string text, Guid actorUserId, CancellationToken cancellationToken)
    {
        var stock = await _inventory.ListOnHandAsync(null, cancellationToken);
        var n = AssistantIntentDetector.Normalize(text);
        var hits = stock
            .Where(x => AssistantIntentDetector.Normalize($"{x.ProductName} {x.PackagingType} {x.PackagingSize}").Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Any(token => token.Length > 2 && n.Contains(token, StringComparison.Ordinal)))
            .ToList();
        if (hits.Count == 0)
        {
            hits = stock.Take(20).ToList();
        }

        var warnings = hits.Take(20).Select(x => $"{x.ProductName} {x.PackagingType} {x.PackagingSize}: المتاح {x.OnHand:0.##}").ToList();
        var summary = hits.Count == 0 ? "لا توجد حركة مخزون مطابقة." : $"المخزون: {hits.Count} صنف";
        warnings.Insert(0, summary);
        return await PersistReadAsync(text, AssistantIntentTypes.InventoryLookup, actorUserId, summary, warnings, [], cancellationToken);
    }

    private async Task<OperationResult<AssistantDraftPreview>> ParseGetPricesAsync(string text, Guid actorUserId, CancellationToken cancellationToken)
    {
        var variants = await _db.ProductVariants.AsNoTracking().Include(x => x.Product).Where(x => x.IsActive).ToListAsync(cancellationToken);
        var matches = MatchVariants(text, variants);
        var customers = await _db.Customers.AsNoTracking().Where(x => x.IsActive).ToListAsync(cancellationToken);
        var customerMatches = MatchCustomers(text, customers);
        var warnings = new List<string>();
        var ambiguities = new List<AssistantAmbiguity>();
        if (matches.Count == 0)
        {
            var matrix = await _pricing.ListMatrixAsync(null, cancellationToken);
            warnings.AddRange(matrix.Take(12).Select(x => $"{x.ProductName} {x.PackagingType} {x.PackagingSize}: {x.BasePrice:0.##}"));
            return await PersistReadAsync(text, AssistantIntentTypes.GetPrices, actorUserId, "أسعار العملاء الحالية", warnings, ambiguities, cancellationToken);
        }

        if (matches.Count > 8)
        {
            ambiguities.Add(new AssistantAmbiguity("variant", "حدد العبوة المطلوبة.", matches.Take(12).Select(x => new AssistantChoice(x.Id, $"{x.Product.Name} — {x.PackagingType} {x.PackagingSize}")).ToList()));
        }

        foreach (var variant in matches.Take(8))
        {
            warnings.Add($"{variant.Product.Name} {variant.PackagingType} {variant.PackagingSize}: سعر العملاء {variant.StandardWholesalePrice:0.##} / سعر الشركة {variant.StandardPurchasePrice:0.##}");
        }

        if (customerMatches.Count == 1)
        {
            var specials = await _pricing.ListCustomerPricesAsync(customerMatches[0].Id, cancellationToken);
            foreach (var special in specials.Where(x => matches.Any(v => v.Id == x.VariantId)))
            {
                warnings.Add($"سعر {customerMatches[0].Name}: {special.ProductName} {special.PackagingSize} = {special.UnitPrice:0.##}");
            }
        }

        return await PersistReadAsync(text, AssistantIntentTypes.GetPrices, actorUserId, warnings.FirstOrDefault() ?? "الأسعار", warnings, ambiguities, cancellationToken, customerMatches.Count == 1 ? customerMatches[0].Id : null, customerMatches.Count == 1 ? customerMatches[0].Name : null);
    }

    private async Task<OperationResult<AssistantDraftPreview>> ParseArchiveCustomerAsync(string text, Guid actorUserId, CancellationToken cancellationToken)
    {
        var customers = await _db.Customers.AsNoTracking().Where(x => x.IsActive).ToListAsync(cancellationToken);
        var matches = MatchCustomers(text, customers);
        var ambiguities = new List<AssistantAmbiguity>();
        if (matches.Count != 1)
        {
            ambiguities.Add(new AssistantAmbiguity("customer", "حدد العميل للأرشفة. العملاء ذوو الفواتير لا يُحذفون، تتم أرشفتهم فقط.", matches.Select(x => new AssistantChoice(x.Id, $"{x.Name} — {x.Code}")).ToList()));
            return await PersistDraftAsync(
                text,
                AssistantIntentTypes.ArchiveCustomer,
                actorUserId,
                new { intentType = AssistantIntentTypes.ArchiveCustomer, ambiguities, warnings = new[] { "لن يتم حذف السجل المالي." } },
                null,
                null,
                [],
                null,
                null,
                null,
                null,
                ["لن يتم حذف السجل المالي."],
                ambiguities,
                canApprove: false,
                null,
                null,
                null,
                "أرشفة عميل",
                cancellationToken,
                summaryOverride: "حدد العميل أولاً.");
        }

        var customer = matches[0];
        return await PersistDraftAsync(
            text,
            AssistantIntentTypes.ArchiveCustomer,
            actorUserId,
            new { intentType = AssistantIntentTypes.ArchiveCustomer, customerId = customer.Id, customerName = customer.Name },
            customer.Id,
            customer.Name,
            [],
            null,
            null,
            null,
            null,
            ["سيتم أرشفة العميل فقط. الفواتير والتحصيلات وكشف الحساب تبقى كما هي."],
            [],
            true,
            null,
            null,
            null,
            "أرشفة عميل",
            cancellationToken,
            summaryOverride: $"أرشفة العميل {customer.Name}؟ السجل المالي لن يُحذف.",
            actionPayload: new { customerId = customer.Id });
    }

    private async Task<OperationResult<AssistantApproveResult>> ApproveArchiveCustomerAsync(AssistantDraft draft, JsonElement root, Guid actorUserId, CancellationToken cancellationToken)
    {
        var customerId = ReadGuid(root, "customerId");
        if (customerId is null)
        {
            return OperationResult<AssistantApproveResult>.Fail("حدد العميل أولاً.");
        }

        var archived = await _customers.ArchiveAsync(customerId.Value, actorUserId, cancellationToken);
        if (!archived.Succeeded || archived.Value is null)
        {
            return OperationResult<AssistantApproveResult>.Fail(archived.Error ?? "تعذر أرشفة العميل.");
        }

        draft.Status = AssistantDraftStatuses.Approved;
        draft.ResultJson = JsonSerializer.Serialize(new { customerId }, JsonOptions);
        draft.UpdatedByUserId = actorUserId;
        _db.AuditLogs.Add(Audits.Create(actorUserId, "assistant.approve", "assistant_draft", draft.Id, null, new { customerId, action = "archive" }));
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<AssistantApproveResult>.Ok(new AssistantApproveResult(draft.Id, draft.Status, customerId, "تمت أرشفة العميل. السجل المالي محفوظ."));
    }

    private async Task<OperationResult<AssistantApproveResult>> ApproveSalesInvoiceAsync(
        AssistantDraft draft,
        AssistantDraftPreview preview,
        Guid actorUserId,
        bool canOverridePrice,
        CancellationToken cancellationToken)
    {
        if (preview.CustomerId is null || preview.Lines.Count == 0)
        {
            return OperationResult<AssistantApproveResult>.Fail("لا يمكن التنفيذ قبل حل الغموض.");
        }

        var warehouse = await _db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive, cancellationToken);
        var save = new SaveInvoiceRequest(
            preview.CustomerId.Value,
            warehouse?.Id,
            preview.InvoiceDate ?? DateTime.Today,
            null,
            $"من المساعد الذكي: {draft.UserText}",
            preview.DiscountAmount ?? 0m,
            preview.ManualTotal,
            preview.ManualTotalReason,
            preview.Lines.Select(x => new SaveInvoiceLineRequest(x.VariantId!.Value, x.Quantity, x.UnitPrice, null)).ToList());

        var created = await _sales.CreateAsync(save, actorUserId, canOverridePrice, cancellationToken);
        if (!created.Succeeded || created.Value is null)
        {
            draft.Error = created.Error;
            draft.UpdatedByUserId = actorUserId;
            await _db.SaveChangesAsync(cancellationToken);
            return OperationResult<AssistantApproveResult>.Fail(created.Error ?? "تعذر إنشاء المسودة.");
        }

        draft.Status = AssistantDraftStatuses.Approved;
        draft.ResultJson = JsonSerializer.Serialize(new { invoiceId = created.Value.Id, number = created.Value.Number }, JsonOptions);
        draft.UpdatedByUserId = actorUserId;
        _db.AuditLogs.Add(Audits.Create(actorUserId, "assistant.approve", "assistant_draft", draft.Id, null, new { invoiceId = created.Value.Id }));
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<AssistantApproveResult>.Ok(new AssistantApproveResult(draft.Id, draft.Status, created.Value.Id, "تم إنشاء مسودة الفاتورة. راجعها ثم رحّلها يدوياً."));
    }

    private async Task<OperationResult<AssistantApproveResult>> ApprovePaymentAsync(AssistantDraft draft, JsonElement root, Guid actorUserId, CancellationToken cancellationToken)
    {
        var customerId = ReadGuid(root, "customerId");
        var amount = ReadDecimal(root, "amount");
        var methodId = ReadGuid(root, "paymentMethodId");
        if (customerId is null || amount is null or <= 0 || methodId is null)
        {
            return OperationResult<AssistantApproveResult>.Fail("بيانات التحصيل غير مكتملة.");
        }

        var created = await _payments.CreateAsync(
            new SavePaymentRequest(null, customerId, methodId.Value, amount.Value, DateTime.Today, null, $"من المساعد: {draft.UserText}"),
            actorUserId,
            cancellationToken);
        if (!created.Succeeded || created.Value is null)
        {
            draft.Error = created.Error;
            draft.UpdatedByUserId = actorUserId;
            await _db.SaveChangesAsync(cancellationToken);
            return OperationResult<AssistantApproveResult>.Fail(created.Error ?? "تعذر تسجيل الدفعة.");
        }

        draft.Status = AssistantDraftStatuses.Approved;
        draft.ResultJson = JsonSerializer.Serialize(new { paymentId = created.Value.Id }, JsonOptions);
        draft.UpdatedByUserId = actorUserId;
        _db.AuditLogs.Add(Audits.Create(actorUserId, "assistant.approve", "assistant_draft", draft.Id, null, new { paymentId = created.Value.Id }));
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<AssistantApproveResult>.Ok(new AssistantApproveResult(draft.Id, draft.Status, created.Value.Id, "تم تسجيل التحصيل."));
    }

    private async Task<OperationResult<AssistantApproveResult>> ApproveBasePriceAsync(AssistantDraft draft, JsonElement root, Guid actorUserId, CancellationToken cancellationToken)
    {
        var variantId = ReadGuid(root, "variantId");
        var unitPrice = ReadDecimal(root, "unitPrice");
        if (variantId is null || unitPrice is null)
        {
            return OperationResult<AssistantApproveResult>.Fail("بيانات السعر غير مكتملة.");
        }

        var updated = await _pricing.UpdateBasePriceAsync(variantId.Value, unitPrice.Value, "من المساعد الذكي", actorUserId, cancellationToken);
        if (!updated.Succeeded)
        {
            return OperationResult<AssistantApproveResult>.Fail(updated.Error ?? "تعذر تعديل السعر.");
        }

        draft.Status = AssistantDraftStatuses.Approved;
        draft.ResultJson = JsonSerializer.Serialize(new { variantId }, JsonOptions);
        draft.UpdatedByUserId = actorUserId;
        _db.AuditLogs.Add(Audits.Create(actorUserId, "assistant.approve", "assistant_draft", draft.Id, null, new { variantId, unitPrice }));
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<AssistantApproveResult>.Ok(new AssistantApproveResult(draft.Id, draft.Status, variantId, "تم تحديث السعر الأساسي."));
    }

    private async Task<OperationResult<AssistantApproveResult>> ApproveCustomerPriceAsync(AssistantDraft draft, JsonElement root, Guid actorUserId, CancellationToken cancellationToken)
    {
        var customerId = ReadGuid(root, "customerId");
        var variantId = ReadGuid(root, "variantId");
        var unitPrice = ReadDecimal(root, "unitPrice");
        if (customerId is null || variantId is null || unitPrice is null)
        {
            return OperationResult<AssistantApproveResult>.Fail("بيانات السعر الخاص غير مكتملة.");
        }

        var updated = await _pricing.SetCustomerPriceAsync(customerId.Value, variantId.Value, unitPrice.Value, "من المساعد الذكي", actorUserId, cancellationToken);
        if (!updated.Succeeded || updated.Value is null)
        {
            return OperationResult<AssistantApproveResult>.Fail(updated.Error ?? "تعذر تعديل السعر الخاص.");
        }

        draft.Status = AssistantDraftStatuses.Approved;
        draft.ResultJson = JsonSerializer.Serialize(new { priceId = updated.Value.Id }, JsonOptions);
        draft.UpdatedByUserId = actorUserId;
        _db.AuditLogs.Add(Audits.Create(actorUserId, "assistant.approve", "assistant_draft", draft.Id, null, new { priceId = updated.Value.Id }));
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<AssistantApproveResult>.Ok(new AssistantApproveResult(draft.Id, draft.Status, updated.Value.Id, "تم حفظ السعر الخاص للعميل."));
    }

    private async Task<OperationResult<AssistantApproveResult>> ApproveCreateCustomerAsync(AssistantDraft draft, JsonElement root, Guid actorUserId, CancellationToken cancellationToken)
    {
        var name = root.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
        if (string.IsNullOrWhiteSpace(name))
        {
            return OperationResult<AssistantApproveResult>.Fail("اسم العميل مطلوب.");
        }

        var created = await _customers.CreateAsync(
            new SaveCustomerRequest(name!, null, null, null, null, null),
            actorUserId,
            cancellationToken);
        if (!created.Succeeded || created.Value is null)
        {
            return OperationResult<AssistantApproveResult>.Fail(created.Error ?? "تعذر إنشاء العميل.");
        }

        draft.Status = AssistantDraftStatuses.Approved;
        draft.ResultJson = JsonSerializer.Serialize(new { customerId = created.Value.Id }, JsonOptions);
        draft.UpdatedByUserId = actorUserId;
        _db.AuditLogs.Add(Audits.Create(actorUserId, "assistant.approve", "assistant_draft", draft.Id, null, new { customerId = created.Value.Id }));
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<AssistantApproveResult>.Ok(new AssistantApproveResult(draft.Id, draft.Status, created.Value.Id, "تم إنشاء العميل."));
    }

    private async Task<OperationResult<AssistantDraftPreview>> PersistReadAsync(
        string text,
        string intent,
        Guid actorUserId,
        string summary,
        IReadOnlyList<string> warnings,
        IReadOnlyList<AssistantAmbiguity> ambiguities,
        CancellationToken cancellationToken,
        Guid? customerId = null,
        string? customerName = null,
        object? actionPayload = null)
    {
        return await PersistDraftAsync(
            text,
            intent,
            actorUserId,
            new
            {
                intentType = intent,
                customerId,
                customerName,
                warnings,
                ambiguities,
                actionPayload,
                readOnly = true
            },
            customerId,
            customerName,
            [],
            null,
            null,
            null,
            null,
            warnings,
            ambiguities,
            canApprove: false,
            null,
            null,
            null,
            summary,
            cancellationToken,
            summaryOverride: summary,
            actionPayload: actionPayload);
    }

    private async Task<OperationResult<AssistantDraftPreview>> PersistDraftAsync(
        string text,
        string intent,
        Guid actorUserId,
        object payload,
        Guid? customerId,
        string? customerName,
        IReadOnlyList<AssistantDraftLine> lines,
        decimal? discount,
        decimal? manualTotal,
        string? manualReason,
        decimal? estimated,
        IReadOnlyList<string> warnings,
        IReadOnlyList<AssistantAmbiguity> ambiguities,
        bool canApprove,
        Guid? variantId,
        decimal? amount,
        Guid? paymentMethodId,
        string? actionLabel,
        CancellationToken cancellationToken,
        string? summaryOverride = null,
        object? actionPayload = null)
    {
        var draft = new AssistantDraft
        {
            Id = UuidV7.New(),
            IntentType = intent,
            Status = AssistantDraftStatuses.Pending,
            UserText = text,
            DraftJson = JsonSerializer.Serialize(payload, JsonOptions),
            CreatedByUserId = actorUserId,
            UpdatedByUserId = actorUserId
        };
        _db.AssistantDrafts.Add(draft);
        _db.AuditLogs.Add(Audits.Create(actorUserId, "assistant.draft", "assistant_draft", draft.Id, null, new { intent, canApprove }));
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<AssistantDraftPreview>.Ok(ToPreview(
            draft,
            intent,
            customerId,
            customerName,
            lines,
            discount ?? 0m,
            manualTotal,
            manualReason,
            estimated,
            warnings,
            ambiguities,
            canApprove,
            variantId,
            amount,
            paymentMethodId,
            actionLabel,
            actionPayload,
            summaryOverride));
    }

    private static List<Domain.Parties.Customer> MatchCustomers(string text, List<Domain.Parties.Customer> customers)
    {
        var normalized = Normalize(text);
        return customers
            .Where(x =>
            {
                var name = Normalize(x.Name);
                if (normalized.Contains(name, StringComparison.Ordinal)) return true;
                var tokens = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(token => token.Length >= 3).ToList();
                return tokens.Count > 0 && tokens.Any(token => normalized.Contains(token, StringComparison.Ordinal));
            })
            .OrderByDescending(x => Normalize(x.Name).Length)
            .ToList();
    }

    private static List<Domain.Catalog.ProductVariant> MatchVariants(string text, List<Domain.Catalog.ProductVariant> variants)
    {
        var normalized = Normalize(text);
        var hits = variants
            .Where(x =>
            {
                var name = Normalize(x.Product.Name);
                var label = Normalize($"{x.Product.Name} {x.Product.Brand} {x.Product.Specification} {x.PackagingType} {x.PackagingSize}");
                if (normalized.Contains(name, StringComparison.Ordinal)) return true;
                var nameTokens = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(token => token.Length >= 4).ToList();
                if (nameTokens.Any(token => normalized.Contains(token, StringComparison.Ordinal))) return true;
                return label.Split(' ', StringSplitOptions.RemoveEmptyEntries).Count(token => token.Length > 1 && normalized.Contains(token, StringComparison.Ordinal)) >= 2;
            })
            .OrderByDescending(x => Normalize(x.Product.Name).Length)
            .ToList();

        if (hits.Count > 1 && (normalized.Contains("1ل", StringComparison.Ordinal) || normalized.Contains("لتر", StringComparison.Ordinal)))
        {
            var filtered = hits.Where(x => Normalize(x.PackagingSize).Contains("1", StringComparison.Ordinal) || Normalize(x.PackagingSize).Contains("لتر", StringComparison.Ordinal)).ToList();
            if (filtered.Count > 0) hits = filtered;
        }

        if (hits.Count > 1 && (normalized.Contains("4ل", StringComparison.Ordinal) || normalized.Contains("4 ل", StringComparison.Ordinal)))
        {
            var filtered = hits.Where(x => Normalize(x.PackagingSize).Contains("4", StringComparison.Ordinal)).ToList();
            if (filtered.Count > 0) hits = filtered;
        }

        return hits;
    }

    private static decimal? ExtractQuantity(string text)
    {
        var match = Regex.Match(text, @"(\d+(?:[.,]\d+)?)\s*(كرتون|كراتين|عبوة|عبوات|قطعة|قطع)?", RegexOptions.CultureInvariant);
        if (!match.Success) return null;
        return decimal.TryParse(match.Groups[1].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static decimal? ExtractMoney(string text, params string[] markers)
    {
        foreach (var marker in markers)
        {
            var idx = text.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) continue;
            var slice = text[idx..];
            var match = Regex.Match(slice, @"(\d{2,7}(?:[.,]\d+)?)");
            if (match.Success && decimal.TryParse(match.Groups[1].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static decimal? ExtractTrailingMoney(string text)
    {
        var match = Regex.Match(text.Trim(), @"(\d{2,7}(?:[.,]\d+)?)\s*$");
        return match.Success && decimal.TryParse(match.Groups[1].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static string? ExtractCustomerNameForCreate(string text)
    {
        var patterns = new[]
        {
            @"عميل جديد\s+(.+)$",
            @"أضف عميل\s+(.+)$",
            @"اضف عميل\s+(.+)$",
            @"أنشئ عميل\s+(.+)$",
            @"انشئ عميل\s+(.+)$"
        };
        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text.Trim(), pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var name = match.Groups[1].Value.Trim().Trim('"', '\'', '«', '»');
                if (!string.IsNullOrWhiteSpace(name)) return name;
            }
        }

        return null;
    }

    private static string Normalize(string value)
    {
        return value
            .Replace("أ", "ا", StringComparison.Ordinal)
            .Replace("إ", "ا", StringComparison.Ordinal)
            .Replace("آ", "ا", StringComparison.Ordinal)
            .Replace("ة", "ه", StringComparison.Ordinal)
            .Replace("ى", "ي", StringComparison.Ordinal)
            .ToLowerInvariant();
    }

    private static Guid? ReadGuid(JsonElement root, string name)
        => root.TryGetProperty(name, out var el) && el.ValueKind != JsonValueKind.Null && Guid.TryParse(el.ToString().Trim('"'), out var g) ? g : null;

    private static decimal? ReadDecimal(JsonElement root, string name)
        => root.TryGetProperty(name, out var el) && el.ValueKind != JsonValueKind.Null && el.TryGetDecimal(out var d) ? d : null;

    private static AssistantDraftPreview FromStored(AssistantDraft draft)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(draft.DraftJson) ? "{}" : draft.DraftJson);
        var root = doc.RootElement;
        Guid? customerId = ReadGuid(root, "customerId");
        var customerName = root.TryGetProperty("customerName", out var cn) && cn.ValueKind == JsonValueKind.String ? cn.GetString() : null;
        var lines = new List<AssistantDraftLine>();
        if (root.TryGetProperty("lines", out var linesEl) && linesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var line in linesEl.EnumerateArray())
            {
                Guid? variantId = line.TryGetProperty("variantId", out var vid) && vid.ValueKind != JsonValueKind.Null && Guid.TryParse(vid.ToString(), out var vg) ? vg : null;
                var productQuery = line.TryGetProperty("productQuery", out var pq) ? pq.GetString() ?? "" : "";
                var quantity = line.TryGetProperty("quantity", out var q) ? q.GetDecimal() : 0m;
                decimal? unitPrice = line.TryGetProperty("unitPrice", out var up) && up.ValueKind != JsonValueKind.Null ? up.GetDecimal() : null;
                var packaging = line.TryGetProperty("packagingHint", out var ph) ? ph.GetString() : null;
                lines.Add(new AssistantDraftLine(variantId, productQuery, quantity, unitPrice, packaging));
            }
        }

        var ambiguities = new List<AssistantAmbiguity>();
        if (root.TryGetProperty("ambiguities", out var amb) && amb.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in amb.EnumerateArray())
            {
                var field = item.GetProperty("field").GetString() ?? "";
                var message = item.GetProperty("message").GetString() ?? "";
                var choices = new List<AssistantChoice>();
                if (item.TryGetProperty("choices", out var ch) && ch.ValueKind == JsonValueKind.Array)
                {
                    foreach (var choice in ch.EnumerateArray())
                    {
                        if (Guid.TryParse(choice.GetProperty("id").ToString(), out var choiceId))
                        {
                            choices.Add(new AssistantChoice(choiceId, choice.GetProperty("label").GetString() ?? ""));
                        }
                    }
                }

                ambiguities.Add(new AssistantAmbiguity(field, message, choices));
            }
        }

        var warnings = new List<string>();
        if (root.TryGetProperty("warnings", out var warns) && warns.ValueKind == JsonValueKind.Array)
        {
            warnings.AddRange(warns.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0));
        }

        decimal? discount = root.TryGetProperty("discountAmount", out var d) && d.ValueKind != JsonValueKind.Null ? d.GetDecimal() : 0m;
        decimal? manualTotal = root.TryGetProperty("manualTotal", out var mt) && mt.ValueKind != JsonValueKind.Null ? mt.GetDecimal() : null;
        var manualReason = root.TryGetProperty("manualTotalReason", out var mr) && mr.ValueKind == JsonValueKind.String ? mr.GetString() : null;
        decimal? estimated = root.TryGetProperty("estimatedTotal", out var et) && et.ValueKind != JsonValueKind.Null ? et.GetDecimal() : null;
        decimal? amount = root.TryGetProperty("amount", out var am) && am.ValueKind != JsonValueKind.Null ? am.GetDecimal()
            : root.TryGetProperty("unitPrice", out var up2) && up2.ValueKind != JsonValueKind.Null ? up2.GetDecimal() : null;
        Guid? variantIdField = ReadGuid(root, "variantId");
        Guid? methodId = ReadGuid(root, "paymentMethodId");
        var readOnly = root.TryGetProperty("readOnly", out var ro) && ro.ValueKind == JsonValueKind.True;
        object? actionPayload = root.TryGetProperty("actionPayload", out var ap) ? ap.Clone() : null;

        var canApprove = !readOnly && draft.Status == AssistantDraftStatuses.Pending && ambiguities.Count == 0;
        if (draft.IntentType == AssistantIntentTypes.SalesInvoice)
        {
            canApprove = canApprove && customerId is not null && lines.Count > 0 && lines.All(x => x.VariantId is not null);
        }
        else if (draft.IntentType == AssistantIntentTypes.RecordPayment)
        {
            canApprove = canApprove && customerId is not null && amount is > 0 && methodId is not null;
        }
        else if (draft.IntentType is AssistantIntentTypes.UpdateBasePrice or AssistantIntentTypes.UpdateCustomerPrice)
        {
            canApprove = canApprove && variantIdField is not null && amount is >= 0
                && (draft.IntentType != AssistantIntentTypes.UpdateCustomerPrice || customerId is not null);
        }
        else if (draft.IntentType == AssistantIntentTypes.CreateCustomer)
        {
            var nameOk = root.TryGetProperty("name", out var nm) && nm.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(nm.GetString());
            canApprove = canApprove && nameOk;
        }
        else if (draft.IntentType == AssistantIntentTypes.ArchiveCustomer)
        {
            canApprove = canApprove && customerId is not null;
        }
        else
        {
            canApprove = false;
        }

        var summary = warnings.FirstOrDefault()
            ?? (draft.Status == AssistantDraftStatuses.Pending ? "مسودة جاهزة للمراجعة"
                : draft.Status == AssistantDraftStatuses.Approved ? "تم التنفيذ بعد الموافقة" : "تم الإلغاء");

        return ToPreview(draft, draft.IntentType, customerId, customerName, lines, discount ?? 0m, manualTotal, manualReason, estimated, warnings, ambiguities, canApprove, variantIdField, amount, methodId, null, actionPayload, summary);
    }

    private static AssistantDraftPreview ToPreview(
        AssistantDraft draft,
        string intent,
        Guid? customerId,
        string? customerName,
        IReadOnlyList<AssistantDraftLine> lines,
        decimal discount,
        decimal? manualTotal,
        string? manualReason,
        decimal? estimated,
        IReadOnlyList<string> warnings,
        IReadOnlyList<AssistantAmbiguity> ambiguities,
        bool canApprove,
        Guid? variantId,
        decimal? amount,
        Guid? paymentMethodId,
        string? actionLabel,
        object? actionPayload,
        string? summaryOverride = null)
    {
        var summary = summaryOverride
            ?? (draft.Status == AssistantDraftStatuses.Pending
                ? "مسودة جاهزة للمراجعة"
                : draft.Status == AssistantDraftStatuses.Approved
                    ? "تم التنفيذ بعد الموافقة"
                    : "تم الإلغاء");
        return new AssistantDraftPreview(
            draft.Id,
            intent,
            draft.Status,
            summary,
            customerId,
            customerName,
            DateTime.Today,
            lines,
            discount,
            manualTotal,
            manualReason,
            estimated,
            warnings,
            ambiguities,
            canApprove && draft.Status == AssistantDraftStatuses.Pending,
            variantId,
            amount,
            paymentMethodId,
            actionLabel,
            actionPayload);
    }
}
