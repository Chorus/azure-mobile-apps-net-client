#nullable enable
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Sync.Conflicts
{
    public interface IPropertyValuesComparer
    {
        bool AreValuesEqual(in string tableName, in string propertyName, JValue? value1, JValue? value2);
    }
}
