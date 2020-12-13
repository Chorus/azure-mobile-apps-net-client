// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Sync
{
    /// <summary>
    /// An object representing table operation against remote table
    /// </summary>
    public interface IMobileServiceTableOperation
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
        IMobileServiceTable<ITable> Table { get; }

        /// <summary>
        /// The item associated with the operation.
        /// </summary>
        ITable Item { get; set; }

        /// <summary>
        /// Executes the operation against remote table.
        /// </summary>
        Task<ITable> ExecuteAsync();

        /// <summary>
        /// Abort the parent push operation.
        /// </summary>
        void AbortPush();
    }
}
