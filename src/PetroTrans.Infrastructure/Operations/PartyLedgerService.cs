using Microsoft.EntityFrameworkCore;
using PetroTrans.Application.Operations;
using PetroTrans.Domain;
using PetroTrans.Domain.Finance;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Infrastructure.Operations;

public sealed class PartyLedgerService : IPartyLedgerService
{
    private readonly AppDbContext _db;

    public PartyLedgerService(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(
        string partyKind,
        Guid partyId,
        string entryType,
        decimal signedAmount,
        string sourceType,
        Guid sourceId,
        DateTime occurredAt,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        _db.PartyLedgerEntries.Add(new PartyLedgerEntry
        {
            Id = UuidV7.New(),
            PartyKind = partyKind,
            PartyId = partyId,
            EntryType = entryType,
            SignedAmount = decimal.Round(signedAmount, 4, MidpointRounding.AwayFromZero),
            SourceDocumentType = sourceType,
            SourceDocumentId = sourceId,
            OccurredAt = occurredAt,
            CreatedByUserId = actorUserId,
            UpdatedByUserId = actorUserId
        });
        await Task.CompletedTask;
    }

    public async Task<decimal> GetOutstandingAsync(string partyKind, Guid partyId, CancellationToken cancellationToken = default)
    {
        return (await _db.PartyLedgerEntries
            .Where(x => x.PartyKind == partyKind && x.PartyId == partyId)
            .Select(x => x.SignedAmount)
            .ToListAsync(cancellationToken)).Sum();
    }
}
