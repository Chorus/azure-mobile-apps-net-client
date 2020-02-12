// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System;

namespace Microsoft.Azure.MobileServices.SQLiteStore
{
    public class SQLiteException : Exception
    {
        public SQLiteException()
        {
        }

        public SQLiteException(string message)
            : base(message)
        {
        }

        public SQLiteException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
