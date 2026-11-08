using ClosedPayments;

namespace ForeignPayments;

#if DIRECT
public sealed record class ForeignOutcome : PaymentOutcome;
#elif INDIRECT
public sealed record class ForeignReview() : ReviewRequired("synthetic");
#elif INTERMEDIATE
public sealed record class ForeignReview() : ReviewRequired("synthetic");
#endif
