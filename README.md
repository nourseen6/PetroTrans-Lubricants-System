<div align="center">

# Petro Trans

**A Windows desktop system for a lubricants distributor. Stock is a ledger. A posted invoice is still unpaid.**

Inventory movements · credit sales · balances from transactions · Arabic RTL · offline on one machine

[What it does](#what-it-does) · [Why](#why) · [How it is built](#how-it-is-built) · [Run](#run)

<p>
  <img src="https://raw.githubusercontent.com/nourseen6/nourseen6/main/assets/badges/react.svg" alt="React">
  <img src="https://raw.githubusercontent.com/nourseen6/nourseen6/main/assets/badges/typescript.svg" alt="TypeScript">
  <img src="https://raw.githubusercontent.com/nourseen6/nourseen6/main/assets/badges/javascript.svg" alt="JavaScript">
  <img src="https://raw.githubusercontent.com/nourseen6/nourseen6/main/assets/badges/sql.svg" alt="SQL">
</p>

.NET 8 · ASP.NET Core · SQLite

</div>

---

## What it does

Petro Trans replaces the Excel files a branch used for daily sales and stock.

1. **Sell on credit.** Posting an invoice records the sale and reduces stock. Payment is a later action.
2. **Move stock only through a ledger.** Every quantity change is a movement tied to a document, a user, and a time.
3. **Price the package, not a vague product.** The oil is the product. The litre, gallon, or drum is what is sold, stocked, and priced.
4. **Derive the customer balance.** It comes from invoices and payments, not from a typed cell.
5. **Keep working offline.** The database stays on the machine. The API listens on `127.0.0.1` only.
6. **Work in Arabic.** Right-to-left layout and Arabic messages for the person actually using it.

Sales, warehouse, accounting, and management already have separate permissions, with an audit trail behind sensitive actions.

| Area | Handles |
|---|---|
| Sales | Draft and posted invoices, printing |
| Customers | File, balance, statement |
| Products and pricing | Packages, base price, price for one customer |
| Inventory | Balances, movements, adjustments, transfers |
| Purchasing and treasury | Supplier bills, cash in, cash out |
| Settings | Users, roles, payment methods, backup |

## Why

A credit sale and a stock count were living in different spreadsheets. One overwritten cell could change what a customer owes, with nothing to show how. The system makes the sale, the stock movement, and the later payment three separate facts.

## How it is built

The user opens a Windows icon. A WPF host starts a local ASP.NET Core API and shows the React UI inside WebView2. Business rules sit in `Domain`, `Application`, and `Infrastructure`, so the screen is not where stock and credit are decided.

## Run

[.NET 8 SDK](https://dotnet.microsoft.com/download), [Node.js](https://nodejs.org/), and WebView2.

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

<p align="center">
  <a href="https://www.linkedin.com/in/nourseen-tarek-1a9718399">LinkedIn</a>
  &nbsp;&#183;&nbsp;
  <a href="mailto:nourkhfaga@gmail.com">Email</a>
</p>
