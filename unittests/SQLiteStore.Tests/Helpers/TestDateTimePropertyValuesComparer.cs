// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using Microsoft.WindowsAzure.MobileServices.Sync.Conflicts;
using SQLiteStore.Tests.Helpers;

namespace SQLiteStore.Tests
{
    public partial class SQLiteStoreIntegration
    {
        class TestDateTimePropertyValuesComparer : DateTimePropertyValuesComparer
        {
            public TestDateTimePropertyValuesComparer() : base(PropertyConflict.Comparer) { }

            protected override bool IsDateTime(in string tableName, in string propertyName) =>
                tableName == PropertyConflictsTestTable &&
                propertyName == nameof(ItemForPropertyConflicts.DateTime1);
        }
    }
}
