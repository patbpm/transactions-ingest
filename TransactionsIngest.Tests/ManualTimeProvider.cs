namespace TransactionsIngest.Tests;

/// <summary>
/// A controllable TimeProvider for tests: lets a test say "pretend it is exactly this moment"
/// instead of depending on the real system clock. This is what makes the 24-hour revocation and
/// finalization windows testable without sleeping a real thread for 24 hours.
/// </summary>
public class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public ManualTimeProvider(DateTimeOffset start) => _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);

    public void SetTo(DateTimeOffset to) => _now = to;
}
