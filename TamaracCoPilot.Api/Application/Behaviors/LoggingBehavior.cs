using System.Diagnostics;
using MediatR;

namespace TamaracCoPilot.Api.Application.Behaviors;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("[MediatR] Handling command/query {RequestName}", requestName);

        var response = await next();

        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;

        if (elapsedMs > 250)
        {
            _logger.LogWarning("[MediatR] Long-running request {RequestName} took {ElapsedMs}ms", requestName, elapsedMs);
        }
        else
        {
            _logger.LogInformation("[MediatR] Handled {RequestName} in {ElapsedMs}ms", requestName, elapsedMs);
        }

        return response;
    }
}
