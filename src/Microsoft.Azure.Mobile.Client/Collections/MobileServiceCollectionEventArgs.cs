using System;

namespace Microsoft.Azure.MobileServices
{
    /// <summary>
    /// Represents the loaded status changed event arguments.
    /// </summary>
    public class LoadingCompleteEventArgs : EventArgs
    {
        /// <summary>
        /// Provides how many items were loaded.
        /// </summary>
        public int TotalItemsLoaded { get; set; }
    }
}
