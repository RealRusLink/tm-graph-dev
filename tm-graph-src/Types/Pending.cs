namespace tm_graph.Types;

public readonly record struct PendingEmail(string Email, EmailVerificationReason Reason);
public readonly record struct PendingUsername(string Username, EmailVerificationReason Reason);