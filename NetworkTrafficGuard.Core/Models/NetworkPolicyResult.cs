namespace NetworkTrafficGuard.Core.Models;

public sealed record NetworkPolicyResult(
    NetworkRiskLevel RiskLevel,
    string Message,
    bool ShouldNotify,
    bool ShouldBlockSimRoute);
