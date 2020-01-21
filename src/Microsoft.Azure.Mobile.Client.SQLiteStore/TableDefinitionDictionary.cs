// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.WindowsAzure.MobileServices.SQLiteStore
{
    /// <summary>
    /// A class that represents the structure of table on local store
    /// </summary>
    [SuppressMessage("Microsoft.Interoperability", "CA1405:ComVisibleTypeBaseTypesShouldBeComVisible")]
    public class TableDefinitionDictionary : Dictionary<string, ColumnDefinition>
    {
        public MobileServiceSystemProperties SystemProperties { get; private set; }

        public TableDefinitionDictionary()
        {
        }

        public TableDefinitionDictionary(IDictionary<string, ColumnDefinition> definition, MobileServiceSystemProperties systemProperties)
            : base(definition, StringComparer.OrdinalIgnoreCase)
        {
            this.SystemProperties = systemProperties;
        }
    }
}
