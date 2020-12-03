// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MobileServices.Query;
using Microsoft.WindowsAzure.MobileServices.Threading;

namespace Microsoft.WindowsAzure.MobileServices.Sync
{
    /// <summary>
    /// Queue of all operations i.e. Push, Pull, Insert, Update, Delete
    /// </summary>
    internal class OperationQueue
    {
        private readonly AsyncLockDictionary tableLocks = new AsyncLockDictionary();
        private readonly AsyncLockDictionary itemLocks = new AsyncLockDictionary();
        private readonly IMobileServiceLocalStore _store;
        private long sequenceId;
        private long pendingOperations;

        public OperationQueue(IMobileServiceLocalStore store)
        {
            _store = store;
        }

        public async virtual Task<MobileServiceTableOperation<T>> PeekAsync<T>(long prevSequenceId, MobileServiceTableKind tableKind, IEnumerable<string> tableNames)
        {
            MobileServiceTableQueryDescription query = CreateQuery();

            var tableKindNode = Compare(BinaryOperatorKind.Equal, "tableKind", (int)tableKind);
            var sequenceNode = Compare(BinaryOperatorKind.GreaterThan, "sequence", prevSequenceId);

            query.Filter = new BinaryOperatorNode(BinaryOperatorKind.And, tableKindNode, sequenceNode);

            if (tableNames != null && tableNames.Any())
            {
                BinaryOperatorNode nameInList = tableNames.Select(t => Compare(BinaryOperatorKind.Equal, "tableName", t))
                                                          .Aggregate((first, second) => new BinaryOperatorNode(BinaryOperatorKind.Or, first, second));
                query.Filter = new BinaryOperatorNode(BinaryOperatorKind.And, query.Filter, nameInList);
            }

            query.Ordering.Add(new OrderByNode(new MemberAccessNode(null, "sequence"), OrderByDirection.Ascending));
            query.Top = 1;
            var op = await _store.FirstOrDefault(query);
            if (op == null)
            {
                return null;
            }

            return MobileServiceTableOperation<T>.Deserialize(op);
        }

        public long PendingOperations => pendingOperations;

        internal void UpdateOperationCount(long delta)
        {
            long current, updated;
            do
            {
                current = pendingOperations;
                updated = current + delta;
            }
            while (current != Interlocked.CompareExchange(ref pendingOperations, updated, current));
        }

        public virtual async Task<long> CountPending(string tableName)
        {
            MobileServiceTableQueryDescription query = CreateQuery();
            query.Filter = new BinaryOperatorNode(BinaryOperatorKind.Equal, new MemberAccessNode(null, "tableName"), new ConstantNode(tableName));
            return await _store.CountAsync(query);
        }

        public virtual Task<IDisposable> LockTableAsync(string name, CancellationToken cancellationToken)
        {
            return tableLocks.Acquire(name, cancellationToken);
        }

        public Task<IDisposable> LockItemAsync(string id, CancellationToken cancellationToken)
        {
            return itemLocks.Acquire(id, cancellationToken);
        }

        public virtual async Task<MobileServiceTableOperation<T>> GetOperationByItemIdAsync<T>(string tableName, string itemId)
        {
            MobileServiceTableQueryDescription query = CreateQuery();
            query.Filter = new BinaryOperatorNode(BinaryOperatorKind.And,
                                Compare(BinaryOperatorKind.Equal, "tableName", tableName),
                                Compare(BinaryOperatorKind.Equal, "itemId", itemId));
            JObject op = await _store.FirstOrDefault(query);
            return MobileServiceTableOperation<T>.Deserialize(op);
        }

        public async Task<MobileServiceTableOperation<T>> GetOperationAsync<T>(string id)
        {
            JObject op = await _store.LookupAsync(MobileServiceLocalSystemTables.OperationQueue, id);
            if (op == null)
            {
                return null;
            }
            return MobileServiceTableOperation<T>.Deserialize(op);
        }

        public async Task EnqueueAsync<T>(MobileServiceTableOperation<T> op)
        {
            op.Sequence = Interlocked.Increment(ref sequenceId);
            await _store.UpsertAsync(MobileServiceLocalSystemTables.OperationQueue, op.Serialize(), fromServer: false);
            Interlocked.Increment(ref pendingOperations);
        }

        public virtual async Task<bool> DeleteAsync<T>(string id, long version)
        {
            try
            {
                MobileServiceTableOperation<T> op = await GetOperationAsync<T>(id);
                if (op == null || op.Version != version)
                {
                    return false;
                }

                await _store.DeleteAsync(MobileServiceLocalSystemTables.OperationQueue, id);
                Interlocked.Decrement(ref pendingOperations);
                return true;
            }
            catch (Exception ex)
            {
                throw new MobileServiceLocalStoreException("Failed to delete operation from the local _store.", ex);
            }
        }

        public virtual async Task UpdateAsync<T>(MobileServiceTableOperation<T> op)
        {
            try
            {
                await _store.UpsertAsync(MobileServiceLocalSystemTables.OperationQueue, op.Serialize(), fromServer: false);
            }
            catch (Exception ex)
            {
                throw new MobileServiceLocalStoreException("Failed to update operation in the local _store.", ex);
            }
        }

        public virtual async Task<bool> UpdateAsync<T>(string id, long version, T item)
        {
            try
            {
                MobileServiceTableOperation<T> op = await GetOperationAsync<T>(id);
                if (op == null || op.Version != version)
                {
                    return false;
                }

                op.Version++;

                // Change the operation state back to pending since this is a newly updated operation without any conflicts
                op.State = MobileServiceTableOperationState.Pending;

                // if the operation type is delete then set the item property in the Operation table
                if (op.Kind == MobileServiceTableOperationKind.Delete)
                {
                    op.Item = item;
                }
                else
                {
                    op.Item = null;
                }

                await UpdateAsync(op);
                return true;
            }
            catch (Exception ex)
            {
                throw new MobileServiceLocalStoreException("Failed to update operation in the local _store.", ex);
            }
        }

        public static async Task<OperationQueue> LoadAsync<T>(IMobileServiceLocalStore store)
        {
            var opQueue = new OperationQueue(store);
            var query = CreateQuery();

            // to know how many pending operations are there
            query.IncludeTotalCount = true;

            // to get the max sequence id, order by sequence desc
            query.Ordering.Add(new OrderByNode(new MemberAccessNode(null, "sequence"), OrderByDirection.Descending));

            // we just need the highest value, not all the operations
            query.Top = 1;

            QueryResult<T> result = await store.QueryAsync<T>(query);
            opQueue.pendingOperations = result.TotalCount;
            opQueue.sequenceId = result.Values == null ? 0 : result.Values.Select(v => v.Value<long>("sequence")).FirstOrDefault();
            return opQueue;
        }

        private static MobileServiceTableQueryDescription CreateQuery()
        {
            var query = new MobileServiceTableQueryDescription(MobileServiceLocalSystemTables.OperationQueue);
            return query;
        }

        private static BinaryOperatorNode Compare(BinaryOperatorKind kind, string member, object value)
        {
            return new BinaryOperatorNode(kind, new MemberAccessNode(null, member), new ConstantNode(value));
        }
    }
}
