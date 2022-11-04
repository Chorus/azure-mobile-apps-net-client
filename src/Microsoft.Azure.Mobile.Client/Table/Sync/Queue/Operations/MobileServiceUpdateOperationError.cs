using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Sync
{
    public interface IMobileServiceUpdateOperationError
    {
        JObject PreviousItem { get; }
        IEnumerable<PropertyConflict> PropertyConflicts { get; }
        JObject Item { get; }
        JObject Result { get; }
        Task MergeAndUpdateOperationAsync();
    }

    public class MobileServiceUpdateOperationError : MobileServiceTableOperationError, IMobileServiceUpdateOperationError
    {
        /// <summary>
        /// The previous version of the item associated with the operation.
        /// </summary>
        public JObject PreviousItem { get; private set; }

        public IEnumerable<PropertyConflict> PropertyConflicts { get; private set; }

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
        }

        internal override JObject Serialize()
        {
            var item = base.Serialize();
            item["previousItem"] = PreviousItem?.ToString(Formatting.None);
            return item;
        }

        public async Task MergeAndUpdateOperationAsync()
        {

        }

        private IEnumerable<PropertyConflict> GetPropertyConflicts()
        {
            throw new Exception();
        }
    }
}
