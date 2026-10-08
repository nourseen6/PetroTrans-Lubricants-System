using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetroTrans.Application.Assistant;
using PetroTrans.Application.Catalog;
using PetroTrans.Application.Identity;
using PetroTrans.Application.Operations;
using PetroTrans.Application.Sales;
using PetroTrans.Infrastructure.Assistant;
using PetroTrans.Infrastructure.Catalog;
using PetroTrans.Infrastructure.Identity;
using PetroTrans.Infrastructure.Operations;
using PetroTrans.Infrastructure.Persistence;
using PetroTrans.Infrastructure.Sales;

namespace PetroTrans.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string sqliteConnectionString, string databasePath)
    {
        services.AddSingleton(new SqliteRuntime(databasePath));
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlite(sqliteConnectionString);
        });
        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ICustomerTypeService, CustomerTypeService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IPricingService, PricingService>();
        services.AddScoped<IHomeService, HomeService>();
        services.AddScoped<ISalesInvoiceService, SalesInvoiceService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IPartyLedgerService, PartyLedgerService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<IPaymentMethodService, PaymentMethodService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ISupplierPaymentService, SupplierPaymentService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IPurchasingService, PurchasingService>();
        services.AddScoped<ISalesReturnService, SalesReturnService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<ITreasuryService, TreasuryService>();
        services.AddSingleton<IAssistantProvider, LocalDeterministicAssistantProvider>();
        services.AddScoped<IAssistantService, AssistantService>();
        return services;
    }
}
