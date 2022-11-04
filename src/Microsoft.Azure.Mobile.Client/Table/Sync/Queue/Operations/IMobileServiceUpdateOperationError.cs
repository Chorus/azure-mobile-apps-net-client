#nullable enable
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Sync
{
    public interface IMobileServiceUpdateOperationError
    {
        IEnumerable<PropertyConflict> PropertyConflicts { get; }
        /// <summary>
        /// The base item before the update
        /// </summary>
        JObject PreviousItem { get; }
        /// <summary>
        /// The updated local item
        /// </summary>
        JObject Item { get; }
        /// <summary>
        /// The remote item
        /// </summary>
        JObject Result { get; }
        Task MergeAndUpdateOperationAsync();
    }
}
