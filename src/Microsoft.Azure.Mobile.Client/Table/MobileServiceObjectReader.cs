// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System;
using System.Text.Json;

namespace Microsoft.WindowsAzure.MobileServices
{
    internal class MobileServiceObjectReader
    {
        public string VersionPropertyName { get; set; }
        public string DeletedPropertyName { get; set; }
        public string UpdatedAtPropertyName { get; set; }
        public string IdPropertyName { get; set; }
        public string CreatedAtPropertyName { get; set; }

        public MobileServiceObjectReader()
        {
            VersionPropertyName = MobileServiceSystemColumns.Version;
            DeletedPropertyName = MobileServiceSystemColumns.Deleted;
            UpdatedAtPropertyName = MobileServiceSystemColumns.UpdatedAt;
            IdPropertyName = MobileServiceSystemColumns.Id;
            CreatedAtPropertyName = MobileServiceSystemColumns.CreatedAt;
        }

        public string GetVersion(JsonElement item)
        {
            return item.GetProperty(VersionPropertyName).GetString();
        }

        public string GetId(JsonElement item)
        {
            return item.GetProperty(IdPropertyName).GetString();
        }

        public bool IsDeleted(JsonElement item)
        {
            var deletedElement = item.GetProperty(DeletedPropertyName);
            bool isDeleted = deletedElement.GetBoolean();
            return isDeleted;
        }

        public DateTimeOffset? GetUpdatedAt(JsonElement item)
        {
            return GetDateTimeOffset(item, UpdatedAtPropertyName);
        }

        public DateTimeOffset? GetCreatedAt(JsonElement item)
        {
            return GetDateTimeOffset(item, CreatedAtPropertyName);
        }

        private static DateTimeOffset? GetDateTimeOffset(JsonElement item, string name)
        {
            return item.GetProperty(name).GetDateTimeOffset();
        }
    }
}
