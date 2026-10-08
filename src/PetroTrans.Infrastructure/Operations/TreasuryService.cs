using Microsoft.EntityFrameworkCore;
using PetroTrans.Application.Catalog;
using PetroTrans.Application.Operations;
using PetroTrans.Domain;
using PetroTrans.Domain.Finance;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Infrastructure.Operations;

public sealed class TreasuryService : ITreasuryService
{
    private readonly AppDbContext _db;

    public TreasuryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<TreasuryBookDto> GetBookAsync(DateTime? from = null, DateTime? to = null, string? category = null, CancellationToken cancellationToken = default)
    {
        var rows = await _db.TreasuryEntries.AsNoTracking()
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
            return new TreasuryBookDto(from, to, 0m, totalIn, totalOut, categoryTotal, entries, filter, categoryTotal);
        }

        var opening = 0m;
        if (entries.Count == 0)
        {
            var lastBefore = mapped.LastOrDefault(x => from is null || x.OccurredOn.Date < from.Value.Date);
            opening = lastBefore?.RunningBalance ?? 0m;
            return new TreasuryBookDto(from, to, opening, 0m, 0m, opening, entries);
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
            entries);
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

        var category = TreasuryCategories.Canonical(request.Category);
        if (!TreasuryCategories.IsAllowed(direction, category))
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
                var bookExisting = await GetBookAsync(cancellationToken: cancellationToken);
                return OperationResult<TreasuryEntryDto>.Ok(bookExisting.Entries.First(x => x.Id == existing.Id));
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            var duplicateNote = await _db.TreasuryEntries.AnyAsync(x => x.Notes == request.Notes.Trim(), cancellationToken);
            if (duplicateNote)
            {
                var bookDup = await GetBookAsync(cancellationToken: cancellationToken);
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
            entry.Direction,
            entry.Category,
            entry.Description,
            entry.Amount
        }));
        await _db.SaveChangesAsync(cancellationToken);

        var book = await GetBookAsync(cancellationToken: cancellationToken);
        var mapped = book.Entries.First(x => x.Id == entry.Id);
        return OperationResult<TreasuryEntryDto>.Ok(mapped);
    }

    public async Task<OperationResult<bool>> DeleteAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var entry = await _db.TreasuryEntries.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entry is null)
        {
            return OperationResult<bool>.Fail("حركة الخزينة غير موجودة.", 404);
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
            !string.IsNullOrWhiteSpace(row.SourceDocumentType));
}
