# Petro Trans

Desktop operations system for a lubricants distributor. It replaces scattered Excel files with one Arabic, offline Windows application for sales, inventory, customers, and treasury.

## The problem

The business sells on credit. An invoice is recorded, goods leave the warehouse, and the customer pays later. Stock changes with every sale, purchase, and adjustment. A customer balance has to come from those transactions, not from a number typed into a cell.

Excel kept the work moving, but prices, customers, and stock each lived in a separate file. One bad copy or a deleted row could change a balance with no trace.

## What the system does

- **Credit sales are a normal case.** Posting an invoice records the sale and reduces stock. It does not mean the customer has paid. Collection is a separate action.
- **Inventory is a ledger.** Quantity changes only through a movement tied to a document, a user, and a time.
- **A product is not its package.** The oil is the product. The package (litre, gallon, drum) is what is sold, stocked, and priced.
- **Pricing is layered.** Base price, price by customer type, and a price for one customer. An exceptional override is a limited permission.
- **It runs offline.** Data stays on the machine in SQLite.
- **Arabic comes first.** Right-to-left layout and Arabic messages, so the branch manager works in the language of the business.
- **Sensitive actions are audited** in the background, without extra approval steps in the daily flow.

Permissions are already split across sales, warehouse, accounting, and management, so the same system can take more than one user later.

| Area | What it handles |
|---|---|
| Home | Today's summary, recent invoices and payments, customers with a balance |
| Sales | Draft and posted invoices, printing |
| Customers | Customer file, balance from transactions, statement |
| Products | Product and its packages |
| Pricing | Base and customer-specific prices |
| Inventory | Balances, movements, adjustments, transfers |
| Purchasing | Supplier bills |
| Treasury | Cash in and cash out |
| Reports | Operational follow-up |
| Settings | Users, roles, payment methods, backup |

Payment methods are configured in Settings. A payment cannot be posted without one.

## How it is built

The user opens a Windows application, not a browser URL.

1. A WPF desktop host opens a native WebView2 window.
2. The same process starts an ASP.NET Core API bound only to `127.0.0.1`.
3. A React interface loads inside that window.
4. SQLite stays on the machine.

Business rules live in `Domain`, `Application`, and `Infrastructure`. The UI and the desktop shell sit on top. Sales and stock behaviour is not tied to the screen, and the API is ready if the system later needs more than one user.

| Piece | Choice |
|---|---|
| Desktop | .NET 8, WPF, WebView2 |
| API | ASP.NET Core, localhost only |
| UI | React, TypeScript, Arabic RTL |
| Data | SQLite, EF Core |
| Tests | xUnit against the API and the database |

## Run it

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download), [Node.js](https://nodejs.org/), and WebView2 (included on Windows 10 and 11).

```powershell
cd src/PetroTrans.Web
npm install
npm run build
cd ../..
dotnet run --project src/PetroTrans.Desktop
```

```powershell
dotnet test PetroTrans.sln
```
