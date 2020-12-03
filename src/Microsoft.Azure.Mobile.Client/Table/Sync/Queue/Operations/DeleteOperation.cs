﻿// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Sync
{
    internal class DeleteOperation : MobileServiceTableOperation
    {
        public override MobileServiceTableOperationKind Kind => MobileServiceTableOperationKind.Delete;

        public override bool CanWriteResultToStore => false;

        protected override bool SerializeItemToQueue => true;

        public DeleteOperation(string tableName, MobileServiceTableKind tableKind, string itemId)
            : base(tableName, tableKind, itemId)
        {
        }

        protected override async Task<ITable> OnExecuteAsync()
        {
            try
            {
                return await this.Table.DeleteAsync(Item);
            }
            catch (MobileServiceInvalidOperationException ex)
            {
                // if the item is already deleted then local store is in-sync with the server state
                if (ex.Response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
                throw;
            }
        }

        public override void Validate(MobileServiceTableOperation newOperation)
        {
            if (newOperation.ItemId != ItemId)
            {
                throw new ArgumentException("ItemId does not match", nameof(newOperation));
            }

            // we don't allow any more operations on object that has already been deleted
            throw new InvalidOperationException("A delete operation on the item is already in the queue.");
        }

        public override void Collapse(MobileServiceTableOperation other)
        {
            // nothing to collapse we don't allow any operation after delete
        }

        public override Task ExecuteLocalAsync(IMobileServiceLocalStore store, ITable item)
        {
            return store.DeleteAsync(TableName, ItemId);
        }
    }
}
