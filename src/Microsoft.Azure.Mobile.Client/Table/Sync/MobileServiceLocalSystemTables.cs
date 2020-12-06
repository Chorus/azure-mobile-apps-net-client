// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System.Collections.Generic;

namespace Microsoft.WindowsAzure.MobileServices.Sync
{
    /// <summary>
    /// Names of tables in local store that are reserved by sync framework
    /// </summary>
    public static class MobileServiceLocalSystemTables
    {
        /// <summary>
        /// Prefix used on system table names
        /// </summary>
        public static readonly string Prefix = "__";

        /// <summary>
        /// Name of the table that stores operation queue items
        /// </summary>
        public static readonly string OperationQueue = Prefix + "operations";

        /// <summary>
        /// Name of the table that stores sync errors
        /// </summary>
        public static readonly string SyncErrors = Prefix + "errors";

        /// <summary>
        /// Name of the table that stores configuration settings related to sync framework
        /// </summary>
        public static readonly string Config = Prefix + "config";

        /// <summary>
        /// Returns the names of all system tables
        /// </summary>
        public static IEnumerable<string> All { get; private set; }

        static MobileServiceLocalSystemTables()
        {
            All = new [] { OperationQueue, SyncErrors, Config };
        }
    }
}
