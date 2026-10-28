using Microsoft.EntityFrameworkCore;
using PetroTrans.Application.Catalog;
using PetroTrans.Application.Operations;
using PetroTrans.Domain;
using PetroTrans.Domain.Finance;
using PetroTrans.Domain.Inventory;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Infrastructure.Operations;

public sealed class TreasuryService : ITreasuryService
{
    private readonly AppDbContext _db;

    public TreasuryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<TreasuryBookDto> GetBookAsync(DateTime? from = null, DateTime? to = null, string? category = null, CancellationToken cancellationToken = default, string? account = null)
    {
        var book = TreasuryAccounts.Normalize(account) ?? TreasuryAccounts.Cash;
        var rows = await _db.TreasuryEntries.AsNoTracking()
            .Where(x => x.Account == book)
            .OrderBy(x => x.OccurredOn)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var running = 0m;
        var mapped = new List<TreasuryEntryDto>(rows.Count);
        foreach (var row in rows)
        {
            running += Signed(row);
            mapped.Add(Map(row, running));
        }

        IEnumerable<TreasuryEntryDto> visible = mapped;
        if (from is { } fromDate)
        {
            visible = visible.Where(x => x.OccurredOn.Date >= fromDate.Date);
        }

        if (to is { } toDate)
        {
            visible = visible.Where(x => x.OccurredOn.Date <= toDate.Date);
        }

        var filter = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        if (filter is not null)
        {
            visible = visible.Where(x => TreasuryCategories.MatchesFilter(x.Category, filter));
        }

        var entries = visible.ToList();
        if (filter is not null)
        {
            var totalIn = entries.Where(x => x.Direction == TreasuryDirections.In).Sum(x => x.Amount);
            var totalOut = entries.Where(x => x.Direction == TreasuryDirections.Out).Sum(x => x.Amount);
            var categoryTotal = entries.Sum(x => x.Amount);
            return new TreasuryBookDto(from, to, 0m, totalIn, totalOut, categoryTotal, entries, filter, categoryTotal, book);
        }

        var opening = 0m;
        if (entries.Count == 0)
        {
            var lastBefore = mapped.LastOrDefault(x => from is null || x.OccurredOn.Date < from.Value.Date);
            opening = lastBefore?.RunningBalance ?? 0m;
            return new TreasuryBookDto(from, to, opening, 0m, 0m, opening, entries, Account: book);
        }

        var first = entries[0];
        opening = first.RunningBalance - (first.Direction == TreasuryDirections.In ? first.Amount : -first.Amount);
        return new TreasuryBookDto(
            from,
            to,
            opening,
            entries.Where(x => x.Direction == TreasuryDirections.In).Sum(x => x.Amount),
            entries.Where(x => x.Direction == TreasuryDirections.Out).Sum(x => x.Amount),
            entries[^1].RunningBalance,
            entries,
            Account: book);
    }

    public async Task<OperationResult<TreasuryEntryDto>> CreateAsync(SaveTreasuryEntryRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var direction = (request.Direction ?? string.Empty).Trim().ToLowerInvariant();
        if (direction is not TreasuryDirections.In and not TreasuryDirections.Out)
        {
            return OperationResult<TreasuryEntryDto>.Fail("حدد الوارد أو المنصرف.");
        }

        if (request.OccurredOn == default)
        {
            return OperationResult<TreasuryEntryDto>.Fail("تاريخ الحركة مطلوب.");
        }

        if (request.Amount <= 0)
        {
            return OperationResult<TreasuryEntryDto>.Fail("المبلغ يجب أن يكون أكبر من صفر.");
        }

        var description = (request.Description ?? string.Empty).Trim();
        if (description.Length == 0)
        {
            return OperationResult<TreasuryEntryDto>.Fail("البيان مطلوب.");
        }

        var account = TreasuryAccounts.Normalize(request.Account);
        if (account is null)
        {
            return OperationResult<TreasuryEntryDto>.Fail("حدد الخزينة أو البنك.");
        }

        var category = TreasuryCategories.Canonical(request.Category);
        if (!TreasuryCategories.IsAllowed(direction, category, account))
        {
            return OperationResult<TreasuryEntryDto>.Fail("تصنيف الحركة غير مطابق لاتجاه الخزينة.");
        }

        if (!string.IsNullOrWhiteSpace(request.SourceDocumentType) && request.SourceDocumentId is { } sourceId)
        {
            var existing = await _db.TreasuryEntries.FirstOrDefaultAsync(
                x => x.SourceDocumentType == request.SourceDocumentType && x.SourceDocumentId == sourceId,
                cancellationToken);
            if (existing is not null)
            {
                var bookExisting = await GetBookAsync(cancellationToken: cancellationToken, account: existing.Account);
                return OperationResult<TreasuryEntryDto>.Ok(bookExisting.Entries.First(x => x.Id == existing.Id));
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            var duplicateNote = await _db.TreasuryEntries.AnyAsync(x => x.Notes == request.Notes.Trim() && x.Account == account, cancellationToken);
            if (duplicateNote)
            {
                var bookDup = await GetBookAsync(cancellationToken: cancellationToken, account: account);
                var mappedDup = bookDup.Entries.FirstOrDefault(x => x.Notes == request.Notes.Trim());
                if (mappedDup is not null)
                {
                    return OperationResult<TreasuryEntryDto>.Ok(mappedDup);
                }
            }
        }

        var amount = decimal.Round(request.Amount, 4, MidpointRounding.AwayFromZero);
        var entry = new TreasuryEntry
        {
            Id = UuidV7.New(),
            OccurredOn = request.OccurredOn.Date,
            Account = account,
            Direction = direction,
            Category = category,
            Description = description,
            Amount = amount,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            SourceDocumentType = string.IsNullOrWhiteSpace(request.SourceDocumentType) ? null : request.SourceDocumentType.Trim(),
            SourceDocumentId = request.SourceDocumentId,
            CreatedByUserId = actorUserId,
            UpdatedByUserId = actorUserId
        };
        _db.TreasuryEntries.Add(entry);
        _db.AuditLogs.Add(Audits.Create(actorUserId, "treasury.create", "treasury_entry", entry.Id, null, new
        {
            entry.OccurredOn,
            entry.Account,
            entry.Direction,
            entry.Category,
            entry.Description,
            entry.Amount
        }));
        await _db.SaveChangesAsync(cancellationToken);

        var book = await GetBookAsync(cancellationToken: cancellationToken, account: account);
        var mapped = book.Entries.First(x => x.Id == entry.Id);
        return OperationResult<TreasuryEntryDto>.Ok(mapped);
    }

    public async Task<OperationResult<bool>> CreateTransferAsync(SaveBankTransferRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var toBank = (request.Direction ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "to_bank" => true,
            "from_bank" => false,
            _ => (bool?)null
        };
        if (toBank is null)
        {
            return OperationResult<bool>.Fail("حدد إيداع في البنك أو سحب من البنك.");
        }

        if (request.OccurredOn == default)
        {
            return OperationResult<bool>.Fail("تاريخ الحركة مطلوب.");
        }

        if (request.Amount <= 0)
        {
            return OperationResult<bool>.Fail("المبلغ يجب أن يكون أكبر من صفر.");
        }

        var amount = decimal.Round(request.Amount, 4, MidpointRounding.AwayFromZero);
        var description = string.IsNullOrWhiteSpace(request.Description)
            ? (toBank.Value ? "إيداع في بنك مصر" : "سحب من بنك مصر")
            : request.Description.Trim();
        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        var transferId = UuidV7.New();
        var category = toBank.Value ? TreasuryCategories.BankDeposit : TreasuryCategories.BankWithdrawal;

        // Both sides share one transfer id, so they are saved, shown and deleted together.
        TreasuryEntry Side(string account, string direction) => new()
        {
            Id = UuidV7.New(),
            OccurredOn = request.OccurredOn.Date,
            Account = account,
            Direction = direction,
            Category = category,
            Description = description,
            Amount = amount,
            Notes = notes,
            SourceDocumentType = SourceDocumentTypes.BankTransfer,
            SourceDocumentId = transferId,
            CreatedByUserId = actorUserId,
            UpdatedByUserId = actorUserId
        };

        var cash = Side(TreasuryAccounts.Cash, toBank.Value ? TreasuryDirections.Out : TreasuryDirections.In);
        var bank = Side(TreasuryAccounts.Bank, toBank.Value ? TreasuryDirections.In : TreasuryDirections.Out);
        _db.TreasuryEntries.AddRange(cash, bank);
        _db.AuditLogs.Add(Audits.Create(actorUserId, "treasury.transfer", "treasury_entry", transferId, null, new
        {
            cash.OccurredOn,
            Direction = toBank.Value ? "to_bank" : "from_bank",
            description,
            amount
        }));
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<bool>.Ok(true);
    }

    public async Task<OperationResult<bool>> DeleteAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var entry = await _db.TreasuryEntries.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entry is null)
        {
            return OperationResult<bool>.Fail("حركة الخزينة غير موجودة.", 404);
        }

        if (entry.SourceDocumentType == SourceDocumentTypes.BankTransfer && entry.SourceDocumentId is { } transferId)
        {
            var sides = await _db.TreasuryEntries
                .Where(x => x.SourceDocumentType == SourceDocumentTypes.BankTransfer && x.SourceDocumentId == transferId)
                .ToListAsync(cancellationToken);
            _db.TreasuryEntries.RemoveRange(sides);
            _db.AuditLogs.Add(Audits.Create(actorUserId, "treasury.transfer.delete", "treasury_entry", transferId, new
            {
                entry.OccurredOn,
                entry.Description,
                entry.Amount
            }, new { deleted = true }));
            await _db.SaveChangesAsync(cancellationToken);
            return OperationResult<bool>.Ok(true);
        }

        if (!string.IsNullOrWhiteSpace(entry.SourceDocumentType))
        {
            return OperationResult<bool>.Fail("هذه الحركة مربوطة بتحصيل أو سداد. ألغِ التحصيل من المبيعات أو المشتريات بدلاً من حذفها هنا.");
        }

        _db.TreasuryEntries.Remove(entry);
        _db.AuditLogs.Add(Audits.Create(actorUserId, "treasury.delete", "treasury_entry", entry.Id, new
        {
            entry.OccurredOn,
            entry.Direction,
            entry.Category,
            entry.Description,
            entry.Amount
        }, new { deleted = true }));
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<bool>.Ok(true);
    }

    private static decimal Signed(TreasuryEntry row)
        => row.Direction == TreasuryDirections.In ? row.Amount : -row.Amount;

    private static TreasuryEntryDto Map(TreasuryEntry row, decimal running)
        => new(
            row.Id,
            row.OccurredOn,
            row.Direction,
            row.Category,
            row.Description,
            row.Amount,
            row.Notes,
            running,
            !string.IsNullOrWhiteSpace(row.SourceDocumentType) && row.SourceDocumentType != SourceDocumentTypes.BankTransfer,
            row.Account,
            row.SourceDocumentType == SourceDocumentTypes.BankTransfer);
}
