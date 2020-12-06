using Microsoft.WindowsAzure.MobileServices.Sync;

namespace Microsoft.WindowsAzure.MobileServices.Table.SystemTables
{
    public class Operations : ITable
    {
        public string Id { get; set; }

        public string Version { get; set; }

        public MobileServiceTableOperationKind Kind { get; }

        internal MobileServiceTableKind TableKind { get; private set; }

        public string TableName { get; private set; }

        public string ItemId { get; private set; }

        public ITable Item { get; set; }

        public MobileServiceTableOperationState State { get; internal set; }

        public long Sequence { get; set; }
    }
}
