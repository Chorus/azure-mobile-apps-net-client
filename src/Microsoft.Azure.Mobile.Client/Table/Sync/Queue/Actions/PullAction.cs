// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.MobileServices.Query;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.MobileServices.Sync
{
    internal class PullAction : TableAction
    {
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private readonly IDictionary<string, string> _parameters;
        private readonly MobileServiceRemoteTableOptions _options; // the supported options on remote table 
        private readonly PullOptions _pullOptions;
        private readonly PullCursor _cursor;
        private Task _pendingAction;
        private PullStrategy _strategy;

        public PullAction(MobileServiceTable table,
                          MobileServiceTableKind tableKind,
                          MobileServiceSyncContext context,
                          string queryId,
                          MobileServiceTableQueryDescription query,
                          IDictionary<string, string> parameters,
                          IEnumerable<string> relatedTables,
                          OperationQueue operationQueue,
                          MobileServiceSyncSettingsManager settings,
                          IMobileServiceLocalStore store,
                          MobileServiceRemoteTableOptions options,
                          PullOptions pullOptions,
                          MobileServiceObjectReader reader,
                          CancellationToken cancellationToken)
            : base(table, tableKind, queryId, query, relatedTables, context, operationQueue, settings, store, cancellationToken)
        {
            _options = options;
            _parameters = parameters;
            _cursor = new PullCursor(query);
            _pullOptions = pullOptions;
            Reader = reader ?? new MobileServiceObjectReader();
        }

        public MobileServiceObjectReader Reader { get; private set; }

        public IDictionary<string, string> Parameters => _parameters;

        protected override Task<bool> HandleDirtyTable()
        {
            // there are pending operations on the same table so defer the action
            _pendingAction = Context.DeferTableActionAsync(this);
            // we need to return in order to give PushAsync a chance to execute so we don't await the pending push
            return Task.FromResult(false);
        }

        protected override Task WaitPendingAction()
        {
            return _pendingAction ?? Task.CompletedTask;
        }

        protected async override Task ProcessTableAsync()
        {
            await CreatePullStrategy();

            QueryResult result;
            do
            {
                CancellationToken.ThrowIfCancellationRequested();

                string query = Query.ToODataString();
                if (Query.UriPath != null)
                {
                    query = MobileServiceUrlBuilder.CombinePathAndQuery(Query.UriPath, query);
                }
                result = await Table.ReadAsync(query, MobileServiceTable.IncludeDeleted(_parameters), Table.Features);
                await ProcessAll(result.Values); // process the first batch

                result = await FollowNextLinks(result);
            }
            // if we are not at the end of result and there is no link to get more results                
            while (!EndOfResult(result) && await _strategy.MoveToNextPageAsync());

            await _strategy.PullCompleteAsync();
        }

        private async Task ProcessAll(JArray items)
        {
            CancellationToken.ThrowIfCancellationRequested();

            var deletedIds = new List<string>();
            var upsertList = new List<JObject>();

            foreach (var token in items)
            {
                if (!(token is JObject item))
                {
                    continue;
                }

                if (!_cursor.OnNext())
                {
                    break;
                }

                string id = Reader.GetId(item);
                if (id == null)
                {
                    continue;
                }

                var pendingOperation = await OperationQueue.GetOperationByItemIdAsync(Table.TableName, id);
                if (pendingOperation != null)
                {
                    continue;
                }

                DateTimeOffset updatedAt = Reader.GetUpdatedAt(item).GetValueOrDefault(Epoch).ToUniversalTime();
                _strategy.SetUpdateAt(updatedAt);

                if (Reader.IsDeleted(item))
                {
                    deletedIds.Add(id);
                }
                else
                {
                    upsertList.Add(item);
                }
            }

            if (upsertList.Any())
            {
                await Store.UpsertAsync(Table.TableName, upsertList, ignoreMissingColumns: true);
            }

            if (deletedIds.Any())
            {
                await Store.DeleteAsync(Table.TableName, deletedIds);
            }

            await _strategy.OnResultsProcessedAsync();
        }

        // follows next links in the query result and returns final result
        private async Task<QueryResult> FollowNextLinks(QueryResult result)
        {
            while (!EndOfResult(result) && // if we are not at the end of result
                    IsNextLinkValid(result.NextLink, _options)) // and there is a valid link to get more results
            {
                CancellationToken.ThrowIfCancellationRequested();

                result = await Table.ReadAsync(result.NextLink);
                await ProcessAll(result.Values); // process the results as soon as we've gotten them
            }
            return result;
        }

        // mongo doesn't support skip and top yet it generates next links with top and skip
        private bool IsNextLinkValid(Uri link, MobileServiceRemoteTableOptions options)
        {
            if (link == null)
            {
                return false;
            }

            IDictionary<string, string> parameters = HttpUtility.ParseQueryString(link.Query);

            bool isValid = ValidateOption(options, parameters, ODataOptions.Top, MobileServiceRemoteTableOptions.Top) &&
                           ValidateOption(options, parameters, ODataOptions.Skip, MobileServiceRemoteTableOptions.Skip) &&
                           ValidateOption(options, parameters, ODataOptions.OrderBy, MobileServiceRemoteTableOptions.OrderBy);

            return isValid;
        }

        private static bool ValidateOption(MobileServiceRemoteTableOptions validOptions, IDictionary<string, string> parameters, string optionKey, MobileServiceRemoteTableOptions option)
        {
            bool hasInvalidOption = parameters.ContainsKey(optionKey) && !validOptions.HasFlag(option);
            return !hasInvalidOption;
        }

        private bool EndOfResult(QueryResult result)
        {
            // if we got as many as we initially wanted 
            // or there are no more results
            // then we're at the end
            return _cursor.Complete || result.Values.Count == 0;
        }

        private async Task CreatePullStrategy()
        {
            bool isIncrementalSync = !string.IsNullOrEmpty(QueryId);
            if (isIncrementalSync)
            {
                _strategy = new IncrementalPullStrategy(Table, Query, QueryId, Settings, _cursor, _options, _pullOptions);
            }
            else
            {
                _strategy = new PullStrategy(Query, _cursor, _options, _pullOptions);
            }
            await _strategy.InitializeAsync();
        }
    }
}
