// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Threading
{
    internal sealed class AsyncLock : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(initialCount: 1, maxCount: 1);

        public async Task<IDisposable> Acquire(CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken)
                                .ConfigureAwait(continueOnCapturedContext: false);

            return new DisposeAction(() => _semaphore.Release());
        }

        public void Dispose()
        {
            _semaphore.Dispose();
        }
    }
}
