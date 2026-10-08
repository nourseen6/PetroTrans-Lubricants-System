using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PetroTrans.Infrastructure.Persistence;

#nullable disable

namespace PetroTrans.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260816120000_Foundation")]
    public partial class Foundation
    {
    }
}
