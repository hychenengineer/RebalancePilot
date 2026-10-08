let currentAccountId = 1;
let currentProposalId = null;
let currentConcurrencyToken = null;

function logEvent(tag, message) {
  const logEl = document.getElementById("eventLog");
  if (!logEl) return;
  const time = new Date().toLocaleTimeString();
  const entry = document.createElement("div");
  entry.className = "timeline-entry";
  entry.innerHTML = `<span class="timeline-tag">[${time} ${tag}]</span> ${message}`;
  logEl.prepend(entry);
}

function setPrompt(text) {
  document.getElementById("copilotInput").value = text;
}

async function checkHealth() {
  try {
    const res = await fetch("/healthz");
    if (res.ok) {
      document.getElementById("healthLabel").innerText = "Engine: Healthy";
    }
  } catch (e) {
    document.getElementById("healthLabel").innerText = "Engine: Offline";
  }
}

function renderHoldings(positions, totalAum) {
  const tbody = document.getElementById("holdingsBody");
  if (!tbody) return;
  tbody.innerHTML = "";

  if (!positions || positions.length === 0) {
    tbody.innerHTML = `<tr><td colspan="8" style="text-align: center; padding: 15px; color: #64748b;">No positions found in this portfolio.</td></tr>`;
    return;
  }

  document.getElementById("holdingsCount").innerText = `${positions.length} Positions • $${Number(totalAum).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} AUM`;

  positions.forEach(p => {
    const row = document.createElement("tr");
    const isEquity = (p.assetClass || "").toLowerCase().includes("equit");
    const badgeClass = isEquity ? "badge-equity" : "badge-fixed";
    const weightPct = (Number(p.weightPct || 0) * 100).toFixed(1);
    const gainVal = Number(p.unrealizedGain || 0);
    const gainStr = gainVal > 0 ? `+$${gainVal.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}` 
                  : gainVal < 0 ? `-$${Math.abs(gainVal).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}` 
                  : "$0.00";
    const gainColor = gainVal > 0 ? "#059669" : gainVal < 0 ? "#b91c1c" : "#64748b";

    row.innerHTML = `
      <td><strong>${p.symbol}</strong></td>
      <td>${p.securityName}</td>
      <td><span class="${badgeClass}">${p.assetClass}</span></td>
      <td>${Number(p.shares).toFixed(2)}</td>
      <td>$${Number(p.currentPrice).toFixed(2)}</td>
      <td><strong>$${Number(p.totalValue).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</strong></td>
      <td>${weightPct}%</td>
      <td style="color: ${gainColor}; font-weight: 600;">${gainStr}</td>
    `;
    tbody.appendChild(row);
  });
}

function renderProposal(proposal, emailDraft) {
  if (!proposal) return;
  currentProposalId = proposal.proposalId;
  document.getElementById("proposalStatus").innerText = `Status: ${proposal.status || 'Awaiting Sign-Off'}`;

  if (emailDraft) {
    document.getElementById("emailDraft").innerText = emailDraft;
  }

  const tbody = document.getElementById("ordersBody");
  tbody.innerHTML = "";

  const orders = proposal.orders || [];

  if (orders.length === 0) {
    tbody.innerHTML = `<tr><td colspan="7" style="text-align: center; padding: 20px; color: #64748b;">No rebalance trades required. Portfolio is aligned within model tolerance.</td></tr>`;
    document.getElementById("btnApprove").disabled = true;
    document.getElementById("fiduciarySummary").innerHTML = `Total Trade Volume: <strong>$0.00</strong> • Est. Realized Gains: <strong>$0.00</strong>`;
    return;
  }

  orders.forEach(order => {
    const row = document.createElement("tr");
    const isSell = (order.action || "").toString().toLowerCase() === "sell";
    const actionBadge = isSell ? '<span class="badge-sell">SELL</span>' : '<span class="badge-buy">BUY</span>';
    const shares = Number(order.shares || 0).toFixed(2);
    const price = Number(order.estimatedPrice || 0).toFixed(2);
    const value = Number(order.estimatedValue || 0).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const gainVal = Number(order.estimatedCapitalGain || 0);
    const gainStr = gainVal > 0 ? `+$${gainVal.toFixed(2)}` : '$0.00';
    const gainColor = gainVal > 0 ? '#b91c1c' : '#059669';

    row.innerHTML = `
      <td>${actionBadge}</td>
      <td><strong>${order.symbol}</strong></td>
      <td>${order.securityName}</td>
      <td>${shares}</td>
      <td>$${price}</td>
      <td>$${value}</td>
      <td style="color: ${gainColor}; font-weight: 600;">${gainStr}</td>
    `;
    tbody.appendChild(row);
  });

  document.getElementById("btnApprove").disabled = false;
  const vol = Number(proposal.totalTradeVolume || 0).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  const gains = Number(proposal.totalEstimatedCapitalGains || 0).toFixed(2);
  document.getElementById("fiduciarySummary").innerHTML = `
    Total Trade Volume: <strong>$${vol}</strong> • 
    Est. Realized Gains: <strong style="color: #b91c1c;">$${gains}</strong>
  `;

  // Render Post-Trade Allocation Impact Banner
  const banner = document.getElementById("allocationImpactBanner");
  if (banner) {
    banner.style.display = "block";
    const curEqPct = (Number(proposal.currentEquitiesPct || 0.75) * 100).toFixed(1);
    const projEqPct = (Number(proposal.projectedEquitiesPct || 0.60) * 100).toFixed(1);
    const curFiPct = (Number(proposal.currentFixedIncomePct || 0.25) * 100).toFixed(1);
    const projFiPct = (Number(proposal.projectedFixedIncomePct || 0.40) * 100).toFixed(1);

    const eqTrim = Math.max(0, Number(proposal.currentEquitiesValue || 0) - Number(proposal.projectedEquitiesValue || 0));
    const fiAdd = Math.max(0, Number(proposal.projectedFixedIncomeValue || 0) - Number(proposal.currentFixedIncomeValue || 0));

    document.getElementById("impactEquities").innerText = `${curEqPct}% ➔ ${projEqPct}% (-$${eqTrim.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })})`;
    document.getElementById("impactBonds").innerText = `${curFiPct}% ➔ ${projFiPct}% (+$${fiAdd.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })})`;
    document.getElementById("impactModelStatus").innerText = `${projEqPct}% Stocks / ${projFiPct}% Bonds`;

    const badge = document.getElementById("targetAchievedBadge");
    if (proposal.isTargetModelAchieved) {
      badge.className = "success-tag";
      badge.innerText = "🎯 60/40 Target Model Realigned";
    } else {
      badge.className = "alert-tag";
      badge.innerText = `⚠️ Partial Rebalance (${projEqPct}/${projFiPct}) - Tax Budget Capped`;
    }

    // Update the top 'After Rebalance (Projected)' card dynamically
    document.getElementById("barTargetEq").style.width = `${projEqPct}%`;
    document.getElementById("barTargetEq").innerText = `${projEqPct}% Stocks`;
    document.getElementById("barTargetFi").style.width = `${projFiPct}%`;
    document.getElementById("barTargetFi").innerText = `${projFiPct}% Bonds`;

    document.getElementById("legendTargetEq").innerText = `Stocks: $${Number(proposal.projectedEquitiesValue).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} (${projEqPct}%)`;
    document.getElementById("legendTargetFi").innerText = `Bonds: $${Number(proposal.projectedFixedIncomeValue).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} (${projFiPct}%)`;
  }

  logEvent("Proposal Ready", `Proposal #${proposal.proposalId} populated with ${orders.length} orders. Post-trade: ${(Number(proposal.projectedEquitiesPct)*100).toFixed(1)}% Stocks / ${(Number(proposal.projectedFixedIncomePct)*100).toFixed(1)}% Bonds`);
}

async function loadAccount(accountId) {
  currentAccountId = accountId;
  currentProposalId = null;
  document.getElementById("btnApprove").disabled = true;
  document.getElementById("proposalStatus").innerText = "Status: Awaiting Advisor Instruction";

  try {
    const res = await fetch(`/api/portfolios/${accountId}/drift`);
    if (!res.ok) throw new Error("Failed to load portfolio");
    const data = await res.json();

    currentConcurrencyToken = data.concurrencyToken;

    document.getElementById("clientName").innerText = data.clientName;
    document.getElementById("clientAvatar").innerText = data.clientName.split(" ").map(n => n[0]).join("");
    document.getElementById("accountNumber").innerText = `${data.accountNumber} • ${data.custodian}`;
    document.getElementById("totalAum").innerText = `$${Number(data.totalAum).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
    document.getElementById("targetModel").innerText = `Model: ${data.targetModelName}`;

    // Render Full Portfolio Holdings Table!
    renderHoldings(data.positions, data.totalAum);

    const curEqPct = (data.currentEquitiesPct * 100).toFixed(1);
    const curFiPct = (data.currentFixedIncomePct * 100).toFixed(1);
    const tgtEqPct = (data.targetEquitiesPct * 100).toFixed(1);
    const tgtFiPct = (data.targetFixedIncomePct * 100).toFixed(1);

    document.getElementById("barCurrentEq").style.width = `${curEqPct}%`;
    document.getElementById("barCurrentEq").innerText = `${curEqPct}% Stocks`;
    document.getElementById("barCurrentFi").style.width = `${curFiPct}%`;
    document.getElementById("barCurrentFi").innerText = `${curFiPct}% Bonds`;

    document.getElementById("legendCurrentEq").innerText = `Stocks: ${(curEqPct)}%`;
    document.getElementById("legendCurrentFi").innerText = `Bonds: ${(curFiPct)}%`;

    document.getElementById("barTargetEq").style.width = `${tgtEqPct}%`;
    document.getElementById("barTargetEq").innerText = `${tgtEqPct}% Stocks`;
    document.getElementById("barTargetFi").style.width = `${tgtFiPct}%`;
    document.getElementById("barTargetFi").innerText = `${tgtFiPct}% Bonds`;

    const driftBadge = document.getElementById("driftToleranceBadge");
    const statusTag = document.getElementById("currentStatusTag");

    if (data.isDriftToleranceExceeded) {
      const eqDrift = (data.equitiesDriftPct * 100).toFixed(1);
      driftBadge.className = "alert-tag";
      driftBadge.innerText = `Drift: ${eqDrift > 0 ? '+' : ''}${eqDrift}% Over Limit`;
      statusTag.className = "alert-tag";
      statusTag.innerText = "Rebalance Needed";
      logEvent("Drift Alert", `Account #${data.accountNumber} (${data.clientName}) drifted ${eqDrift}% from target model.`);
    } else {
      driftBadge.className = "success-tag";
      driftBadge.innerText = "Within Tolerance";
      statusTag.className = "success-tag";
      statusTag.innerText = "Compliant";
    }

    // Reset proposal section to waiting state until advisor clicks 'Generate Proposal'
    const banner = document.getElementById("allocationImpactBanner");
    if (banner) banner.style.display = "none";

    document.getElementById("btnApprove").disabled = true;
    document.getElementById("proposalStatus").innerText = "Status: Awaiting Advisor Instruction";
    document.getElementById("fiduciarySummary").innerHTML = "Total Trade Volume: <strong>$0.00</strong> • Est. Realized Gains: <strong>$0.00</strong>";
    document.getElementById("ordersBody").innerHTML = `
      <tr><td colspan="7" style="text-align: center; color: #64748b; padding: 25px;">
        ✨ Portfolio drifted by <strong>${data.isDriftToleranceExceeded ? (data.equitiesDriftPct * 100).toFixed(1) + '%' : '0%'}</strong>. 
        Click <strong style="color: #2563eb;">Generate Proposal</strong> above to simulate rebalancing trades.
      </td></tr>`;
    document.getElementById("emailDraft").innerText = 
      "Subject: Portfolio Review — Awaiting proposal generation...\n\nClick 'Generate Proposal' to simulate rebalance trades and draft the fiduciary client explanation email.";

  } catch (err) {
    console.error(err);
    logEvent("Error", "Failed to retrieve portfolio drift: " + err.message);
  }
}

const PROVIDER_MODELS = {
  "Groq": [
    { id: "openai/gpt-oss-120b", name: "openai/gpt-oss-120b (Flagship Reasoning)" },
    { id: "openai/gpt-oss-20b", name: "openai/gpt-oss-20b (Fast & Lightweight)" },
    { id: "qwen/qwen3.8-27b", name: "qwen/qwen3.8-27b (High Performance)" },
    { id: "allam-2-7b", name: "allam-2-7b" },
    { id: "custom", name: "Custom / Other Model ID..." }
  ],
  "Google": [
    { id: "gemini-1.5-flash", name: "Gemini 1.5 Flash (Default)" },
    { id: "gemini-1.5-pro", name: "Gemini 1.5 Pro" }
  ],
  "OpenAI": [
    { id: "gpt-4o-mini", name: "GPT-4o Mini (Default)" },
    { id: "gpt-4o", name: "GPT-4o (Omni)" }
  ],
  "Grok": [
    { id: "grok-2-latest", name: "Grok 2 Latest (xAI)" },
    { id: "grok-beta", name: "Grok Beta (xAI)" }
  ],
  "Offline": [
    { id: "offline-deterministic", name: "Deterministic Rules Engine (Free & Instant)" }
  ]
};

function updateModelDropdown() {
  const provider = document.getElementById("llmProviderSelect").value;
  const select = document.getElementById("llmModelSelect");
  const customInput = document.getElementById("llmCustomModelInput");
  if (!select) return;

  select.innerHTML = "";
  const models = PROVIDER_MODELS[provider] || [];
  models.forEach(m => {
    const opt = document.createElement("option");
    opt.value = m.id;
    opt.innerText = m.name;
    select.appendChild(opt);
  });

  const keyInput = document.getElementById("llmApiKey");
  if (provider === "Offline") {
    select.disabled = true;
    if (customInput) customInput.style.display = "none";
    if (keyInput) keyInput.disabled = true;
  } else {
    select.disabled = false;
    if (keyInput) keyInput.disabled = false;
  }

  handleModelSelectChange();
}

function handleModelSelectChange() {
  const select = document.getElementById("llmModelSelect");
  const customInput = document.getElementById("llmCustomModelInput");
  if (!select || !customInput) return;

  if (select.value === "custom") {
    customInput.style.display = "inline-block";
    customInput.focus();
  } else {
    customInput.style.display = "none";
  }
}

async function runRebalance() {
  const prompt = document.getElementById("copilotInput").value;
  const btn = document.getElementById("btnRunCoPilot");
  btn.disabled = true;
  btn.innerText = "Generating Proposal...";

  const provider = document.getElementById("llmProviderSelect").value;
  const modelSelect = document.getElementById("llmModelSelect");
  const customInput = document.getElementById("llmCustomModelInput");
  let modelId = modelSelect ? modelSelect.value : "";
  if (modelId === "custom" && customInput) {
    modelId = customInput.value.trim();
  }
  const apiKey = document.getElementById("llmApiKey").value.trim();

  logEvent("Rebalance Engine", `Running simulation [${provider} / ${modelId}]: "${prompt}"`);

  try {
    const res = await fetch("/api/rebalance/copilot-ask", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        accountId: parseInt(currentAccountId),
        prompt: prompt,
        provider: provider,
        apiKey: apiKey.length > 0 ? apiKey : null,
        modelId: modelId.length > 0 ? modelId : null
      })
    });

    if (!res.ok) {
      const err = await res.json();
      throw new Error(err.message || (err.errors ? err.errors.join(", ") : "Simulation failed"));
    }

    const data = await res.json();

    if (!data.isValidPrompt) {
      // Input was rejected by the AI guardrail validator
      currentProposalId = null;
      document.getElementById("btnApprove").disabled = true;
      document.getElementById("proposalStatus").innerHTML = `<span class="alert-tag">Input Rejected (No Proposal Generated)</span>`;
      
      const banner = document.getElementById("allocationImpactBanner");
      if (banner) banner.style.display = "none";

      document.getElementById("ordersBody").innerHTML = `
        <tr><td colspan="7" style="text-align: center; color: #dc2626; padding: 25px; background: #fef2f2; border-radius: 8px;">
          <strong>🛑 Invalid or Unrelated Input:</strong><br />
          ${data.validationMessage || "The prompt was not recognized as a valid portfolio rebalancing instruction."}<br />
          <span style="color: #64748b; font-size: 0.85rem; margin-top: 6px; display: inline-block;">
            Please provide a valid directive such as <em>'Rebalance portfolio, minimize capital gains taxes (budget $2,000)'</em>.
          </span>
        </td></tr>`;

      document.getElementById("emailDraft").innerText = data.clientEmailDraft || "No email generated for invalid input.";
      document.getElementById("fiduciarySummary").innerHTML = "Total Trade Volume: <strong>$0.00</strong> • Est. Realized Gains: <strong>$0.00</strong>";

      logEvent("Input Rejected", `Advisor prompt deemed invalid: "${prompt}"`);
      return;
    }

    renderProposal(data.rebalanceProposal, data.clientEmailDraft);

    if (data.clientEmailDraft && data.clientEmailDraft.includes("encountered:")) {
      logEvent("AI Warning", `LLM Call Failed (${provider}): Check API Key / Permissions.`);
    } else {
      logEvent("AI Completed", `Generated rationale & client communication via ${provider}.`);
    }

  } catch (err) {
    console.error(err);
    logEvent("Error", err.message);
    alert(err.message);
  } finally {
    btn.disabled = false;
    btn.innerText = "Generate Proposal";
  }
}

async function approveTrades() {
  if (!currentProposalId) return;

  const btn = document.getElementById("btnApprove");
  btn.disabled = true;
  btn.innerHTML = "Signing & Dispatching...";

  logEvent("Human-in-the-Loop", `Advisor sign-off initiated for Proposal #${currentProposalId}...`);

  try {
    const res = await fetch("/api/rebalance/approve", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        proposalId: currentProposalId,
        approvedBy: "Hsin-Yu Chen (Senior Architect)",
        expectedConcurrencyToken: currentConcurrencyToken
      })
    });

    if (!res.ok) {
      const err = await res.json();
      throw new Error(err.message || (err.errors ? err.errors.join(", ") : "Approval failed"));
    }

    const data = await res.json();

    document.getElementById("proposalStatus").innerHTML = `<span class="success-tag">Approved & Dispatched (FIX Orders Sent)</span>`;
    btn.innerHTML = "✅ Orders Dispatched";

    logEvent("MediatR: Publish", `TradeProposalApprovedEvent published to all in-process event handlers.`);
    logEvent("Audit Ledger", `Immutable compliance record #SAVED by ${data.approvedBy}.`);
    logEvent("OMS Gateway", `${data.ordersDispatchedCount} FIX 4.4 orders successfully routed to Custodian Trading Desk via Kafka.`);
    logEvent("CRM Sync", `Client review briefing delivered to Advisor CRM queue.`);

  } catch (err) {
    console.error(err);
    btn.disabled = false;
    btn.innerHTML = "<span>🛡️</span> Approve & Execute Trades (Advisor Sign-off)";
    logEvent("Approval Error", err.message);
    alert(err.message);
  }
}

// Event Listeners
document.getElementById("clientSelect").addEventListener("change", (e) => {
  loadAccount(e.target.value);
});

document.getElementById("btnRunCoPilot").addEventListener("click", runRebalance);
document.getElementById("btnApprove").addEventListener("click", approveTrades);

document.getElementById("btnCopyEmail").addEventListener("click", () => {
  const text = document.getElementById("emailDraft").innerText;
  navigator.clipboard.writeText(text);
  alert("Client email draft copied to clipboard!");
});

document.getElementById("llmProviderSelect").addEventListener("change", updateModelDropdown);
document.getElementById("llmModelSelect").addEventListener("change", handleModelSelectChange);

// Initialization
updateModelDropdown();
checkHealth();
loadAccount(1);
