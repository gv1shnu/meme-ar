namespace MemeAR.Events
{
    /// <summary>Monotonic source of unique event/correlation IDs, shared across detectors.</summary>
    public sealed class EventIdGenerator
    {
        private long _next = 1;

        public long Next()
        {
            return _next++;
        }

        public void Reset()
        {
            _next = 1;
        }
    }
}
