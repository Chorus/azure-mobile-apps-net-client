// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace System
{
    internal static class StringExtensions
    {
        public static bool IsNullOrEmpty(this string value) => 
            string.IsNullOrEmpty(value);

        public static bool IsNullOrWhiteSpace(this string value) =>
            string.IsNullOrWhiteSpace(value);

        public static string FormatInvariant(this string format, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, format, args);
        }

        /// <summary>
        /// Parses the content into a JToken.
        /// If the content is null or empty, null will be returned.
        /// </summary>
        /// <param name="content">The content to parse.</param>
        /// <param name="settings">The serializer settings used for parsing the content.</param>
        /// <returns>A JToken containing the content or null.</returns>
        public static JToken ParseToJToken(this string content, JsonSerializerSettings settings)
        {
            if (String.IsNullOrEmpty(content))
            {
                return null;
            }
            return JsonConvert.DeserializeObject<JToken>(content, settings);            
        }
    }
}

