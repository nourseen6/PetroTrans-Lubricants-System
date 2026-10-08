# Petro Trans

Arabic-first Windows desktop system for **Petro Trans**, a lubricants distributor. Daily work used to live in separate Excel files. This application puts sales, stock, and customer balances in one program that runs on the machine with no internet.

This is a real operations system, not a sample CRUD app.

## Why it matters

Petro Trans sells on credit. An invoice is recorded, goods leave the warehouse, and the customer pays later. Stock changes with every invoice, purchase, and adjustment. A customer balance is not a number someone types in. It is the result of invoices and payments.

Excel kept the business moving, but each file stood alone: prices in one place, customers in another, stock in a third. A bad copy or a deleted row could change a balance with no trace. This system closes that gap.

- **A credit invoice is normal.** Posting the invoice records the sale and reduces stock. It does not mean the customer has paid. Collection is a separate later action.
- **Inventory is a ledger.** Quantity changes only through an in or out movement tied to a document, a user, and a time. There is no silent overwrite of the balance.
- **A product is not its package.** The oil is the product. The package (litre, gallon, drum) is what is sold, stocked, and priced.
- **Pricing is flexible.** There is a base price, a price for a customer type, and a price for one customer. An exceptional price change is a limited permission.
- **It works offline.** Data stays on the machine in SQLite. If the internet drops, sales and stock do not stop.
- **Arabic comes first.** The interface is right-to-left and messages are in Arabic, so the branch manager can work without translating the screen.
- **Sensitive actions leave a trail.** Audit runs in the background, without extra approval steps in front of the user.

The current user is one branch manager. Permissions are already split (sales, warehouse, accountant, manager) so the same system can take more users later without being rewritten.

## What it covers

| Screen | Work |
|---|---|
| Home | Today's summary, recent invoices and payments, customers with an outstanding balance |
| Sales | Draft and posted invoices, and printing |
| Customers | The customer file, the balance derived from transactions, and the statement |
| Products | The product and its packages |
| Pricing | Base prices and customer-specific prices |
| Inventory | Balances, movements, adjustments, and transfers between warehouses |
| Purchasing | Supplier bills |
| Treasury | Cash in and cash out |
| Reports | Operational follow-up |
| Assistant | Draft help inside the daily work |
| Settings | Users, permissions, payment methods, and backup |

Payment methods are not a fixed list in code. They are configured in Settings, and a payment cannot be posted without a selected method.

## How it is built

The application is a Windows icon, not a site opened in a browser.

1. The desktop program (WPF) opens a WebView2 window.
2. The same program starts a local API bound only to `127.0.0.1`.
3. The Arabic React interface loads inside that window.
4. The SQLite database stays on the machine.

Layers are `Domain`, then `Application`, then `Infrastructure`, then `Api`. The `Desktop` program is what the user opens. Sales and stock rules are not tied to the shape of the screen, and the API is already there if a multi-user version is needed later.

## Run it

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download), [Node.js](https://nodejs.org/), and WebView2 (included on Windows 10 and 11).

```powershell
cd src/PetroTrans.Web
npm install
npm run build
cd ../..
dotnet run --project src/PetroTrans.Desktop
```

Tests:

```powershell
dotnet test PetroTrans.sln
```

## What is not in this repository

The old Excel files and real customer invoices stay on the machine. They are not part of this public repository. The repository contains the system and its documentation, not live operating data.
