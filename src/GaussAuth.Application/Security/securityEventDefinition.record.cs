namespace GaussAuth.Application.Security;

public sealed record SecurityEventDefinition(string EventType, SecurityEventOutcome Outcome, SecurityEventReliability Reliability);
