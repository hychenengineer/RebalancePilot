# RebalancePilot ??

> **Autonomous, Tax-Aware Portfolio Rebalancing Co-Pilot for Modern Wealth Management**  
> *Translating natural language advisor directives into deterministic, fiduciary-compliant trade proposals.*

---

## 1. What Is RebalancePilot?

In institutional wealth management and Registered Investment Advisor (RIA) firms, portfolio managers spend hours manually reviewing asset allocation drift, calculating tax impacts, and aligning client portfolios to target models (e.g., 60/40 Balanced Growth).

**RebalancePilot** is an intelligent portfolio rebalancing co-pilot that bridges conversational AI with institutional financial mathematics:
- **Conversational Trading Directives:** Advisors express trades in natural language (e.g., *"sell 10% on AI stocks"*, *"ignore non-energy, do a full rebalance"*, or *"cap taxes at $1,500"*).
- **Zero-Hallucination Fiduciary Engine:** LLMs extract semantic intent into a strict JSON contract, while a **deterministic C# engine** executes all financial calculations, tax-loss harvesting, and lot selection.
- **Tax & Guardrail Budgeting:** Hard dollar caps on realized capital gains are strictly enforced at the trade order level.
- **Client Communication Synthesis:** Automatically drafts personalized, compliance-ready client letters explaining the portfolio realignment rationale.

---

## 2. Software Architecture

RebalancePilot follows a **2-Stage Institutional Architecture** that strictly decouples natural language understanding from financial calculation:

```
[ Advisor Request / Webhook ]
            ¦
            ?
+------------------------------+
¦  1. Guardrail & Validation   ¦   Checks for prompt injection, gibberish & wealth intent
+------------------------------+
            ¦
            ?
+------------------------------+
¦ 2. Semantic Intent Extractor ¦   LLM (Groq / Gemini / OpenAI via Semantic Kernel)
¦    (Structured JSON Contract)¦   Outputs: AdvisorRebalanceIntent
+------------------------------+
            ¦
            ?
+------------------------------+
¦ 3. Deterministic Engine (C#) ¦   NO AI Hallucination
¦    • Portfolio Drift (75%?60%)¦   • Resolves % and $ into exact shares
¦    • Tax Lot Optimizer       ¦   • Strictly enforces capital gains budget
¦    • Fiduciary Buy/Sell Match ¦   • Reinvests proceeds into fixed income (BND)
+------------------------------+
            ¦
            ?
+------------------------------+
¦ 4. Client Letter Synthesis   ¦   Generates compliance-ready advisor-to-client email
+------------------------------+
            ¦
            ?
[ Trade Proposal Review & Human-in-the-Loop Sign-off ]
```

### Core Architecture Highlights
- **CQRS Pattern via MediatR:** Clear separation of queries and mutating commands (`SimulateRebalanceCommand`, `AdvisorCoPilotAskCommand`).
- **Semantic Intent Contract (`AdvisorIntentContract.cs`):** Strongly typed bridge between AI and financial math supporting `Shares`, `Percent`, and `Dollars`.
- **Entity Framework Core 8:** In-memory or SQLite database with concurrency tokens for multi-advisor workflows.

---

## 3. Setup & How to Run

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Windows x64 (for standalone executable)

### Quickstart

1. **Clone the repository:**
   ```bash
   git clone https://github.com/hychenengineer/RebalancePilot.git
   cd RebalancePilot
   ```

2. **Run Unit Tests:**
   ```bash
   dotnet test TamaracCoPilot.Tests/TamaracCoPilot.Tests.csproj
   ```

3. **Start the API & Web Service:**
   ```bash
   dotnet run --project TamaracCoPilot.Api/TamaracCoPilot.Api.csproj --urls "http://localhost:5000"
   ```

4. **Access the Application:**
   - Swagger API Documentation: `http://localhost:5000/swagger`
   - Health / Portfolios: `http://localhost:5000/api/portfolios`

---

## 4. Interactive Demo Cases & Prompt Examples

The demo environment is pre-seeded with a **$100,000 portfolio (Sarah Jenkins)** drifted to **75% Equities / 25% Fixed Income** across 5 distinct sectors:
- **Tech / AI:** `NVDA` ($14,000), `MSFT` ($11,000)
- **Healthcare:** `LLY` ($9,000), `JNJ` ($6,000)
- **Financials:** `JPM` ($9,000), `V` ($6,000)
- **Energy:** `XOM` ($6,000), `NEE` ($4,000)
- **Consumer:** `WMT` ($6,000), `PG` ($4,000)
- **Fixed Income:** `BND` ($25,000)

### Demo Case 1: Percentage Trims on Thematic Sectors
> **Advisor Prompt:** `"sell 10% on ai related stocks"`

- **Intent Extracted:** Action: `Sell`, Amount: `10%`, Target: AI stocks (`NVDA`, `MSFT`).
- **Engine Execution:**
  - Sells 10.00 shares NVDA ($1,400 proceeds)
  - Sells 2.75 shares MSFT ($1,100 proceeds)
  - Reinvests $2,500 into 31.25 shares BND.

### Demo Case 2: Inverted / Negated Category Protection
> **Advisor Prompt:** `"ignore non-energy related stocks, do the full rebalance"`

- **Intent Extracted:** Protect all stocks *except* Energy (`XOM`, `NEE`).
- **Engine Execution:**
  - 8 non-energy positions (`NVDA`, `MSFT`, `LLY`, `JNJ`, `JPM`, `V`, `WMT`, `PG`) are locked and untouched.
  - Sells 50 shares XOM ($6,000) and 50 shares NEE ($4,000) to reallocate into BND.

### Demo Case 3: Action Synonyms
> **Advisor Prompt:** `"retrieve 10 shares of MSFT and get rid of 5 shares on bond"`

- **Intent Extracted:** Resolves colloquialisms (`retrieve` ? Buy, `get rid of` ? Sell).
- **Engine Execution:** Generates Buy for 10 shares of MSFT and Sell for 5 shares of BND.

### Demo Case 4: Combined Thematic Protection + Hard Tax Cap
> **Advisor Prompt:** `"Don't sell AI related stocks, cap capital gains at $1,500, and rebalance to target model."`

- **Intent Extracted:** Exclude `NVDA` and `MSFT`, hard budget: `$1,500.00`.
- **Engine Execution:**
  - Zero AI shares sold.
  - Tax-loss harvesting algorithm sorts positions to minimize tax hit.
  - Realized gains strictly capped at **$1,499.90** (under $1,500 budget).

---

## 5. Standalone Binary (.exe)

You can run RebalancePilot without any .NET SDK installed by running the pre-built single-file executable in `./dist/RebalancePilot.exe`.

To rebuild the single-file binary:
```powershell
dotnet publish TamaracCoPilot.Api/TamaracCoPilot.Api.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o ./dist
```

---

## 6. Author

Crafted by **Hsinyu Chen** ([@hychenengineer](https://github.com/hychenengineer))  
*Software Engineer specializing in High-Performance FinTech, Distributed Systems, and Autonomous AI Workflows.*
