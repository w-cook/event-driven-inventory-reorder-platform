using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace InventoryReorderPlatform.SupplierMockApi.Behavior;

public sealed class SupplierBehaviorSimulator
    : ISupplierBehaviorSimulator
{
    private readonly IOptionsMonitor<SupplierBehaviorOptions>
        _optionsMonitor;

    private readonly ConcurrentDictionary<string, int>
        _attemptCounts = new(StringComparer.Ordinal);

    public SupplierBehaviorSimulator(
        IOptionsMonitor<SupplierBehaviorOptions>
            optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;
    }

    public async Task<SupplierBehaviorResult> EvaluateAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;

        switch (options.Mode)
        {
            case SupplierBehaviorMode.Normal:
                return Proceed();

            case SupplierBehaviorMode.Delayed:
                await Task.Delay(
                    options.DelayMilliseconds,
                    cancellationToken);

                return Proceed();

            case SupplierBehaviorMode.TransientFailure:
                var attemptNumber =
                    _attemptCounts.AddOrUpdate(
                        idempotencyKey,
                        addValue: 1,
                        updateValueFactory:
                            (_, currentCount) =>
                                currentCount + 1);

                if (attemptNumber <=
                    options.TransientFailuresBeforeSuccess)
                {
                    return new SupplierBehaviorResult(
                        SupplierBehaviorOutcome
                            .TransientFailure,
                        "The supplier is temporarily unavailable.",
                        attemptNumber);
                }

                return Proceed();

            case SupplierBehaviorMode.PermanentRejection:
                return new SupplierBehaviorResult(
                    SupplierBehaviorOutcome
                        .PermanentRejection,
                    options.PermanentRejectionMessage);

            default:
                throw new InvalidOperationException(
                    $"Unsupported supplier behavior mode " +
                    $"'{options.Mode}'.");
        }
    }

    private static SupplierBehaviorResult Proceed()
    {
        return new SupplierBehaviorResult(
            SupplierBehaviorOutcome.Proceed);
    }
}