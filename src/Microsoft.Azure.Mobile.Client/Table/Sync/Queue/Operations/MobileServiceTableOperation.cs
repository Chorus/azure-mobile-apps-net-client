// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Sync
{
    internal abstract class MobileServiceTableOperation<T> : IMobileServiceTableOperation<T>, ITable
        where T : ITable
    {
        // --- Persisted properties -- //
        public string Id { get; private set; }

        public abstract MobileServiceTableOperationKind Kind { get; }

        public MobileServiceTableKind TableKind { get; private set; }

        public string TableName { get; private set; }

        public string ItemId { get; private set; }

        public T Item { get; set; }

        public MobileServiceTableOperationState State { get; internal set; }

        public long Sequence { get; set; }

        public long Version { get; set; }

        // --- Non persisted properties -- //
        IMobileServiceTable<T> IMobileServiceTableOperation<T>.Table => Table;

        public MobileServiceTable<T> Table { get; set; }

        public bool IsCancelled { get; private set; }

        public bool IsUpdated { get; private set; }

        public virtual bool CanWriteResultToStore => true;

        protected virtual bool SerializeItemToQueue => false;

        protected MobileServiceTableOperation(string tableName, MobileServiceTableKind tableKind, string itemId)
        {
            Id = Guid.NewGuid().ToString();
            State = MobileServiceTableOperationState.Pending;
            TableKind = tableKind;
            TableName = tableName;
            ItemId = itemId;
            Version = 1;
        }

        public void AbortPush() => throw new MobileServicePushAbortException();

        public async Task<T> ExecuteAsync()
        {
            if (IsCancelled)
            {
                return default;
            }

            if (Item == null)
            {
                throw new MobileServiceInvalidOperationException("Operation must have an item associated with it.", request: null, response: null);
            }

            var response = await OnExecuteAsync();
            if (response == null)
            {
                throw new MobileServiceInvalidOperationException("Mobile Service table operation returned an unexpected response.", request: null, response: null);
            }

            return response;
        }

        protected abstract Task<T> OnExecuteAsync();

        internal void Cancel()
        {
            IsCancelled = true;
        }

        internal void Update()
        {
            Version++;
            IsUpdated = true;
        }

        /// <summary>
        /// Execute the operation on sync store
        /// </summary>
        /// <param name="store">Sync store</param>
        /// <param name="item">The item to use for store operation</param>
        public abstract Task ExecuteLocalAsync(IMobileServiceLocalStore store, T item);

        /// <summary>
        /// Validates that the operation can collapse with the late operation
        /// </summary>
        /// <exception cref="InvalidOperationException">This method throws when the operation cannot collapse with new operation.</exception>
        public abstract void Validate(MobileServiceTableOperation<T> newOperation);

        /// <summary>
        /// Collapse this operation with the late operation by cancellation of either operation.
        /// </summary>
        public abstract void Collapse(MobileServiceTableOperation<T> newOperation);

        /// <summary>
        /// Defines the the table for storing operations
        /// </summary>
        /// <param name="store">An instance of <see cref="IMobileServiceLocalStore"/></param>
        internal static void DefineTable(MobileServiceLocalStore store)
        {
            store.DefineTable(MobileServiceLocalSystemTables.OperationQueue, new JObject()
            {
                { MobileServiceSystemColumns.Id, string.Empty },
                { "kind", 0 },
                { "state", 0 },
                { "tableName", string.Empty },
                { "tableKind", 0 },
                { "itemId", string.Empty },
                { "item", string.Empty },
                { MobileServiceSystemColumns.CreatedAt, DateTime.Now },
                { "sequence", 0 },
                { "version", 0 }
            });
        }

        internal JObject Serialize()
        {
            var obj = new JObject()
            {
                { MobileServiceSystemColumns.Id, Id },
                { "kind", (int)Kind },
                { "state", (int)State },
                { "tableName", TableName },
                { "tableKind", (int)TableKind },
                { "itemId", ItemId },
                { "item", Item != null && SerializeItemToQueue ? Item.ToString(Formatting.None) : null },
                { "sequence", Sequence },
                { "version", Version }
            };

            return obj;
        }

        internal static MobileServiceTableOperation<T> Deserialize(JObject obj)
        {
            if (obj == null)
            {
                return null;
            }

            var kind = (MobileServiceTableOperationKind)obj.Value<int>("kind");
            string tableName = obj.Value<string>("tableName");
            var tableKind = (MobileServiceTableKind)obj.Value<int?>("tableKind").GetValueOrDefault();
            string itemId = obj.Value<string>("itemId");


            MobileServiceTableOperation<T> operation = null;
            switch (kind)
            {
                case MobileServiceTableOperationKind.Insert:
                    operation = new InsertOperation<T>(tableName, tableKind, itemId);
                    break;
                case MobileServiceTableOperationKind.Update:
                    operation = new UpdateOperation<T>(tableName, tableKind, itemId);
                    break;
                case MobileServiceTableOperationKind.Delete:
                    operation = new DeleteOperation<T>(tableName, tableKind, itemId);
                    break;
            }

            if (operation != null)
            {
                operation.Id = obj.Value<string>(MobileServiceSystemColumns.Id);
                operation.Sequence = obj.Value<long?>("sequence").GetValueOrDefault();
                operation.Version = obj.Value<long?>("version").GetValueOrDefault();
                string itemJson = obj.Value<string>("item");
                operation.Item = !String.IsNullOrEmpty(itemJson) ? JObject.Parse(itemJson) : null;
                operation.State = (MobileServiceTableOperationState)obj.Value<int?>("state").GetValueOrDefault();
            }

            return operation;
        }
    }
}
