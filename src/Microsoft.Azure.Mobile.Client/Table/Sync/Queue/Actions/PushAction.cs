// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Sync
{
    /// <summary>
    /// Responsible for executing a batch of operations from queue until bookmark is found
    /// </summary>
    internal class PushAction : SyncAction
    {
        private readonly IMobileServiceSyncHandler _syncHandler;
        private readonly MobileServiceClient _client;
        private readonly MobileServiceSyncContext _context;
        private readonly IEnumerable<string> _tableNames;
        private readonly MobileServiceTableKind _tableKind;

        public PushAction(OperationQueue operationQueue,
                          IMobileServiceLocalStore store,
                          MobileServiceTableKind tableKind,
                          IEnumerable<string> tableNames,
                          IMobileServiceSyncHandler syncHandler,
                          MobileServiceClient client,
                          MobileServiceSyncContext context,
                          CancellationToken cancellationToken)
            : base(operationQueue, store, cancellationToken)
        {
            _tableKind = tableKind;
            _tableNames = tableNames;
            _client = client;
            _syncHandler = syncHandler;
            _context = context;
        }

        public override async Task ExecuteAsync()
        {
            var batch = new OperationBatch(_syncHandler, Store, _context);
            var syncErrors = new List<MobileServiceTableOperationError>();
            var batchStatus = MobileServicePushStatus.InternalError;

            try
            {
                batchStatus = await ExecuteBatchAsync(batch, syncErrors);
            }
            catch (Exception ex)
            {
                batch.OtherErrors.Add(ex);
            }

            await FinalizePush(batch, batchStatus, syncErrors);
        }

        private async Task<MobileServicePushStatus> ExecuteBatchAsync(OperationBatch batch, List<MobileServiceTableOperationError> syncErrors)
        {
            // when cancellation is requested, abort the batch
            CancellationToken.Register(() => batch.Abort(MobileServicePushStatus.CancelledByToken));

            try
            {
                await ExecuteAllOperationsAsync(batch);
            }
            catch (Exception ex)
            {
                batch.OtherErrors.Add(ex);
            }

            var batchStatus = batch.AbortReason.GetValueOrDefault(MobileServicePushStatus.Complete);
            try
            {
                syncErrors.AddRange(await batch.LoadSyncErrorsAsync(this._client.SerializerSettings));
            }
            catch (Exception ex)
            {

                batch.OtherErrors.Add(new MobileServiceLocalStoreException("Failed to read errors from the local store.", ex));
            }
            return batchStatus;
        }

        private async Task FinalizePush(OperationBatch batch, MobileServicePushStatus batchStatus, IEnumerable<MobileServiceTableOperationError> syncErrors)
        {
            var result = new MobileServicePushCompletionResult(syncErrors, batchStatus);

            try
            {
                await batch.SyncHandler.OnPushCompleteAsync(result);

                // now that we've successfully given the errors to user, we can delete them from store
                await batch.DeleteErrorsAsync();
            }
            catch (Exception ex)
            {
                batch.OtherErrors.Add(ex);
            }

            if (batchStatus != MobileServicePushStatus.Complete || batch.HasErrors(syncErrors))
            {
                List<MobileServiceTableOperationError> unhandledSyncErrors = syncErrors.Where(e => !e.Handled).ToList();

                Exception inner;
                if (batch.OtherErrors.Count == 1)
                {
                    inner = batch.OtherErrors[0];
                }
                else
                {
                    inner = batch.OtherErrors.Any() ? new AggregateException(batch.OtherErrors) : null;
                }

                // create a new result with only unhanded errors
                result = new MobileServicePushCompletionResult(unhandledSyncErrors, batchStatus);
                this.TaskSource.TrySetException(new MobileServicePushFailedException(result, inner));
            }
            else
            {
                this.TaskSource.SetResult(0);
            }
        }

        private async Task ExecuteAllOperationsAsync(OperationBatch batch)
        {
            MobileServiceTableOperation operation = await this.OperationQueue.PeekAsync(0, this._tableKind, this._tableNames);

            // keep taking out operations and executing them until queue is empty or operation finds the bookmark or batch is aborted 
            while (operation != null)
            {
                using (await OperationQueue.LockItemAsync(operation.ItemId, this.CancellationToken))
                {
                    bool success = await ExecuteOperationAsync(operation, batch);

                    if (batch.AbortReason.HasValue)
                    {
                        break;
                    }

                    if (success)
                    {
                        // we successfully executed an operation so remove it from queue
                        await OperationQueue.DeleteAsync(operation.Id, operation.Version);
                    }

                    // get next operation
                    operation = await this.OperationQueue.PeekAsync(operation.Sequence, this._tableKind, this._tableNames);
                }
            }
        }

        private async Task<bool> ExecuteOperationAsync(MobileServiceTableOperation operation, OperationBatch batch)
        {
            if (operation.IsCancelled || this.CancellationToken.IsCancellationRequested)
            {
                return false;
            }

            operation.Table = await this._context.GetTable(operation.TableName);
            await LoadOperationItem(operation, batch);

            if (operation.Item == null || this.CancellationToken.IsCancellationRequested)
            {
                return false;
            }

            await TryUpdateOperationState(operation, MobileServiceTableOperationState.Attempted, batch);

            // strip out system properties before executing the operation
            operation.Item = MobileServiceSyncTable.RemoveSystemPropertiesKeepVersion(operation.Item);

            JObject result = null;
            Exception error = null;
            try
            {
                result = await batch.SyncHandler.ExecuteTableOperationAsync(operation);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            if (error != null)
            {
                await TryUpdateOperationState(operation, MobileServiceTableOperationState.Failed, batch);

                if (TryAbortBatch(batch, error))
                {
                    // there is no error to save in sync error and no result to capture
                    // this operation will be executed again next time the push happens
                    return false;
                }
            }

            // save the result if ExecuteTableOperation did not throw
            if (error == null && result.IsValidItem() && operation.CanWriteResultToStore)
            {
                await TryStoreOperation(() => this.Store.UpsertAsync(operation.TableName, result, fromServer: true), batch, "Failed to update the item in the local store.");
            }
            else if (error != null)
            {
                HttpStatusCode? statusCode = null;
                string rawResult = null;
                if (error is MobileServiceInvalidOperationException iox && iox.Response != null)
                {
                    statusCode = iox.Response.StatusCode;
                    Tuple<string, JToken> content = await MobileServiceTable.ParseContent(iox.Response, this._client.SerializerSettings);
                    rawResult = content.Item1;
                    result = content.Item2.ValidItemOrNull();
                }
                var syncError = new MobileServiceTableOperationError(operation.Id,
                                                                        operation.Version,
                                                                        operation.Kind,
                                                                        statusCode,
                                                                        operation.TableName,
                                                                        operation.Item,
                                                                        rawResult,
                                                                        result)
                                                                        {
                                                                            TableKind = this._tableKind,
                                                                            Context = this._context
                                                                        };
                await batch.AddSyncErrorAsync(syncError);
            }

            bool success = error == null;
            return success;
        }

        private async Task TryUpdateOperationState(MobileServiceTableOperation operation, MobileServiceTableOperationState state, OperationBatch batch)
        {
            operation.State = state;
            await TryStoreOperation(() => this.OperationQueue.UpdateAsync(operation), batch, "Failed to update operation in the local store.");
        }

        private async Task LoadOperationItem(MobileServiceTableOperation operation, OperationBatch batch)
        {
            // only read the item from store if it is not in the operation already
            if (operation.Item == null)
            {
                await TryStoreOperation(async () =>
                {
                    operation.Item = await this.Store.LookupAsync(operation.TableName, operation.ItemId) as JObject;
                }, batch, "Failed to read the item from local store.");

                // Add sync error if item is not found.
                if (operation.Item == null)
                {
                    // Create an item with only id to be able handle error properly.
                    var item = new JObject(new JProperty(MobileServiceSystemColumns.Id, operation.ItemId));
                    var syncError = new MobileServiceTableOperationError(operation.Id,
                                                                         operation.Version,
                                                                         operation.Kind,
                                                                         null,
                                                                         operation.TableName,
                                                                         item, null, null)
                    {
                        TableKind = _tableKind,
                        Context = _context
                    };
                    await batch.AddSyncErrorAsync(syncError);
                }
            }
        }

        private static async Task TryStoreOperation(Func<Task> action, OperationBatch batch, string error)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                batch.Abort(MobileServicePushStatus.CancelledBySyncStoreError);
                throw new MobileServiceLocalStoreException(error, ex);
            }
        }

        private bool TryAbortBatch(OperationBatch batch, Exception ex)
        {
            if (ex.IsNetworkError())
            {
                batch.Abort(MobileServicePushStatus.CancelledByNetworkError);
            }
            else if (ex.IsAuthenticationError())
            {
                batch.Abort(MobileServicePushStatus.CancelledByAuthenticationError);
            }
            else if (ex is MobileServicePushAbortException)
            {
                batch.Abort(MobileServicePushStatus.CancelledByOperation);
            }
            else
            {
                return false; // not a known exception that should abort the batch
            }

            return true;
        }
    }
}
