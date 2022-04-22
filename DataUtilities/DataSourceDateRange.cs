using System;

namespace DataUtilities
{
    public class DataSourceDateRange
    {
        public DateTimeOffset? From { get; set;  }
        public DateTimeOffset? To { get; set; }

        public DataSourceDateRange(DateTimeOffset? from, DateTimeOffset? to)
        {
            From = from;
            To = to;
        }

        public override string ToString()
        {
            return String.Join(" ", new[] { From.HasValue ? $"From {From}" : null, To.HasValue ? $"To {To}" : null });
        }
    }
}
