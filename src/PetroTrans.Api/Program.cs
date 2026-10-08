using PetroTrans.Api;

var app = PetroTransApiHost.Create(args);
await PetroTransApiHost.InitializeDatabaseAsync(app);
await app.RunAsync();

public partial class Program;
