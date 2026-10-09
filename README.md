# RebalancePilot (Demo / Proof of Concept)

> **Interactive Demo: Autonomous, Tax-Aware Portfolio Rebalancing Co-Pilot**  
> *A technical reference architecture demonstrating how to translate natural language advisor directives into deterministic, fiduciary-compliant trade proposals.*

---

## 1. What Is This Demo?

This repository is a **technical demonstration and proof of concept (PoC)** showcasing how modern AI can be responsibly integrated into institutional wealth management and RIA rebalancing workflows.

In production wealth management platforms, portfolio managers spend significant time manually calculating drift, checking tax lot gains, and aligning portfolios to target models (e.g., 60/40 Balanced Growth).

### Core Demonstration Goal
This demo proves that conversational AI can handle natural language instructions **without financial math hallucinations** by using a **2-stage decoupled architecture**:
1. **Conversational Semantic Translation:** An LLM interprets messy advisor language (e.g. percentages, colloquial terms, sector exclusions).
2. **Deterministic Financial Math:** Pure C# code performs the drift calculations, tax lot optimization, and trade order generation.

> **Note:** This project is an illustrative demo and architectural reference prototype, not a production-grade commercial platform or registered investment advice service.

---

## 2. Software Architecture

```
[ Advisor Input in Demo UI ]
            ¦  e.g. "Sell 10% on AI, ignore non-energy, cap taxes at $1,500"
            ?
+--------------------------------------+
¦ 1. Guardrail & Intent Validation     ¦  Catches prompt injections, gibberish & off-topic inputs
+--------------------------------------+
            ¦
            ?
+--------------------------------------+
¦ 2. Semantic Intent Extractor (LLM)   ¦  Semantic Kernel (Groq / Gemini / OpenAI)
¦    Structured JSON Contract Output   ¦  Outputs: AdvisorRebalanceIntent
+--------------------------------------+
            ¦
            ?
+--------------------------------------+
¦ 3. Deterministic Rebalance Engine    ¦  NO AI Math / Zero Hallucination
¦    (Pure C# Domain Layer)            ¦  • Drift: 75% stocks ? 60% target model
¦                                      ¦  • Resolves % and $ into exact shares
¦                                      ¦  • Enforces hard $1,500 capital gains cap
¦                                      ¦  • Reallocates excess into bonds (BND)
+--------------------------------------+
            ¦
            ?
+--------------------------------------+
¦ 4. Client Communication Synthesis    ¦  LLM drafts compliance review email for client
+--------------------------------------+
            ¦
            ?
[ Interactive Demo Dashboard: Review & Execute ]
```

### Architecture Highlights
- **CQRS via MediatR:** Clear separation of read queries and simulation commands.
- **Structured JSON Contract (`AdvisorIntentContract.cs`):** Strongly typed interface passing instructions between AI and the financial math engine.
- **In-Memory / SQLite Database:** Pre-seeded with a sample portfolio for instant zero-setup demonstration.

---

## 3. Setup & How to Run the Demo

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- (Optional) Free API key from [Groq](https://console.groq.com), Google Gemini, or OpenAI for live LLM parsing. An **offline deterministic mode** is built-in and works out of the box with zero API keys required.

### Quickstart

1. **Clone the repository:**
   ```bash
   git clone https://github.com/hychenengineer/RebalancePilot.git
   cd RebalancePilot
   ```

2. **Run the Automated Tests:**
   ```bash
   dotnet test RebalancePilot.Tests/RebalancePilot.Tests.csproj
   ```

3. **Start the Demo Web Application:**
   ```bash
   dotnet run --project RebalancePilot.Api/RebalancePilot.Api.csproj --urls "http://localhost:5000"
   ```

4. **Open the Demo UI:**
   - Interactive Web Dashboard: `http://localhost:5000`
   - Swagger API Documentation: `http://localhost:5000/swagger`

---

## 4. Pre-Seeded Demo Portfolio Context

The demo automatically boots with a realistic **$100,000 portfolio (Sarah Jenkins)** drifted to **75% Equities / 25% Fixed Income** across 5 sectors:
- **Tech / AI ($25k):** `NVDA` ($14,000), `MSFT` ($11,000)
- **Healthcare ($15k):** `LLY` ($9,000), `JNJ` ($6,000)
- **Financials ($15k):** `JPM` ($9,000), `V` ($6,000)
- **Energy ($10k):** `XOM` ($6,000), `NEE` ($4,000)
- **Consumer Retail ($10k):** `WMT` ($6,000), `PG` ($4,000)
- **Fixed Income ($25k):** `BND` ($25,000)

**Rebalance Target:** Restore to 60/40 Balanced Growth by trimming **$15,000** of excess equities and buying `BND`.

---

## 5. Demo Test Cases & Try-It Prompts

You can test these prompts directly in the demo chat bar:

### Demo Case 1: Percentage Trims on Thematic Sectors
> **Prompt:** `"sell 10% on ai related stocks"`
- **System Behavior:** 
  - Resolves "AI" into `NVDA` and `MSFT`.
  - Calculates 10% trim: sells 10.00 shares NVDA ($1,400) and 2.75 shares MSFT ($1,100).
  - Automatically reinvests $2,500 proceeds into 31.25 shares of BND.

### Demo Case 2: Inverted Category Exclusions
> **Prompt:** `"ignore non-energy related stocks, do the full rebalance"`
- **System Behavior:**
  - Inverts the rule: protects all stocks *except* Energy (`XOM`, `NEE`).
  - Completely locks the other 8 stocks (`NVDA`, `MSFT`, `LLY`, `JNJ`, `JPM`, `V`, `WMT`, `PG`).
  - Sells 50 shares of XOM ($6,000) and 50 shares of NEE ($4,000) into BND.

### Demo Case 3: Action Synonyms & Colloquialisms
> **Prompt:** `"retrieve 10 shares of MSFT and get rid of 5 shares on bond"`
- **System Behavior:**
  - Correctly maps `"retrieve"` ? Buy and `"get rid of"` ? Sell without rigid keyword failures.

### Demo Case 4: Sector Protection + Hard Tax Cap Guardrail
> **Prompt:** `"Don't sell AI related stocks, cap capital gains at $1,500, and rebalance to target model."`
- **System Behavior:**
  - Excludes NVDA and MSFT (0 shares sold).
  - Sorts remaining stocks by lowest gain to harvest tax efficiency.
  - Stops selling immediately once realized gains hit **$1,499.90** to honor the $1,500 budget.

---

## 6. Standalone Executable (.exe)

For quick offline demonstrations on Windows without installing the .NET SDK, a standalone single-file binary is available in `./dist/RebalancePilot.exe`.

---

## 7. Author

Demo crafted by **Hsinyu Chen** ([@hychenengineer](https://github.com/hychenengineer))  
*Technical Proof of Concept for Autonomous Wealth Management Workflows.*
