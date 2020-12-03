// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Sync
{
    internal class InsertOperation : MobileServiceTableOperation
    {
        public override MobileServiceTableOperationKind Kind
        {
            get { return MobileServiceTableOperationKind.Insert; }
        }

        public InsertOperation(string tableName, MobileServiceTableKind tableKind, string itemId)
            : base(tableName, tableKind, itemId)
        {
        }

        protected override Task<ITable> OnExecuteAsync()
        {
            // for insert operations version should not be sent so strip it out
            //todo fix
            //var item = MobileServiceSerializer.RemoveSystemProperties(this.Item, out _);
            return this.Table.InsertAsync(Item);
        }

        public override void Validate(MobileServiceTableOperation newOperation)
        {
            if (newOperation.ItemId != ItemId)
            {
                throw new ArgumentException("ItemId does not match", nameof(newOperation));
            }

            if (newOperation is InsertOperation)
            {
                throw new InvalidOperationException("An insert operation on the item is already in the queue.");
            }

            if (newOperation is DeleteOperation && this.State != MobileServiceTableOperationState.Pending)
            {
                // if insert was attempted then we can't be sure if it went through or not hence we can't collapse delete
                throw new InvalidOperationException("The item is in inconsistent state in the local store. Please complete the pending sync by calling PushAsync() before deleting the item.");
            }
        }

        public override void Collapse(MobileServiceTableOperation<T> newOperation)
        {
            if (newOperation.ItemId != ItemId)
            {
                throw new ArgumentException("ItemId does not match", nameof(newOperation));
            }

            if (newOperation is DeleteOperation)
            {
                this.Cancel();
                newOperation.Cancel();
            }
            else if (newOperation is UpdateOperation)
            {
                this.Update();
                newOperation.Cancel();
            }
        }

        public override async Task ExecuteLocalAsync(IMobileServiceLocalStore store, ITable item)
        {
            if (await store.LookupAsync(this.TableName, this.ItemId) != null)
            {
                throw new MobileServiceLocalStoreException("An insert operation on the item is already in the queue.", null);
            }

            await store.UpsertAsync(this.TableName, item, fromServer: false);
        }
    }
}
