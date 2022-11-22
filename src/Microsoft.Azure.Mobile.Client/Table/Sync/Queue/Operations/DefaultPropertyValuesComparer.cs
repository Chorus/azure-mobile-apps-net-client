#nullable enable
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Sync
{
    public class DefaultPropertyValuesComparer : IPropertyValuesComparer
    {
        public bool AreValuesEqual(string tableName, string propertyName, JValue? value1, JValue? value2) => 
            Equals(value1, value2);
    }
}
