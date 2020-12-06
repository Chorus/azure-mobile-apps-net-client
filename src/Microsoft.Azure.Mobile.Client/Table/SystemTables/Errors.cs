using Microsoft.WindowsAzure.MobileServices.Sync;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Microsoft.WindowsAzure.MobileServices.Table.SystemTables
{
    class Errors : ITable
    {
        public HttpStatusCode HttpStatus { get; set; }

        public int OperationVersion { get; set; }

        public MobileServiceTableOperationKind OperationKind { get; set; }

        public string TableName { get; set; }

        public MobileServiceTableKind TableKind { get; set; }

        public string Item { get; set; }

        public string RawResult { get; set; }

        public string Id { get; set; }

        public string Version { get; set; }
    }
}
