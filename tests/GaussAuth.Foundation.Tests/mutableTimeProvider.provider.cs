namespace GaussAuth.Foundation.Tests;

public sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset now = start;

    public MutableTimeProvider() : this(DateTimeOffset.UtcNow) { }

    public void Advance(TimeSpan by) => now += by;

    public override DateTimeOffset GetUtcNow() => now;
}
