#nullable enable
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Sync.Conflicts
{
    public class DefaultPropertyValuesComparer : IPropertyValuesComparer
    {
        public bool AreValuesEqual(in string tableName, in string propertyName, JValue? value1, JValue? value2) =>
            Equals(value1, value2);
    }
}
