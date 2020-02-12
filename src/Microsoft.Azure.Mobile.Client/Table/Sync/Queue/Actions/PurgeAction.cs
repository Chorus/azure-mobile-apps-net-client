// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.MobileServices.Eventing;
using Microsoft.Azure.MobileServices.Query;

namespace Microsoft.Azure.MobileServices.Sync
{
    internal class PurgeAction : TableAction
    {
        private readonly bool _force;
        private readonly IMobileServiceEventManager _eventManager;

        public PurgeAction(MobileServiceTable table,
                           MobileServiceTableKind tableKind,
                           string queryId,
                           MobileServiceTableQueryDescription query,
                           bool force,
                           MobileServiceSyncContext context,
                           OperationQueue operationQueue,
                           IMobileServiceEventManager eventManager,
                           MobileServiceSyncSettingsManager settings,
                           IMobileServiceLocalStore store,
                           CancellationToken cancellationToken)
            : base(table, tableKind, queryId, query, null, context, operationQueue, settings, store, cancellationToken)
        {
            _force = force;
            _eventManager = eventManager;
        }

        protected async override Task<bool> HandleDirtyTable()
        {
            if (this.Query.Filter != null || !_force)
            {
                throw new InvalidOperationException("The table cannot be purged because it has pending operations.");
            }

            var delOperationsQuery = new MobileServiceTableQueryDescription(MobileServiceLocalSystemTables.OperationQueue)
            {
                Filter = new BinaryOperatorNode(BinaryOperatorKind.Equal, new MemberAccessNode(null, "tableName"), new ConstantNode(this.Table.TableName)),

                // count ops to be deleted
                IncludeTotalCount = true,
                Top = 0
            };
            long toRemove = QueryResult.Parse(await this.Store.ReadAsync(delOperationsQuery), null, validate: false).TotalCount;

            // delete operations
            delOperationsQuery.Top = null;
            await this.Store.DeleteAsync(delOperationsQuery);

            // delete errors
            var delErrorsQuery = new MobileServiceTableQueryDescription(MobileServiceLocalSystemTables.SyncErrors)
            {
                Filter = delOperationsQuery.Filter
            };
            await this.Store.DeleteAsync(delErrorsQuery);

            // update queue operation count
            this.OperationQueue.UpdateOperationCount(-toRemove);

            return true;
        }

        protected override async Task ProcessTableAsync()
        {
            if (!string.IsNullOrEmpty(this.QueryId))
            {
                await this.Settings.ResetDeltaTokenAsync(this.Table.TableName, this.QueryId);
            }
            await this.Store.DeleteAsync(this.Query);

            if (!this.Table.TableName.StartsWith(MobileServiceLocalSystemTables.Prefix))
            {
                this._eventManager.BackgroundPublish(new PurgeCompletedEvent(this.Table.TableName));
            }
        }
    }
}
