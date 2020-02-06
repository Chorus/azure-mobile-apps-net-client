// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Threading
{
    internal class AsyncReaderWriterLock
    {
        private readonly Task<DisposeAction> _readerReleaser;
        private readonly Task<DisposeAction> _writerReleaser;
        private readonly Queue<TaskCompletionSource<DisposeAction>> _waitingWriters = new Queue<TaskCompletionSource<DisposeAction>>(); 
        private TaskCompletionSource<DisposeAction> _waitingReader = new TaskCompletionSource<DisposeAction>(); 
        private int readersWaiting;

        private int lockStatus; // -1 means write lock, >=0 no. of read locks

        public AsyncReaderWriterLock()
        {
            _readerReleaser = Task.FromResult(new DisposeAction(ReaderRelease));
            _writerReleaser = Task.FromResult(new DisposeAction(WriterRelease)); 
        }

        public Task<DisposeAction> ReaderLockAsync() 
        {
            lock (this._waitingWriters) 
            {
                bool hasPendingReaders = lockStatus >= 0;
                bool hasNoPendingWritiers = _waitingWriters.Count == 0;
                if (hasPendingReaders && hasNoPendingWritiers) 
                {
                    ++lockStatus;
                    return _readerReleaser; 
                } 
                else 
                {
                    ++readersWaiting;
                    return _waitingReader.Task.ContinueWith(t => t.Result);
                } 
            } 
        }

        public Task<DisposeAction> WriterLockAsync() 
        {
            lock (this._waitingWriters) 
            {
                bool hasNoPendingReaders = this.lockStatus == 0;
                if (hasNoPendingReaders) 
                {
                    this.lockStatus = -1;
                    return this._writerReleaser; 
                } 
                else 
                { 
                    var waiter = new TaskCompletionSource<DisposeAction>();
                    this._waitingWriters.Enqueue(waiter); 
                    return waiter.Task; 
                } 
            } 
        }

        private void ReaderRelease() 
        { 
            TaskCompletionSource<DisposeAction> toWake = null;

            lock (this._waitingWriters) 
            {
                --this.lockStatus;
                if (this.lockStatus == 0 && this._waitingWriters.Count > 0) 
                {
                    this.lockStatus = -1;
                    toWake = this._waitingWriters.Dequeue(); 
                } 
            }

            if (toWake != null) 
            {
                toWake.SetResult(new DisposeAction(this.WriterRelease)); 
            }
        }
        private void WriterRelease()
        {
            TaskCompletionSource<DisposeAction> toWake = null;
            Action wakeupAction = this.ReaderRelease;

            lock (this._waitingWriters)
            {
                if (this._waitingWriters.Count > 0)
                {
                    toWake = this._waitingWriters.Dequeue();
                    wakeupAction = this.WriterRelease;
                }
                else if (this.readersWaiting > 0)
                {
                    toWake = this._waitingReader;
                    this.lockStatus = this.readersWaiting;
                    this.readersWaiting = 0;
                    this._waitingReader = new TaskCompletionSource<DisposeAction>();
                }
                else
                {
                    this.lockStatus = 0;
                }
            }

            if (toWake != null)
            {
                toWake.SetResult(new DisposeAction(wakeupAction));
            }
        }
    }
}
