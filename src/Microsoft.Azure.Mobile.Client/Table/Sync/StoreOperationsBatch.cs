﻿// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.MobileServices.Sync
{
    /// <summary>
    /// Represents a series of operations that happened within the context of a single action such as a
    /// server push or pull.
    /// </summary>
    public sealed class StoreOperationsBatch
    {
        private string batchId;
        private StoreOperationSource source;
        private Dictionary<LocalStoreOperationKind, int> operationsCountByType;
        private SemaphoreSlim operationsCountSemaphore = new SemaphoreSlim(1);

        public StoreOperationsBatch(string batchId, StoreOperationSource source)
        {
            this.batchId = batchId;
            this.source = source;
            this.operationsCountByType = new Dictionary<LocalStoreOperationKind, int>();
        }

        /// <summary>
        /// The ID of the batch this operation belongs to.
        /// </summary>
        public string BatchId => batchId;

        /// <summary>
        /// Describes the source this operation was triggered from.
        /// </summary>
        public StoreOperationSource Source => source;

        /// <summary>
        /// The number of operations executed within this batch.
        /// </summary>
        public int OperationCount => operationsCountByType.Sum(kvp => kvp.Value);

        /// <summary>
        /// Gets the number of operations matching the provided operation kind executed within this batch.
        /// </summary>
        /// <param name="operationKind">The kind of operation.</param>
        /// <returns>The number of operations matching the provided count.</returns>
        public int GetOperationCountByKind(LocalStoreOperationKind operationKind) =>
            operationsCountByType.ContainsKey(operationKind)
            ? operationsCountByType[operationKind]
            : 0;

        internal async Task IncrementOperationCount(LocalStoreOperationKind operationKind)
        {
            try
            {
                await operationsCountSemaphore.WaitAsync();

                if (!operationsCountByType.ContainsKey(operationKind))
                {
                    operationsCountByType.Add(operationKind, 1);
                }
                else
                {
                    operationsCountByType[operationKind]++;
                }
            }
            finally
            {
                operationsCountSemaphore.Release();
            }
        }
    }
}
