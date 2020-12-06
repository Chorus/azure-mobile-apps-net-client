using Microsoft.WindowsAzure.MobileServices.Sync;
using System;
using System.Text.Json.Serialization;

namespace Microsoft.WindowsAzure.MobileServices.Table.SystemTables
{
    public class Config : ITable
    {
        public Config(string id, string value)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public string Id { get; set; }

        public string Version { get; set; }

        public string Value { get; set; }
    }
}
