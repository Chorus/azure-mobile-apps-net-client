// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System;

namespace Microsoft.Azure.MobileServices.Threading
{
    internal struct DisposeAction : IDisposable
    {
        private bool _isDisposed;
        private readonly Action _action;

        public DisposeAction(Action action)
        {
            _isDisposed = false;
            _action = action;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                _action();
            }
        }
    }
}
