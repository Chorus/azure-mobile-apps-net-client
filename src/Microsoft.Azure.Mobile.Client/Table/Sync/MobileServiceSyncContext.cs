// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MobileServices.Eventing;
using Microsoft.WindowsAzure.MobileServices.Query;
using Microsoft.WindowsAzure.MobileServices.Threading;
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Sync
{
    internal class MobileServiceSyncContext : IMobileServiceSyncContext, IDisposable
    {
        private MobileServiceSyncSettingsManager _settings;
        private TaskCompletionSource<object> _initializeTask;
        private readonly MobileServiceClient _client;

        /// <summary>
        /// Lock to ensure that multiple insert,update,delete operations don't interleave as they are added to queue and storage
        /// </summary>
        private readonly AsyncReaderWriterLock storeQueueLock = new AsyncReaderWriterLock();

        /// <summary>
        /// Variable for Store property. Not meant to be accessed directly.
        /// </summary>
        private IMobileServiceLocalStore _store;

        /// <summary>
        /// Queue for executing sync calls (push,pull) one after the other
        /// </summary>
        private ActionBlock _syncQueue;

        /// <summary>
        /// Queue for pending operations (insert,delete,update) against remote table 
        /// </summary>
        private OperationQueue _opQueue;

        private StoreTrackingOptions storeTrackingOptions;

        private IMobileServiceLocalStore _localOperationsStore;

        public IMobileServiceSyncHandler Handler { get; private set; }

        public IMobileServiceLocalStore Store
        {
            get => _store;
            private set
            {
                IMobileServiceLocalStore oldStore = this._store;
                this._store = value;
                if (oldStore != null)
                {
                    oldStore.Dispose();
                }
            }
        }

        public bool IsInitialized =>
            _initializeTask != null && _initializeTask.Task.Status == TaskStatus.RanToCompletion;


        public MobileServiceSyncContext(MobileServiceClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public long PendingOperations => !IsInitialized 
            ? 0
            :_opQueue.PendingOperations;

        public StoreTrackingOptions StoreTrackingOptions => storeTrackingOptions;

        public Task InitializeAsync(IMobileServiceLocalStore store, IMobileServiceSyncHandler handler)
        {
            return InitializeAsync(store, handler, StoreTrackingOptions.None);
        }

        public async Task InitializeAsync(IMobileServiceLocalStore store, IMobileServiceSyncHandler handler, StoreTrackingOptions trackingOptions)
        {
            if (store == null)
            {
                throw new ArgumentNullException(nameof(store));
            }
            handler = handler ?? new MobileServiceSyncHandler();

            _initializeTask = new TaskCompletionSource<object>();

            using (await storeQueueLock.WriterLockAsync())
            {
                Handler = handler;
                Store = store;
                storeTrackingOptions = trackingOptions;

                _syncQueue = new ActionBlock();
                await Store.InitializeAsync();
                _opQueue = await OperationQueue.LoadAsync(store);
                _settings = new MobileServiceSyncSettingsManager(store);
                _localOperationsStore = StoreChangeTrackerFactory.CreateTrackedStore(store, StoreOperationSource.Local, trackingOptions, this._client.EventManager, this._settings);

                _initializeTask.SetResult(null);
            }
        }

        public async Task<JToken> ReadAsync(string tableName, string query)
        {
            await EnsureInitializedAsync();

            var queryDescription = MobileServiceTableQueryDescription.Parse(tableName, query);
            using (await storeQueueLock.ReaderLockAsync())
            {
                return await Store.ReadAsync(queryDescription);
            }
        }

        public async Task InsertAsync(string tableName, MobileServiceTableKind tableKind, string id, JObject item)
        {
            var operation = new InsertOperation(tableName, tableKind, id)
            {
                Table = await this.GetTable(tableName)
            };

            await this.ExecuteOperationAsync(operation, item);
        }

        public async Task UpdateAsync(string tableName, MobileServiceTableKind tableKind, string id, JObject item)
        {
            var operation = new UpdateOperation(tableName, tableKind, id)
            {
                Table = await this.GetTable(tableName)
            };

            await this.ExecuteOperationAsync(operation, item);
        }

        public async Task DeleteAsync(string tableName, MobileServiceTableKind tableKind, string id, JObject item)
        {
            var operation = new DeleteOperation(tableName, tableKind, id)
            {
                Table = await this.GetTable(tableName),
                Item = item // item will be deleted from store, so we need to put it in the operation queue
            };

            await this.ExecuteOperationAsync(operation, item);
        }

        public async Task<JObject> LookupAsync(string tableName, string id)
        {
            await this.EnsureInitializedAsync();

            return await this.Store.LookupAsync(tableName, id);
        }

        /// <summary>
        /// Pulls all items that match the given query from the associated remote table.
        /// </summary>
        /// <param name="tableName">The name of table to pull</param>
        /// <param name="tableKind">The kind of table</param>
        /// <param name="queryId">A string that uniquely identifies this query and is used to keep track of its sync state.</param>
        /// <param name="query">An OData query that determines which items to 
        /// pull from the remote table.</param>
        /// <param name="options">An instance of <see cref="MobileServiceRemoteTableOptions"/></param>
        /// <param name="parameters">A dictionary of user-defined parameters and values to include in 
        /// the request URI query string.</param>
        /// <param name="relatedTables">
        /// List of tables that may have related records that need to be push before this table is pulled down.
        /// When no table is specified, all tables are considered related.
        /// </param>
        /// <param name="reader">An instance of <see cref="MobileServiceObjectReader"/></param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> token to observe
        /// </param>
        /// <param name="pullOptions">
        /// PullOptions that determine how to pull data from the remote table
        /// </param>
        /// <returns>
        /// A task that completes when pull operation has finished.
        /// </returns>
        public async Task PullAsync(string tableName, MobileServiceTableKind tableKind, string queryId, string query, MobileServiceRemoteTableOptions options, IDictionary<string, string> parameters, IEnumerable<string> relatedTables, MobileServiceObjectReader reader, CancellationToken cancellationToken, PullOptions pullOptions)
        {
            await this.EnsureInitializedAsync();

            if (parameters != null)
            {
                if (parameters.Keys.Any(k => k.Equals(MobileServiceTable.IncludeDeletedParameterName, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new ArgumentException("The key '{0}' is reserved and cannot be specified as a query parameter.".FormatInvariant(MobileServiceTable.IncludeDeletedParameterName));
                }
            }

            var table = await GetTable(tableName);
            var queryDescription = MobileServiceTableQueryDescription.Parse(this._client.MobileAppUri, tableName, query);

            // local schema should be same as remote schema otherwise push can't function
            if (queryDescription.Selection.Any() || queryDescription.Projections.Any())
            {
                throw new ArgumentException("Pull query with select clause is not supported.", "query");
            }

            bool isIncrementalSync = !string.IsNullOrEmpty(queryId);
            if (isIncrementalSync)
            {
                if (queryDescription.Ordering.Any())
                {
                    throw new ArgumentException("Incremental pull query must not have orderby clause.", "query");
                }
                if (queryDescription.Top.HasValue || queryDescription.Skip.HasValue)
                {
                    throw new ArgumentException("Incremental pull query must not have skip or top specified.", "query");
                }
            }

            if (!options.HasFlag(MobileServiceRemoteTableOptions.OrderBy) && queryDescription.Ordering.Any())
            {
                throw new ArgumentException("The supported table options does not include orderby.", "query");
            }

            if (!options.HasFlag(MobileServiceRemoteTableOptions.Skip) && queryDescription.Skip.HasValue)
            {
                throw new ArgumentException("The supported table options does not include skip.", "query");
            }

            if (!options.HasFlag(MobileServiceRemoteTableOptions.Top) && queryDescription.Top.HasValue)
            {
                throw new ArgumentException("The supported table options does not include top.", "query");
            }

            // let us not burden the server to calculate the count when we don't need it for pull
            queryDescription.IncludeTotalCount = false;

            using (var store = StoreChangeTrackerFactory.CreateTrackedStore(this.Store, StoreOperationSource.ServerPull, this.storeTrackingOptions, this._client.EventManager, this._settings))
            {
                var action = new PullAction(table, tableKind, this, queryId, queryDescription, parameters, relatedTables,
                    _opQueue, _settings, store, options, pullOptions, reader, cancellationToken);
                await ExecuteSyncAction(action);
            }
        }

        public async Task PurgeAsync(string tableName, MobileServiceTableKind tableKind, string queryId, string query, bool force, CancellationToken cancellationToken)
        {
            await this.EnsureInitializedAsync();

            var table = await this.GetTable(tableName);
            var queryDescription = MobileServiceTableQueryDescription.Parse(tableName, query);

            using (var trackedStore = StoreChangeTrackerFactory.CreateTrackedStore(this.Store, StoreOperationSource.LocalPurge, this.storeTrackingOptions, this._client.EventManager, this._settings))
            {
                var action = new PurgeAction(table, tableKind, queryId, queryDescription, force, this, this._opQueue, this._client.EventManager, this._settings, this.Store, cancellationToken);
                await this.ExecuteSyncAction(action);
            }
        }

        public Task PushAsync(CancellationToken cancellationToken)
        {
            return PushAsync(cancellationToken, MobileServiceTableKind.Table, new string[0]);
        }

        public async Task PushAsync(CancellationToken cancellationToken, MobileServiceTableKind tableKind, params string[] tableNames)
        {
            await this.EnsureInitializedAsync();

            // use empty handler if its not a standard table push
            var handler = tableKind == MobileServiceTableKind.Table ? this.Handler : new MobileServiceSyncHandler();

            using (var trackedStore = StoreChangeTrackerFactory.CreateTrackedStore(this.Store, StoreOperationSource.ServerPush, this.storeTrackingOptions, this._client.EventManager, this._settings))
            {
                var action = new PushAction(this._opQueue,
                                          trackedStore,
                                          tableKind,
                                          tableNames,
                                          handler,
                                          this._client,
                                          this,
                                          cancellationToken);

                await this.ExecuteSyncAction(action);
            }
        }

        public async Task ExecuteSyncAction(SyncAction action)
        {
            Task discard = this._syncQueue.Post(action.ExecuteAsync, action.CancellationToken);

            await action.CompletionTask;
        }

        public virtual async Task<MobileServiceTable> GetTable(string tableName)
        {
            await this.EnsureInitializedAsync();

            var table = this._client.GetTable(tableName) as MobileServiceTable;
            table.Features = MobileServiceFeatures.Offline;

            return table;
        }

        public Task CancelAndUpdateItemAsync(MobileServiceTableOperationError error, JObject item)
        {
            string itemId = error.Item.Value<string>(MobileServiceSystemColumns.Id);
            return ExecuteOperationSafeAsync(itemId, error.TableName, async () =>
            {
                await TryCancelOperation(error);
                using (var trackedStore = StoreChangeTrackerFactory.CreateTrackedStore(this.Store, StoreOperationSource.LocalConflictResolution, this.storeTrackingOptions, this._client.EventManager, this._settings))
                {
                    await trackedStore.UpsertAsync(error.TableName, item, fromServer: true);
                }
            });
        }

        public Task UpdateOperationAsync(MobileServiceTableOperationError error, JObject item)
        {
            string itemId = error.Item.Value<string>(MobileServiceSystemColumns.Id);
            return this.ExecuteOperationSafeAsync(itemId, error.TableName, async () =>
            {
                await this.TryUpdateOperation(error, item);
                if (error.OperationKind != MobileServiceTableOperationKind.Delete)
                {
                    using (var trackedStore = StoreChangeTrackerFactory.CreateTrackedStore(this.Store, StoreOperationSource.LocalConflictResolution, this.storeTrackingOptions, this._client.EventManager, this._settings))
                    {
                        await trackedStore.UpsertAsync(error.TableName, item, fromServer: true);
                    }
                }
            });
        }

        private async Task TryUpdateOperation(MobileServiceTableOperationError error, JObject item)
        {
            if (!await this._opQueue.UpdateAsync(error.Id, error.OperationVersion, item))
            {
                throw new InvalidOperationException("The operation has been updated and cannot be updated again");
            }

            // delete errors for updated operation
            await this.Store.DeleteAsync(MobileServiceLocalSystemTables.SyncErrors, error.Id);
        }

        public Task CancelAndDiscardItemAsync(MobileServiceTableOperationError error)
        {
            string itemId = error.Item.Value<string>(MobileServiceSystemColumns.Id);
            return this.ExecuteOperationSafeAsync(itemId, error.TableName, async () =>
            {
                await this.TryCancelOperation(error);
                using (var trackedStore = StoreChangeTrackerFactory.CreateTrackedStore(this.Store, StoreOperationSource.LocalConflictResolution, this.storeTrackingOptions, this._client.EventManager, this._settings))
                {
                    await trackedStore.DeleteAsync(error.TableName, itemId);
                }
            });
        }

        public async Task DeferTableActionAsync(TableAction action)
        {
            IEnumerable<string> tableNames;
            if (action.RelatedTables == null) // no related table
            {
                tableNames = new[] { action.Table.TableName };
            }
            else if (action.RelatedTables.Any()) // some related tables
            {
                tableNames = new[] { action.Table.TableName }.Concat(action.RelatedTables);
            }
            else // all tables are related
            {
                tableNames = Enumerable.Empty<string>();
            }

            try
            {
                await this.PushAsync(action.CancellationToken, action.TableKind, tableNames.ToArray());
            }
            finally
            {
                Task discard = this._syncQueue.Post(action.ExecuteAsync, action.CancellationToken);
            }
        }

        private async Task TryCancelOperation(MobileServiceTableOperationError error)
        {
            if (!await this._opQueue.DeleteAsync(error.Id, error.OperationVersion))
            {
                throw new InvalidOperationException("The operation has been updated and cannot be cancelled.");
            }
            // delete errors for cancelled operation
            await this.Store.DeleteAsync(MobileServiceLocalSystemTables.SyncErrors, error.Id);
        }

        private async Task EnsureInitializedAsync()
        {
            if (this._initializeTask == null)
            {
                throw new InvalidOperationException("SyncContext is not yet initialized.");
            }
            else
            {
                // when the initialization has started we wait for it to complete
                await this._initializeTask.Task;
            }
        }

        private Task ExecuteOperationAsync(MobileServiceTableOperation operation, JObject item)
        {
            return this.ExecuteOperationSafeAsync(operation.ItemId, operation.TableName, async () =>
            {
                MobileServiceTableOperation existing = await this._opQueue.GetOperationByItemIdAsync(operation.TableName, operation.ItemId);
                if (existing != null)
                {
                    existing.Validate(operation); // make sure this operation is legal and collapses after any previous operation on same item already in the queue
                }

                try
                {
                    await operation.ExecuteLocalAsync(this._localOperationsStore, item); // first execute operation on local store
                }
                catch (Exception ex)
                {
                    if (ex is MobileServiceLocalStoreException)
                    {
                        throw;
                    }
                    throw new MobileServiceLocalStoreException("Failed to perform operation on local store.", ex);
                }

                if (existing != null)
                {
                    existing.Collapse(operation); // cancel either existing, new or both operation 
                                                  // delete error for collapsed operation
                    await this.Store.DeleteAsync(MobileServiceLocalSystemTables.SyncErrors, existing.Id);
                    if (existing.IsCancelled) // if cancelled we delete it
                    {
                        await this._opQueue.DeleteAsync(existing.Id, existing.Version);
                    }
                    else if (existing.IsUpdated)
                    {
                        await this._opQueue.UpdateAsync(existing);
                    }
                }

                // if validate didn't cancel the operation then queue it
                if (!operation.IsCancelled)
                {
                    await this._opQueue.EnqueueAsync(operation);
                }
            });
        }

        private async Task ExecuteOperationSafeAsync(string itemId, string tableName, Func<Task> action)
        {
            await EnsureInitializedAsync();

            // take slowest lock first and quickest last in order to avoid blocking quick operations for long time            
            using (await _opQueue.LockItemAsync(itemId, CancellationToken.None))  // prevent any inflight operation on the same item
            using (await _opQueue.LockTableAsync(tableName, CancellationToken.None)) // prevent interferance with any in-progress pull/purge action
            using (await storeQueueLock.WriterLockAsync()) // prevent any other operation from interleaving between store and queue insert
            {
                await action();
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && _store != null)
            {
                _settings.Dispose();
                _store.Dispose();
            }
        }
    }
}
