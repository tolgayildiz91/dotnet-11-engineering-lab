namespace ClosedPayments;

#if OPEN
public abstract record class PaymentOutcome;
#else
public closed record class PaymentOutcome;
#endif

public sealed record class Approved(string Receipt) : PaymentOutcome;

public sealed record class Declined(string Code) : PaymentOutcome;

#if INTERMEDIATE
public closed record class ReviewRequired(string Reason) : PaymentOutcome;

public sealed record class ManualReview(string Reason) : ReviewRequired(Reason);

public sealed record class RiskReview(string Reason) : ReviewRequired(Reason);
#else
public record class ReviewRequired(string Reason) : PaymentOutcome;
#endif

#if ADDED
public sealed record class Pending : PaymentOutcome;
#endif
