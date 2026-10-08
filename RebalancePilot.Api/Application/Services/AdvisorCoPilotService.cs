using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI;
using System.ClientModel;
using System.Text.RegularExpressions;
using RebalancePilot.Api.Application.DTOs;
using RebalancePilot.Api.Domain.Entities;
using RebalancePilot.Api.Domain.Enums;

using RebalancePilot.Api.Domain.Contracts;

namespace RebalancePilot.Api.Application.Services;

public record PromptValidationResult(bool IsValid, string? FeedbackMessage);
public record SpecificTradeInstruction(
    TradeAction Action, 
    string Symbol, 
    decimal Shares, 
    TradeAmountType AmountType = TradeAmountType.Shares, 
    decimal? RawAmount = null);

public interface IAdvisorCoPilotService
{
    Task<PromptValidationResult> ValidatePromptIntentAsync(
        string prompt,
        string? provider = null,
        string? apiKey = null,
        string? modelId = null,
        CancellationToken cancellationToken = default);

    Task<AdvisorRebalanceIntent> ExtractAdvisorIntentAsync(
        string prompt,
        Account account,
        string? provider = null,
        string? apiKey = null,
        string? modelId = null,
        CancellationToken cancellationToken = default);

    List<SpecificTradeInstruction> ParseSpecificTrades(string prompt, IEnumerable<Position> positions);
    List<string> ParseExcludedSymbols(string prompt, IEnumerable<Position> positions);
    decimal? ExtractCapitalGainsBudget(string prompt);
    Task<string> GenerateClientEmailAsync(
        Account account, 
        TradeProposal proposal, 
        PortfolioDriftDto drift,
        string? provider = null,
        string? apiKey = null,
        string? modelId = null,
        CancellationToken cancellationToken = default);
}

public class AdvisorCoPilotService : IAdvisorCoPilotService
{
    private readonly IConfiguration _config;
    private readonly ILogger<AdvisorCoPilotService> _logger;

    public AdvisorCoPilotService(IConfiguration config, ILogger<AdvisorCoPilotService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<PromptValidationResult> ValidatePromptIntentAsync(
        string prompt,
        string? provider = null,
        string? apiKey = null,
        string? modelId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt) || prompt.Trim().Length < 3)
        {
            return new PromptValidationResult(false, "The rebalance prompt is too short or empty. Please enter an instruction related to portfolio management, asset allocation, or tax budgeting.");
        }

        var trimmed = prompt.Trim();

        // 1. Check for obvious gibberish (e.g. repeated characters, no spaces in long random text)
        if (Regex.IsMatch(trimmed, @"^(.)\1{4,}$") || (trimmed.Length > 15 && !trimmed.Contains(' ')))
        {
            return new PromptValidationResult(false, "Invalid input detected. Please provide a clear wealth management directive (e.g., 'Rebalance portfolio, minimize capital gains taxes').");
        }

        // 2. Fast keyword intent check for domain relevance
        // A single generic word (e.g., just "trade", "buy", "sell", "stock") is too vague without context or constraints
        var words = trimmed.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 1 && Regex.IsMatch(words[0], @"^(trade|trades|trading|buy|sell|order|stock|stocks|bond|bonds|cash)$", RegexOptions.IgnoreCase))
        {
            return new PromptValidationResult(false, 
                $"The instruction '{trimmed}' is too ambiguous. Please specify what you would like to do (e.g., 'Rebalance portfolio to target model', 'Trim equities, cap taxes at $2,000', or 'Buy bonds with excess cash').");
        }

        var wealthKeywordsRegex = @"\b(rebalance|realign|drift|portfolio|tax|taxes|gain|gains|budget|stocks?|equit(y|ies)|bonds?|fixed\s*income|asset|allocation|tolerance|trim|harvest|harvesting|model|60/40|70/30|80/20|shares?|liquidate|cash)\b";
        bool hasDomainKeywords = Regex.IsMatch(trimmed, wealthKeywordsRegex, RegexOptions.IgnoreCase);

        // 3. Resolve AI provider settings
        var selectedProvider = !string.IsNullOrWhiteSpace(provider) 
            ? provider 
            : _config["AiSettings:Provider"] ?? "Offline";

        var key = !string.IsNullOrWhiteSpace(apiKey) 
            ? apiKey 
            : _config["AiSettings:ApiKey"];

        // If offline or no key, rely on deterministic domain intent check
        if (selectedProvider.Equals("Offline", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(key))
        {
            if (hasDomainKeywords)
            {
                return new PromptValidationResult(true, null);
            }

            return new PromptValidationResult(false, 
                "Input is not recognized as a valid portfolio rebalancing instruction. Please specify targets, tax constraints, or drift corrections (e.g., 'Rebalance portfolio, cap taxes at $2,000').");
        }

        // 4. Use LLM to validate user intent and catch unrelated queries / garbage
        try
        {
            var kernelBuilder = Kernel.CreateBuilder();

            if (selectedProvider.Equals("Groq", StringComparison.OrdinalIgnoreCase))
            {
                var model = !string.IsNullOrWhiteSpace(modelId) ? modelId : (_config["AiSettings:Groq:ModelId"] ?? "llama-3.3-70b-versatile");
                var endpoint = new Uri(_config["AiSettings:Groq:Endpoint"] ?? "https://api.groq.com/openai/v1");
                var client = new OpenAIClient(new ApiKeyCredential(key), new OpenAIClientOptions { Endpoint = endpoint });
                kernelBuilder.AddOpenAIChatCompletion(model, client);
            }
            else if (selectedProvider.Equals("Google", StringComparison.OrdinalIgnoreCase))
            {
                var model = !string.IsNullOrWhiteSpace(modelId) ? modelId : (_config["AiSettings:Google:ModelId"] ?? "gemini-1.5-flash");
                var endpoint = new Uri(_config["AiSettings:Google:Endpoint"] ?? "https://generativelanguage.googleapis.com/v1beta/openai/");
                var client = new OpenAIClient(new ApiKeyCredential(key), new OpenAIClientOptions { Endpoint = endpoint });
                kernelBuilder.AddOpenAIChatCompletion(model, client);
            }
            else if (selectedProvider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
            {
                var model = !string.IsNullOrWhiteSpace(modelId) ? modelId : (_config["AiSettings:OpenAI:ModelId"] ?? "gpt-4o-mini");
                kernelBuilder.AddOpenAIChatCompletion(model, key);
            }
            else if (selectedProvider.Equals("Grok", StringComparison.OrdinalIgnoreCase))
            {
                var model = !string.IsNullOrWhiteSpace(modelId) ? modelId : (_config["AiSettings:Grok:ModelId"] ?? "grok-beta");
                var endpoint = new Uri(_config["AiSettings:Grok:Endpoint"] ?? "https://api.x.ai/v1");
                var client = new OpenAIClient(new ApiKeyCredential(key), new OpenAIClientOptions { Endpoint = endpoint });
                kernelBuilder.AddOpenAIChatCompletion(model, client);
            }
            else
            {
                return hasDomainKeywords 
                    ? new PromptValidationResult(true, null) 
                    : new PromptValidationResult(false, "Unrecognized instruction. Please enter a valid rebalance prompt.");
            }

            var kernel = kernelBuilder.Build();
            var chat = kernel.GetRequiredService<IChatCompletionService>();

            var classificationHistory = new ChatHistory();
            classificationHistory.AddSystemMessage(@"You are an AI guardrail validator for an FiduciaryAdvisor RebalancePilot portfolio rebalancing system.
Your job is to determine whether an advisor's prompt is a VALID instruction related to financial portfolio management, trading, rebalancing, tax budgets, or asset allocation.
If the prompt is nonsense, gibberish (e.g. 'asdfghjk', '123123123'), chit-chat, recipes, poems, or unrelated topics (e.g. 'what is the weather today', 'tell me a joke', 'buy a pizza'), reply:
INVALID: <brief explanation of why the input is invalid and what to provide instead>
If the prompt is relevant to portfolio rebalancing, trading, tax management, or asset allocation, reply simply:
VALID");

            classificationHistory.AddUserMessage($"Advisor prompt: \"{trimmed}\"");
            var evalResponse = await chat.GetChatMessageContentAsync(classificationHistory, cancellationToken: cancellationToken);
            var replyText = evalResponse.Content?.Trim() ?? "";

            if (replyText.StartsWith("VALID", StringComparison.OrdinalIgnoreCase))
            {
                return new PromptValidationResult(true, null);
            }

            var reason = replyText.StartsWith("INVALID:", StringComparison.OrdinalIgnoreCase) 
                ? replyText.Substring("INVALID:".Length).Trim() 
                : "The instruction provided is unrelated to portfolio management or rebalancing. Please enter a valid portfolio directive (e.g., 'Rebalance portfolio, minimize capital gains taxes').";

            return new PromptValidationResult(false, reason);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SemanticKernel] Prompt validation LLM check failed. Falling back to rule-based keyword validation.");
            if (hasDomainKeywords)
            {
                return new PromptValidationResult(true, null);
            }

            return new PromptValidationResult(false, "The input does not appear to be a valid portfolio or rebalancing directive. Please enter a valid instruction such as 'Rebalance portfolio, minimize taxes'.");
        }
    }

    public async Task<AdvisorRebalanceIntent> ExtractAdvisorIntentAsync(
        string prompt,
        Account account,
        string? provider = null,
        string? apiKey = null,
        string? modelId = null,
        CancellationToken cancellationToken = default)
    {
        var positions = account.Positions.ToList();
        var selectedProvider = !string.IsNullOrWhiteSpace(provider) 
            ? provider 
            : _config["AiSettings:Provider"] ?? "Offline";

        var key = !string.IsNullOrWhiteSpace(apiKey) 
            ? apiKey 
            : _config["AiSettings:ApiKey"];

        // If offline or no key available, run deterministic fallback extraction
        if (selectedProvider.Equals("Offline", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(key))
        {
            return ExtractDeterministicIntent(prompt, positions);
        }

        try
        {
            var kernelBuilder = Kernel.CreateBuilder();
            if (selectedProvider.Equals("Groq", StringComparison.OrdinalIgnoreCase))
            {
                var model = !string.IsNullOrWhiteSpace(modelId) ? modelId : (_config["AiSettings:Groq:ModelId"] ?? "llama-3.3-70b-versatile");
                var endpoint = new Uri(_config["AiSettings:Groq:Endpoint"] ?? "https://api.groq.com/openai/v1");
                var client = new OpenAIClient(new ApiKeyCredential(key), new OpenAIClientOptions { Endpoint = endpoint });
                kernelBuilder.AddOpenAIChatCompletion(model, client);
            }
            else if (selectedProvider.Equals("Google", StringComparison.OrdinalIgnoreCase))
            {
                var model = !string.IsNullOrWhiteSpace(modelId) ? modelId : (_config["AiSettings:Google:ModelId"] ?? "gemini-1.5-flash");
                var endpoint = new Uri(_config["AiSettings:Google:Endpoint"] ?? "https://generativelanguage.googleapis.com/v1beta/openai/");
                var client = new OpenAIClient(new ApiKeyCredential(key), new OpenAIClientOptions { Endpoint = endpoint });
                kernelBuilder.AddOpenAIChatCompletion(model, client);
            }
            else if (selectedProvider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
            {
                var model = !string.IsNullOrWhiteSpace(modelId) ? modelId : (_config["AiSettings:OpenAI:ModelId"] ?? "gpt-4o-mini");
                kernelBuilder.AddOpenAIChatCompletion(model, key);
            }
            else if (selectedProvider.Equals("Grok", StringComparison.OrdinalIgnoreCase))
            {
                var model = !string.IsNullOrWhiteSpace(modelId) ? modelId : (_config["AiSettings:Grok:ModelId"] ?? "grok-beta");
                var endpoint = new Uri(_config["AiSettings:Grok:Endpoint"] ?? "https://api.x.ai/v1");
                var client = new OpenAIClient(new ApiKeyCredential(key), new OpenAIClientOptions { Endpoint = endpoint });
                kernelBuilder.AddOpenAIChatCompletion(model, client);
            }
            else
            {
                return ExtractDeterministicIntent(prompt, positions);
            }

            var kernel = kernelBuilder.Build();
            var chat = kernel.GetRequiredService<IChatCompletionService>();

            var holdingsSummary = string.Join("\n", positions.Select(p => 
                $"- Symbol: {p.Symbol}, Name: {p.SecurityName}, Shares: {p.Shares}, Price: ${p.CurrentMarketPrice}, Class: {p.AssetClass}"));

            var systemPrompt = @"You are an expert wealth management trading intent extractor for FiduciaryAdvisor RebalancePilot.
Extract the advisor's rebalancing directives into a strict JSON contract.
Do NOT output markdown backticks or any explanation. Output ONLY valid raw JSON with this exact schema:
{
  ""strategyMode"": ""FullModelRebalance"" | ""CustomTradesOnly"" | ""RaiseCash"",
  ""maxCapitalGainsBudget"": number | null,
  ""disregardTaxBudget"": boolean,
  ""excludedSymbols"": [""TICKER1"", ""TICKER2""],
  ""specificTrades"": [
    {
      ""action"": ""Buy"" | ""Sell"",
      ""symbol"": ""TICKER"",
      ""amountType"": ""Shares"" | ""Percent"" | ""Dollars"",
      ""amount"": number
    }
  ],
  ""summaryRationale"": ""Brief summary of what the advisor requested""
}

Rules:
1. 'ignore non-energy' or 'only sell energy' means protect/exclude all other equity tickers except energy (XOM, NEE).
2. 'don't sell AI' or 'hold AI' means exclude AI tickers (NVDA, MSFT).
3. 'retrieve X shares' or 'acquire X shares' means Buy. 'get rid of', 'dump', 'trim', 'sell' means Sell.
4. 'sell 10% on AI' means for each AI ticker owned (NVDA, MSFT), create a trade order with amountType='Percent' and amount=10.
5. If prompt mentions a tax cap like '$1,500' or 'cap gains at 1500', set maxCapitalGainsBudget: 1500.";

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage($@"Client Portfolio Holdings:
{holdingsSummary}

Advisor Prompt: ""{prompt}""");

            var response = await chat.GetChatMessageContentAsync(chatHistory, cancellationToken: cancellationToken);
            var jsonText = response.Content?.Trim() ?? "";

            // Strip markdown block if wrapped
            if (jsonText.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                jsonText = jsonText.Substring(7);
            }
            if (jsonText.StartsWith("```"))
            {
                jsonText = jsonText.Substring(3);
            }
            if (jsonText.EndsWith("```"))
            {
                jsonText = jsonText.Substring(0, jsonText.Length - 3);
            }
            jsonText = jsonText.Trim();

            var parsedIntent = System.Text.Json.JsonSerializer.Deserialize<AdvisorRebalanceIntent>(jsonText, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsedIntent != null)
            {
                // Verify all specific trade symbols exist
                parsedIntent.SpecificTrades = parsedIntent.SpecificTrades
                    .Where(t => positions.Any(p => p.Symbol.Equals(t.Symbol, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                return parsedIntent;
            }

            return ExtractDeterministicIntent(prompt, positions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SemanticKernel] Structured Intent Extraction failed. Falling back to deterministic rules.");
            return ExtractDeterministicIntent(prompt, positions);
        }
    }

    private AdvisorRebalanceIntent ExtractDeterministicIntent(string prompt, List<Position> positions)
    {
        var intent = new AdvisorRebalanceIntent();
        intent.MaxCapitalGainsBudget = ExtractCapitalGainsBudget(prompt);
        intent.ExcludedSymbols = ParseExcludedSymbols(prompt, positions);

        var specificTrades = ParseSpecificTrades(prompt, positions);
        foreach (var trade in specificTrades)
        {
            intent.SpecificTrades.Add(new IntentTradeOrder(
                trade.Action,
                trade.Symbol,
                trade.AmountType,
                trade.RawAmount ?? trade.Shares
            ));
        }

        if (intent.SpecificTrades.Count > 0 && !Regex.IsMatch(prompt, @"rebalance|realign|drift|full", RegexOptions.IgnoreCase))
        {
            intent.StrategyMode = RebalanceStrategyMode.CustomTradesOnly;
        }

        intent.SummaryRationale = $"Extracted {intent.SpecificTrades.Count} specific trade(s), {intent.ExcludedSymbols.Count} excluded symbol(s), tax cap: {intent.MaxCapitalGainsBudget?.ToString("C") ?? "Uncapped"}";
        return intent;
    }

    public List<SpecificTradeInstruction> ParseSpecificTrades(string prompt, IEnumerable<Position> positions)
    {
        var result = new List<SpecificTradeInstruction>();
        if (string.IsNullOrWhiteSpace(prompt)) return result;

        var posList = positions.ToList();

        // 1. Check for Percentage Directives: "sell 10% on ai related stocks", "trim 20% of MSFT"
        var pctPattern = @"(?<action>buy|purchase|acquire|add|retrieve|take|pick\s+up|sell|trim|liquidate|dispose\s+of|get\s+rid\s+of|dump|cut|offload)?\s*(?:(?:and|,)\s*)?(?<pct>\d+(?:\.\d+)?)%\s*(?:of|on|in)?\s*(?:the\s+)?(?:stocks?\s+related\s+to\s+|shares?\s+related\s+to\s+)?(?<target>[a-zA-Z0-9\s]+?)(?:,|\.|$|and\s+|for\s+)";
        var pctMatches = Regex.Matches(prompt, pctPattern, RegexOptions.IgnoreCase);
        foreach (Match match in pctMatches)
        {
            var actionStr = match.Groups["action"].Value.Trim().ToLower();
            TradeAction action = (actionStr.Contains("buy") || actionStr.Contains("acquire") || actionStr.Contains("retrieve") || actionStr.Contains("add"))
                ? TradeAction.Buy
                : TradeAction.Sell;

            var pctStr = match.Groups["pct"].Value;
            var targetStr = match.Groups["target"].Value.Trim();
            if (!decimal.TryParse(pctStr, out var pct) || pct <= 0) continue;

            // Check if target is a theme (e.g., "ai", "energy")
            var matchedSymbols = ResolveTargetSymbols(targetStr, posList);
            foreach (var sym in matchedSymbols)
            {
                var pos = posList.FirstOrDefault(p => p.Symbol.Equals(sym, StringComparison.OrdinalIgnoreCase));
                if (pos == null) continue;
                var calculatedShares = Math.Round(pos.Shares * (pct / 100m), 2);
                if (calculatedShares > 0 && !result.Any(r => r.Symbol.Equals(pos.Symbol, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Add(new SpecificTradeInstruction(action, pos.Symbol, calculatedShares, TradeAmountType.Percent, pct));
                }
            }
        }

        if (result.Count > 0) return result;

        // 2. Exact Shares / Dollars Pattern with comprehensive Buy & Sell Synonyms:
        // "retrieve 10 shares of MSFT and get rid of 5 shares on bond", "dump 10 AAPL"
        var tradePattern = @"(?<action>buy|purchase|acquire|add|retrieve|take|pick\s+up|sell|trim|liquidate|dispose\s+of|get\s+rid\s+of|dump|cut|offload)?\s*(?:(?:and|,)\s*)?(?<shares>\d+(?:\.\d+)?)\s*(?:shares?(?:\s+of|\s+on|\s+in)?)?\s+(?<target>[a-zA-Z0-9]+)";

        TradeAction currentAction = TradeAction.Sell;
        var matches = Regex.Matches(prompt, tradePattern, RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            var actionGroup = match.Groups["action"];
            if (actionGroup.Success && !string.IsNullOrWhiteSpace(actionGroup.Value))
            {
                var act = actionGroup.Value.Trim().ToLower();
                currentAction = (act.Contains("buy") || act.Contains("purchase") || act.Contains("acquire") || act.Contains("retrieve") || act.Contains("add") || act.Contains("take"))
                    ? TradeAction.Buy 
                    : TradeAction.Sell;
            }

            var sharesStr = match.Groups["shares"].Value;
            var targetStr = match.Groups["target"].Value;

            if (!decimal.TryParse(sharesStr, out var shares) || shares <= 0) continue;

            var matchedPos = posList.FirstOrDefault(p => 
                p.Symbol.Equals(targetStr, StringComparison.OrdinalIgnoreCase) ||
                p.SecurityName.Contains(targetStr, StringComparison.OrdinalIgnoreCase) ||
                (targetStr.Equals("apple", StringComparison.OrdinalIgnoreCase) && p.Symbol.Equals("AAPL", StringComparison.OrdinalIgnoreCase)) ||
                (targetStr.Equals("microsoft", StringComparison.OrdinalIgnoreCase) && p.Symbol.Equals("MSFT", StringComparison.OrdinalIgnoreCase)) ||
                (targetStr.Equals("nvidia", StringComparison.OrdinalIgnoreCase) && p.Symbol.Equals("NVDA", StringComparison.OrdinalIgnoreCase)) ||
                (targetStr.Equals("bond", StringComparison.OrdinalIgnoreCase) && p.Symbol.Equals("BND", StringComparison.OrdinalIgnoreCase)) ||
                (targetStr.Equals("bonds", StringComparison.OrdinalIgnoreCase) && p.Symbol.Equals("BND", StringComparison.OrdinalIgnoreCase))
            );

            if (matchedPos != null)
            {
                if (!result.Any(r => r.Symbol.Equals(matchedPos.Symbol, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Add(new SpecificTradeInstruction(currentAction, matchedPos.Symbol, shares, TradeAmountType.Shares, shares));
                }
            }
        }

        return result;
    }

    private List<string> ResolveTargetSymbols(string target, List<Position> positions)
    {
        var symbols = new List<string>();
        var cleanTarget = target.ToLower().Trim();

        // 1. Direct symbol / company match
        var directPos = positions.FirstOrDefault(p => 
            p.Symbol.Equals(cleanTarget, StringComparison.OrdinalIgnoreCase) ||
            p.SecurityName.Contains(cleanTarget, StringComparison.OrdinalIgnoreCase) ||
            (cleanTarget == "ai" && (p.Symbol == "NVDA" || p.Symbol == "MSFT"))
        );
        if (directPos != null)
        {
            symbols.Add(directPos.Symbol);
        }

        // 2. Sector thematic mappings
        var thematicMappings = GetThematicMappings();
        foreach (var kvp in thematicMappings)
        {
            if (cleanTarget.Contains(kvp.Key))
            {
                foreach (var sym in kvp.Value)
                {
                    if (positions.Any(p => p.Symbol.Equals(sym, StringComparison.OrdinalIgnoreCase)) &&
                        !symbols.Contains(sym, StringComparer.OrdinalIgnoreCase))
                    {
                        symbols.Add(sym);
                    }
                }
            }
        }

        return symbols;
    }

    public List<string> ParseExcludedSymbols(string prompt, IEnumerable<Position> positions)
    {
        var excluded = new List<string>();
        if (string.IsNullOrWhiteSpace(prompt)) return excluded;

        var posList = positions.ToList();
        var thematicMappings = GetThematicMappings();

        // 1. Inverted/Negated Thematic Pattern: "ignore non-energy related stocks", "exclude non-AI", "only sell energy"
        var invertedPattern = @"(?:(?:don'?t\s+sell|do\s+not\s+sell|exclude|ignore|skip|hold|keep)\s+(?:non[-\s]|other\s+than\s+|except\s+)(?<theme>[a-zA-Z\s]+?)|(?:only\s+sell|only\s+trim|touch\s+only)\s+(?<theme>[a-zA-Z\s]+?))(?:stocks?|shares?|companies)?(?:,|\.|$|do\s+the|for\s+the|and)";
        var invertedMatch = Regex.Match(prompt, invertedPattern, RegexOptions.IgnoreCase);
        if (invertedMatch.Success)
        {
            var rawTheme = invertedMatch.Groups["theme"].Value.Trim().ToLower();
            var allowedSymbols = new List<string>();
            foreach (var kvp in thematicMappings)
            {
                if (rawTheme.Contains(kvp.Key))
                {
                    allowedSymbols.AddRange(kvp.Value);
                }
            }

            if (allowedSymbols.Count > 0)
            {
                // Exclude every equity that is NOT in the allowed set
                foreach (var pos in posList.Where(p => p.AssetClass == AssetClass.Equities))
                {
                    if (!allowedSymbols.Contains(pos.Symbol, StringComparer.OrdinalIgnoreCase) &&
                        !excluded.Contains(pos.Symbol, StringComparer.OrdinalIgnoreCase))
                    {
                        excluded.Add(pos.Symbol);
                    }
                }
                return excluded;
            }
        }

        // 2. Literal symbol/company exclusion patterns: "don't sell X", "ignore X", "get rid of X", "hold X"
        var excludePatterns = new[]
        {
            @"(?:don'?t\s+sell|do\s+not\s+sell|never\s+sell|no\s+selling\s+of|exclude|ignore|skip|hold|keep|protect|lock|omit)\s+(?:shares?\s+(?:of|on|in)\s+)?(?<target>[a-zA-Z0-9]+)",
            @"(?<target>[a-zA-Z0-9]+)\s+(?:should\s+not\s+be\s+sold|is\s+locked|is\s+protected|do\s+not\s+sell|don'?t\s+sell|ignore|skip)"
        };

        foreach (var pattern in excludePatterns)
        {
            var matches = Regex.Matches(prompt, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                var targetStr = match.Groups["target"].Value;
                var matchedPos = posList.FirstOrDefault(p =>
                    p.Symbol.Equals(targetStr, StringComparison.OrdinalIgnoreCase) ||
                    p.SecurityName.Contains(targetStr, StringComparison.OrdinalIgnoreCase) ||
                    (targetStr.Equals("apple", StringComparison.OrdinalIgnoreCase) && p.Symbol.Equals("AAPL", StringComparison.OrdinalIgnoreCase)) ||
                    (targetStr.Equals("microsoft", StringComparison.OrdinalIgnoreCase) && p.Symbol.Equals("MSFT", StringComparison.OrdinalIgnoreCase)) ||
                    (targetStr.Equals("nvidia", StringComparison.OrdinalIgnoreCase) && p.Symbol.Equals("NVDA", StringComparison.OrdinalIgnoreCase)) ||
                    (targetStr.Equals("bonds", StringComparison.OrdinalIgnoreCase) && p.Symbol.Equals("BND", StringComparison.OrdinalIgnoreCase))
                );

                if (matchedPos != null && !excluded.Contains(matchedPos.Symbol, StringComparer.OrdinalIgnoreCase))
                {
                    excluded.Add(matchedPos.Symbol);
                }
            }
        }

        // 3. Regular Thematic / Sector Exclusion Detection: "don't touch the stock related to AI", "exclude healthcare", "hold energy"
        var thematicPattern = @"(?:don'?t\s+(?:sell|touch)|do\s+not\s+(?:sell|touch)|never\s+sell|exclude|ignore|skip|hold|keep|protect|lock)\s+(?:any\s+|the\s+)?(?:stocks?|shares?|assets?|companies)?\s*(?:related\s+to|in)?\s*(?<theme>[a-zA-Z\s]+?)(?:,|\.|$|do\s+the|for\s+the|and)";
        var thematicMatch = Regex.Match(prompt, thematicPattern, RegexOptions.IgnoreCase);
        if (thematicMatch.Success)
        {
            var rawTheme = thematicMatch.Groups["theme"].Value.Trim().ToLower();
            foreach (var kvp in thematicMappings)
            {
                if (rawTheme.Contains(kvp.Key))
                {
                    foreach (var sym in kvp.Value)
                    {
                        if (posList.Any(p => p.Symbol.Equals(sym, StringComparison.OrdinalIgnoreCase)) &&
                            !excluded.Contains(sym, StringComparer.OrdinalIgnoreCase))
                        {
                            excluded.Add(sym);
                        }
                    }
                }
            }
        }

        return excluded;
    }

    private static Dictionary<string, string[]> GetThematicMappings() => new()
    {
        ["ai"] = new[] { "NVDA", "MSFT" },
        ["artificial intelligence"] = new[] { "NVDA", "MSFT" },
        ["tech"] = new[] { "NVDA", "MSFT" },
        ["technology"] = new[] { "NVDA", "MSFT" },
        ["semiconductor"] = new[] { "NVDA" },
        ["healthcare"] = new[] { "LLY", "JNJ" },
        ["pharma"] = new[] { "LLY", "JNJ" },
        ["pharmaceutical"] = new[] { "LLY", "JNJ" },
        ["financial"] = new[] { "JPM", "V" },
        ["financials"] = new[] { "JPM", "V" },
        ["bank"] = new[] { "JPM" },
        ["banking"] = new[] { "JPM" },
        ["energy"] = new[] { "XOM", "NEE" },
        ["oil"] = new[] { "XOM" },
        ["clean energy"] = new[] { "NEE" },
        ["renewables"] = new[] { "NEE" },
        ["consumer"] = new[] { "WMT", "PG" },
        ["retail"] = new[] { "WMT" }
    };

    public decimal? ExtractCapitalGainsBudget(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return null;

        // If prompt explicitly asks for regardless of tax or unconstrained, return null (uncapped)
        if (Regex.IsMatch(prompt, @"regardless of tax|no budget|unconstrained|ignore tax(?:es)?", RegexOptions.IgnoreCase))
        {
            return null;
        }

        // 1. Explicit dollar sign: $2,000, $1500
        var dollarMatch = Regex.Match(prompt, @"\$([0-9]{1,3}(?:,[0-9]{3})*(?:\.[0-9]{2})?|\d+)", RegexOptions.IgnoreCase);
        if (dollarMatch.Success)
        {
            var rawValue = dollarMatch.Groups[1].Value.Replace(",", "");
            if (decimal.TryParse(rawValue, out var parsed) && parsed >= 100)
            {
                return parsed;
            }
        }

        // 2. Keyword with budget/tax/cap: e.g. "budget 2000", "tax cap 1500"
        var budgetMatch = Regex.Match(prompt, @"(?:budget|tax|cap|gains?)\s*(?:of|is|at|:)?\s*\$?([0-9]{1,3}(?:,[0-9]{3})*(?:\.[0-9]{2})?|\d+)", RegexOptions.IgnoreCase);
        if (budgetMatch.Success)
        {
            var rawValue = budgetMatch.Groups[1].Value.Replace(",", "");
            if (decimal.TryParse(rawValue, out var parsed) && parsed >= 100)
            {
                return parsed;
            }
        }

        return null;
    }

    public async Task<string> GenerateClientEmailAsync(
        Account account, 
        TradeProposal proposal, 
        PortfolioDriftDto drift,
        string? provider = null,
        string? apiKey = null,
        string? modelId = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Resolve Provider Settings
        var selectedProvider = !string.IsNullOrWhiteSpace(provider) 
            ? provider 
            : _config["AiSettings:Provider"] ?? "Offline";

        var key = !string.IsNullOrWhiteSpace(apiKey) 
            ? apiKey 
            : _config["AiSettings:ApiKey"];

        if (selectedProvider.Equals("Offline", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(key))
        {
            _logger.LogInformation("[Co-Pilot] Using Deterministic Offline Engine (Provider: {Provider})", selectedProvider);
            return GenerateDeterministicEmail(account, proposal, drift, selectedProvider);
        }

        // 2. Invoke Microsoft Semantic Kernel with selected LLM
        try
        {
            _logger.LogInformation("[SemanticKernel] Initializing provider: {Provider}", selectedProvider);
            var kernelBuilder = Kernel.CreateBuilder();

            if (selectedProvider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
            {
                var model = !string.IsNullOrWhiteSpace(modelId) ? modelId : (_config["AiSettings:OpenAI:ModelId"] ?? "gpt-4o-mini");
                kernelBuilder.AddOpenAIChatCompletion(model, key);
            }
            else if (selectedProvider.Equals("Google", StringComparison.OrdinalIgnoreCase))
            {
                var model = !string.IsNullOrWhiteSpace(modelId) ? modelId : (_config["AiSettings:Google:ModelId"] ?? "gemini-1.5-flash");
                var endpoint = new Uri(_config["AiSettings:Google:Endpoint"] ?? "https://generativelanguage.googleapis.com/v1beta/openai/");
                var client = new OpenAIClient(new ApiKeyCredential(key), new OpenAIClientOptions { Endpoint = endpoint });
                kernelBuilder.AddOpenAIChatCompletion(model, client);
            }
            else if (selectedProvider.Equals("Grok", StringComparison.OrdinalIgnoreCase))
            {
                var model = !string.IsNullOrWhiteSpace(modelId) ? modelId : (_config["AiSettings:Grok:ModelId"] ?? "grok-beta");
                var endpoint = new Uri(_config["AiSettings:Grok:Endpoint"] ?? "https://api.x.ai/v1");
                var client = new OpenAIClient(new ApiKeyCredential(key), new OpenAIClientOptions { Endpoint = endpoint });
                kernelBuilder.AddOpenAIChatCompletion(model, client);
            }
            else if (selectedProvider.Equals("Groq", StringComparison.OrdinalIgnoreCase))
            {
                var model = !string.IsNullOrWhiteSpace(modelId) ? modelId : (_config["AiSettings:Groq:ModelId"] ?? "llama-3.3-70b-versatile");
                var endpoint = new Uri(_config["AiSettings:Groq:Endpoint"] ?? "https://api.groq.com/openai/v1");
                var client = new OpenAIClient(new ApiKeyCredential(key), new OpenAIClientOptions { Endpoint = endpoint });
                kernelBuilder.AddOpenAIChatCompletion(model, client);
            }
            else
            {
                return GenerateDeterministicEmail(account, proposal, drift, "Unknown Provider (Fallback)");
            }

            var kernel = kernelBuilder.Build();
            var chat = kernel.GetRequiredService<IChatCompletionService>();

            var sellOrders = proposal.Orders.Where(o => o.Action == Domain.Enums.TradeAction.Sell).Select(o => $"{o.Shares} shares of {o.Symbol}");
            var buyOrders = proposal.Orders.Where(o => o.Action == Domain.Enums.TradeAction.Buy).Select(o => $"{o.Shares} shares of {o.Symbol}");

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("You are an FiduciaryAdvisor RebalancePilot fiduciary wealth management advisor co-pilot. Write professional, empathetic, and compliant client review letters regarding portfolio drift and tax-loss rebalancing.");
            
            var userPrompt = $@"Draft a client portfolio review email for:
Client: {account.ClientName} (Account #{account.AccountNumber} at {account.Custodian})
Starting Allocation: {(drift.CurrentEquitiesPct * 100):F1}% Equities / {(drift.CurrentFixedIncomePct * 100):F1}% Fixed Income
Target Model ({drift.TargetModelName}): {(drift.TargetEquitiesPct * 100):F1}% Equities / {(drift.TargetFixedIncomePct * 100):F1}% Fixed Income
Executed/Proposed Trades:
- Sells: {string.Join(", ", sellOrders)}
- Buys: {string.Join(", ", buyOrders)}
Total Rebalance Volume: ${proposal.TotalTradeVolume:N2}
Estimated Realized Capital Gains: ${proposal.TotalEstimatedCapitalGains:N2}

Requirements:
- Emphasize risk management and fiduciary duty.
- Highlight the tax-sensitive nature of the trade selection.
- Include an appropriate email subject line.
- Sign off as 'Your FiduciaryAdvisor RebalancePilot Advisory Team'.";

            chatHistory.AddUserMessage(userPrompt);

            var reply = await chat.GetChatMessageContentAsync(chatHistory, cancellationToken: cancellationToken);
            return reply.Content ?? GenerateDeterministicEmail(account, proposal, drift, selectedProvider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SemanticKernel] Error invoking {Provider}. Falling back to deterministic engine. Details: {Message}", selectedProvider, ex.ToString());
            return $"[Note: Semantic Kernel ({selectedProvider}) encountered: {ex.Message}. Showing fallback review]\n\n" + 
                   GenerateDeterministicEmail(account, proposal, drift, selectedProvider);
        }
    }

    private string GenerateDeterministicEmail(Account account, TradeProposal proposal, PortfolioDriftDto drift, string providerName)
    {
        var equitiesBeforePct = drift.CurrentEquitiesPct * 100;
        var equitiesTargetPct = drift.TargetEquitiesPct * 100;
        var fixedBeforePct = drift.CurrentFixedIncomePct * 100;
        var fixedTargetPct = drift.TargetFixedIncomePct * 100;

        var sellOrders = proposal.Orders.Where(o => o.Action == Domain.Enums.TradeAction.Sell).ToList();
        var buyOrders = proposal.Orders.Where(o => o.Action == Domain.Enums.TradeAction.Buy).ToList();

        var sellSummary = string.Join(", ", sellOrders.Select(o => $"{o.Shares} shares of {o.Symbol}"));
        var buySummary = string.Join(", ", buyOrders.Select(o => $"{o.Shares} shares of {o.Symbol}"));

        return $@"Subject: Periodic Portfolio Alignment Review for Account #{account.AccountNumber}

Dear {account.ClientName},

During our recent review of your portfolio at {account.Custodian}, we identified that your asset allocation has drifted beyond our agreed risk tolerance guidelines due to strong market performance in equities.

• Current Allocation: {equitiesBeforePct:F1}% Equities / {fixedBeforePct:F1}% Fixed Income
• Target Risk Model ({drift.TargetModelName}): {equitiesTargetPct:F1}% Equities / {fixedTargetPct:F1}% Fixed Income

To safeguard your accumulated gains and restore your portfolio to your designated risk profile, we have formulated a tax-sensitive rebalancing plan:
- Trimming equities ({sellSummary})
- Reinvesting proceeds into fixed income ({buySummary})

Key Highlights of this Rebalance:
1. Total Rebalance Volume: ${proposal.TotalTradeVolume:N2}
2. Estimated Net Capital Gains Realized: ${proposal.TotalEstimatedCapitalGains:N2} (engineered to minimize your tax liability)
3. New Projected Allocation: {equitiesTargetPct:F1}% Equities / {fixedTargetPct:F1}% Fixed Income

Please review these proposed adjustments. As your fiduciary, we execute trades only upon your agreement or per our discretionary mandate.

Warm regards,

Your FiduciaryAdvisor RebalancePilot Advisory Team
(Generated via {providerName} / Semantic Kernel)";
    }
}
