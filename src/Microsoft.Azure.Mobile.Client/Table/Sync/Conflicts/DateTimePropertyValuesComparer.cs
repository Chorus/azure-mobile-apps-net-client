#nullable enable
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;

namespace Microsoft.WindowsAzure.MobileServices.Sync.Conflicts
{
    public abstract class DateTimePropertyValuesComparer : IPropertyValuesComparer
    {
        private readonly IPropertyValuesComparer _inner;

        public DateTimePropertyValuesComparer(IPropertyValuesComparer inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public bool AreValuesEqual(in string tableName, in string propertyName, JValue? jValue1, JValue? jValue2)
        {
            if (IsDateTime(tableName, propertyName))
            {
                DateTime? value1 = ParseDateTime(jValue1?.Value?.ToString());
                DateTime? value2 = ParseDateTime(jValue2?.Value?.ToString());
                bool equal = value1 == value2;
                return equal;
            }

            return _inner.AreValuesEqual(tableName, propertyName, jValue1, jValue2);

            static DateTime? ParseDateTime(in string? value) => !string.IsNullOrEmpty(value) ?
                DateTime.Parse(value, null, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal) :
                null;
        }

        protected abstract bool IsDateTime(in string tableName, in string propertyName);
    }
}
