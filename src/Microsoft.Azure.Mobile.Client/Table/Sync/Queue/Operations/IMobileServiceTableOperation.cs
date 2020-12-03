// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Sync
{
    /// <summary>
    /// An object representing table operation against remote table
    /// </summary>
    public interface IMobileServiceTableOperation<T> where T : ITable
    {
        /// <summary>
        /// The kind of operation
        /// </summary>
        MobileServiceTableOperationKind Kind { get; }

        /// <summary>
        /// The state of the operation
        /// </summary>
        MobileServiceTableOperationState State { get; }

        /// <summary>
        /// The table that the operation will be executed against.
        /// </summary>
        IMobileServiceTable<T> Table { get; }

        /// <summary>
        /// The item associated with the operation.
        /// </summary>
        T Item { get; set; }

        /// <summary>
        /// Executes the operation against remote table.
        /// </summary>
        Task<T> ExecuteAsync();

        /// <summary>
        /// Abort the parent push operation.
        /// </summary>
        void AbortPush();
    }
}
