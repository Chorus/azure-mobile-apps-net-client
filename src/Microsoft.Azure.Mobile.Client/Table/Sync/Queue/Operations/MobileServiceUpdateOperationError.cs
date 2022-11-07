﻿#nullable enable
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Sync
{
    public class MobileServiceUpdateOperationError : MobileServiceTableOperationError, IMobileServiceUpdateOperationError
    {
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
                throw new ArgumentException($"Only {nameof(operationKind)}={nameof(MobileServiceTableOperationKind.Update)} is supported", nameof(operationKind));
            }

            PreviousItem = previousItem ?? throw new ArgumentNullException(nameof(previousItem));
            PropertyConflicts = GetPropertyConflicts();

            ImmutableArray<PropertyConflict> GetPropertyConflicts()
            {
                static IEnumerable<string> GetPropertyNames(JObject item) =>
                    MobileServiceSerializer.RemoveSystemProperties(item, out _).Properties().Select(r => r.Name);

                var changes =
                    (from propertyName in
                         GetPropertyNames(BaseItem).Intersect(
                         GetPropertyNames(LocalItem)).Intersect(
                         GetPropertyNames(RemoteItem))
                     let change = new PropertyConflict(propertyName, this)
                     where change.IsLocalChanged || change.IsRemoteChanged
                     select change)
                    .ToImmutableArray();

                return changes.Any(r => r.IsLocalChanged) && changes.Any(r => r.IsRemoteChanged) ?
                    changes :
                    ImmutableArray.Create<PropertyConflict>();
            }
        }

        /// <summary>
        /// The previous version of the item associated with the operation.
        /// </summary>
        public JObject PreviousItem { get; }

        public ImmutableArray<PropertyConflict> PropertyConflicts { get; }

        internal override JObject Serialize()
        {
            var item = base.Serialize();
            item["previousItem"] = PreviousItem?.ToString(Formatting.None);
            return item;
        }

        public async Task MergeAndUpdateOperationAsync()
        {
            if (PropertyConflicts.Any(r => !r.Handled))
            {
                throw new InvalidOperationException("All conflicts must be handled first.");
            }

            var item = RemoteItem;
            foreach (var conflict in PropertyConflicts)
            {
                item[conflict.PropertyName] = conflict.ResolvedValue;
            }

            await UpdateOperationAsync(item);
        }
    }
}