#nullable enable
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Sync
{
    public interface IPropertyValuesComparer
    {
        bool AreValuesEqual(string tableName, string propertyName, JValue? value1, JValue? value2);
    }
}
