// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using Microsoft.WindowsAzure.MobileServices.Eventing;
using Microsoft.WindowsAzure.MobileServices.Query;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Sync
{
    internal sealed class LocalStoreChangeTracker : IMobileServiceLocalStore
    {
        private readonly IMobileServiceLocalStore _store;
        private readonly StoreTrackingContext _trackingContext;
        private readonly MobileServiceObjectReader _objectReader;
        private StoreOperationsBatch _operationsBatch;
        private readonly IMobileServiceEventManager _eventManager;
        private int _isBatchCompleted = 0;
        private readonly MobileServiceSyncSettingsManager _settings;
        private bool _trackRecordOperations;
        private bool _trackBatches;

        public LocalStoreChangeTracker(
            IMobileServiceLocalStore store,
            StoreTrackingContext trackingContext,
            IMobileServiceEventManager eventManager, 
            MobileServiceSyncSettingsManager settings)
        {
            Arguments.IsNotNull(store, nameof(store));
            Arguments.IsNotNull(trackingContext, nameof(trackingContext));
            Arguments.IsNotNull(eventManager, nameof(eventManager));
            Arguments.IsNotNull(settings, nameof(settings));

            _objectReader = new MobileServiceObjectReader();
            _store = store;
            _trackingContext = trackingContext;
            _eventManager = eventManager;
            _settings = settings;

            InitializeTracking();
        }

        private void InitializeTracking()
        {
            _trackRecordOperations = IsRecordTrackingEnabled();
            _trackBatches = IsBatchTrackingEnabled();

            if (!_trackRecordOperations & !_trackBatches)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                    "Tracking notifications are not enabled for the source {0}. To use a change tracker, you must enable record operation notifications, batch notifications or both.",
                    _trackingContext.Source));
            }

            if (_trackBatches)
            {
                _operationsBatch = new StoreOperationsBatch(_trackingContext.BatchId, _trackingContext.Source);
            }
        }

        private bool IsBatchTrackingEnabled()
        {
            switch (_trackingContext.Source)
            {
                case StoreOperationSource.Local:
                case StoreOperationSource.LocalPurge:
                case StoreOperationSource.LocalConflictResolution:
                    return false;
                case StoreOperationSource.ServerPull:
                    return _trackingContext.TrackingOptions.HasFlag(StoreTrackingOptions.NotifyServerPullBatch);
                case StoreOperationSource.ServerPush:
                    return _trackingContext.TrackingOptions.HasFlag(StoreTrackingOptions.NotifyServerPushBatch);
                default:
                    throw new InvalidOperationException("Unknown tracking source");
            }
        }

        private bool IsRecordTrackingEnabled()
        {
            switch (_trackingContext.Source)
            {
                case StoreOperationSource.Local:
                case StoreOperationSource.LocalPurge:
                    return _trackingContext.TrackingOptions.HasFlag(StoreTrackingOptions.NotifyLocalOperations);
                case StoreOperationSource.LocalConflictResolution:
                    return _trackingContext.TrackingOptions.HasFlag(StoreTrackingOptions.NotifyLocalConflictResolutionOperations);
                case StoreOperationSource.ServerPull:
                    return _trackingContext.TrackingOptions.HasFlag(StoreTrackingOptions.NotifyServerPullOperations);
                case StoreOperationSource.ServerPush:
                    return _trackingContext.TrackingOptions.HasFlag(StoreTrackingOptions.NotifyServerPushOperations);
                default:
                    throw new InvalidOperationException("Unknown tracking source");
            }
        }

        public async Task DeleteAsync(MobileServiceTableQueryDescription query)
        {
            Arguments.IsNotNull(query, nameof(query));

            string[] recordIds = null;

            if (!query.TableName.StartsWith(MobileServiceLocalSystemTables.Prefix) && _trackingContext.Source != StoreOperationSource.LocalPurge)
            {
                var result = await _store.QueryAsync(query);
                recordIds = result.Values.Select(j => objectReader.GetId((JObject)j)).ToArray();
            }

            await _store.DeleteAsync(query);

            if (recordIds != null)
            {
                foreach (var id in recordIds)
                {
                    TrackStoreOperation(query.TableName, id, LocalStoreOperationKind.Delete);
                }
            }
        }

        public async Task DeleteAsync(string tableName, IEnumerable<string> ids)
        {
            Arguments.IsNotNull(tableName, nameof(tableName));
            Arguments.IsNotNull(ids, nameof(ids));

            if (!tableName.StartsWith(MobileServiceLocalSystemTables.Prefix))
            {
                IEnumerable<string> notificationIds = ids;

                if (_trackingContext.TrackingOptions.HasFlag(StoreTrackingOptions.DetectRecordChanges))
                {
                    IDictionary<string, string> existingRecords = await GetItemsAsync(tableName, ids, false);
                    notificationIds = existingRecords.Select(kvp => kvp.Key);
                }

                await _store.DeleteAsync(tableName, ids);

                foreach (var id in notificationIds)
                {
                    TrackStoreOperation(tableName, id, LocalStoreOperationKind.Delete);
                }
            }
            else
            {
                await _store.DeleteAsync(tableName, ids);
            }
        }

        public Task<ITable> LookupAsync(string tableName, string id)
        {
            Arguments.IsNotNull(tableName, nameof(tableName));
            Arguments.IsNotNull(id, nameof(id));
            return _store.LookupAsync(tableName, id);
        }

        public Task<ITable> ReadAsync(MobileServiceTableQueryDescription query)
        {
            Arguments.IsNotNull(query, nameof(query));

            return _store.ReadAsync(query);
        }

        public async Task UpsertAsync(string tableName, IEnumerable<ITable> items, bool ignoreMissingColumns)
        {
            Arguments.IsNotNull(tableName, nameof(tableName));
            Arguments.IsNotNull(items, nameof(items));

            if (!tableName.StartsWith(MobileServiceLocalSystemTables.Prefix))
            {
                IDictionary<string, string> existingRecords = null;
                bool analyzeUpserts = _trackingContext.TrackingOptions.HasFlag(StoreTrackingOptions.DetectInsertsAndUpdates);
                bool supportsVersion = false;

                if (analyzeUpserts)
                {
                    MobileServiceSystemProperties systemProperties = await _settings.GetSystemPropertiesAsync(tableName);
                    supportsVersion = systemProperties.HasFlag(MobileServiceSystemProperties.Version);

                    existingRecords = await GetItemsAsync(tableName, items.Select(i => i.Id), supportsVersion);
                }

                await _store.UpsertAsync(tableName, items, ignoreMissingColumns);

                foreach (var item in items)
                {
                    string itemId = item.Id;
                    LocalStoreOperationKind operationKind = LocalStoreOperationKind.Upsert;

                    if (analyzeUpserts)
                    {
                        if (existingRecords.ContainsKey(itemId))
                        {
                            operationKind = LocalStoreOperationKind.Update;

                            // If the update isn't a result of a local operation, check if the item exposes a version property
                            // and if we truly have a new version (an actual change) before tracking the change. 
                            // This avoids update notifications for records that haven't changed, which would usually happen as a result of a pull
                            // operation, because of the logic used to pull changes.
                            if (_trackingContext.Source != StoreOperationSource.Local && supportsVersion
                                && string.Compare(existingRecords[itemId], item.Version) == 0)
                            {
                                continue;
                            }
                        }
                        else
                        {
                            operationKind = LocalStoreOperationKind.Insert;
                        }
                    }

                    TrackStoreOperation(tableName, itemId, operationKind);
                }
            }
            else
            {
                await _store.UpsertAsync(tableName, items, ignoreMissingColumns);
            }
        }

        public Task InitializeAsync()
        {
            return _store.InitializeAsync();
        }

        private async Task<IDictionary<string, string>> GetItemsAsync(string tableName, IEnumerable<string> ids, bool includeVersion)
        {
            var query = new MobileServiceTableQueryDescription(tableName);
            BinaryOperatorNode idListFilter = ids.Select(t => new BinaryOperatorNode(BinaryOperatorKind.Equal, new MemberAccessNode(null, MobileServiceSystemColumns.Id), new ConstantNode(t)))
                                                 .Aggregate((aggregate, item) => new BinaryOperatorNode(BinaryOperatorKind.Or, aggregate, item));

            query.Filter = idListFilter;
            query.Selection.Add(MobileServiceSystemColumns.Id);

            if (includeVersion)
            {
                query.Selection.Add(MobileServiceSystemColumns.Version);
            }

            var result = await _store.QueryAsync(query);

            return result.Values.ToDictionary(
                t => _objectReader.GetId((JObject)t),
                rec => includeVersion 
                ? rec[MobileServiceSystemColumns.Version].ToString() 
                : null);
        }

        private void TrackStoreOperation(string tableName, string itemId, LocalStoreOperationKind operationKind)
        {
            var operation = new StoreOperation(tableName, itemId, operationKind, _trackingContext.Source, _trackingContext.BatchId);

            if (_trackBatches)
            {
               _operationsBatch.IncrementOperationCount(operationKind)
                   .ContinueWith(t => t.Exception.Handle(e => true), TaskContinuationOptions.OnlyOnFaulted);
            }

            if (_trackRecordOperations)
            {
                _eventManager.BackgroundPublish(new StoreOperationCompletedEvent(operation));
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                CompleteBatch();
            }
        }

        private void CompleteBatch()
        {
            if (Interlocked.Exchange(ref _isBatchCompleted, 1) == 0)
            {
                if (_trackBatches)
                {
                    _eventManager.PublishAsync(new StoreOperationsBatchCompletedEvent(_operationsBatch));
                }
            }
        }
    }
}
