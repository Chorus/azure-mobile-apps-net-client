#nullable enable
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Sync
{
    public class MobileServiceUpdateOperationError : MobileServiceTableOperationError, IMobileServiceUpdateOperationError
    {
        /// <summary>
        /// The previous version of the item associated with the operation.
        /// </summary>
        public JObject PreviousItem { get; }

        public IEnumerable<PropertyConflict> PropertyConflicts { get; private set; }

        private JObject BaseItem => PreviousItem;
        private JObject LocalItem => Item;
        private JObject RemoteItem => Result;

        public MobileServiceUpdateOperationError(
            string id,
            long operationVersion,
            MobileServiceTableOperationKind operationKind,
            HttpStatusCode? status,
            string tableName,
            JObject item,
            JObject previousItem,
            string rawResult,
            JObject result) :
            base(id, operationVersion, operationKind, status, tableName, item, rawResult, result)
        {
            if (operationKind != MobileServiceTableOperationKind.Update)
            {
                throw new ArgumentException($"Only {nameof(MobileServiceTableOperationKind.Update)} is supported", nameof(operationKind));
            }

            PreviousItem = previousItem;
            PropertyConflicts = GetPropertyConflicts().AsEnumerable();
        }

        internal override JObject Serialize()
        {
            var item = base.Serialize();
            item["previousItem"] = PreviousItem?.ToString(Formatting.None);
            return item;
        }

        private PropertyConflict[] GetPropertyConflicts()
        {
            var changes =
                (from propertyName in
                     BaseItem.Properties().Select(r => r.Name).Intersect(
                     LocalItem.Properties().Select(r => r.Name)).Intersect(
                     RemoteItem.Properties().Select(r => r.Name))
                 let change = new PropertyConflict(propertyName, this)
                 where change.IsLocalChanged || change.IsRemoteChanged
                 select change)
                .ToArray();

            return changes.Any(r => r.IsLocalChanged) && changes.Any(r => r.IsRemoteChanged) ?
                changes :
                Array.Empty<PropertyConflict>();
        }

        public async Task MergeAndUpdateOperationAsync()
        {

        }
    }
}
