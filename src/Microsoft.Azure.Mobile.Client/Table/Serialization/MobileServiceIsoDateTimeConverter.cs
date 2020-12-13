// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.

using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.WindowsAzure.MobileServices
{
    /// <summary>
    /// Converts DateTime and DateTimeOffset object into UTC DateTime and creates a ISO string representation
    /// by calling ToUniversalTime on serialization and ToLocalTime on deserialization.
    /// </summary>
    public class MobileServiceIsoDateTimeConverter : JsonConverter<DateTime>
    {
        public static string DateTimeFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffK";

        public override DateTime Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) =>
            DateTime.ParseExact(reader.GetString(),
                DateTimeFormat, CultureInfo.InvariantCulture)
            .ToLocalTime();

        public override void Write(
            Utf8JsonWriter writer,
            DateTime value,
            JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToUniversalTime()
                .ToString(DateTimeFormat, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Converts DateTime and DateTimeOffset object into UTC DateTime and creates a ISO string representation
    /// by calling ToUniversalTime on serialization and ToLocalTime on deserialization.
    /// </summary>
    public class MobileServiceIsoDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
    {
        public static string DateTimeFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffK";

        public override DateTimeOffset Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) =>
            DateTimeOffset.ParseExact(reader.GetString(),
                DateTimeFormat, CultureInfo.InvariantCulture)
            .ToLocalTime();

        public override void Write(
            Utf8JsonWriter writer,
            DateTimeOffset value,
            JsonSerializerOptions options) =>
            writer.WriteStringValue(value.UtcDateTime
                .ToString(DateTimeFormat, CultureInfo.InvariantCulture));
    }
}
