// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Microsoft.WindowsAzure.MobileServices
{
    /// <summary>
    /// Provides serialization and deserialization for a 
    /// <see cref="MobileServiceClient"/>.
    /// </summary>
    internal class MobileServiceSerializer
    {
        /// <summary>
        /// The version system property as a string
        /// </summary>
        internal static readonly string VersionSystemPropertyString = MobileServiceSystemProperties.Version.ToString().ToLowerInvariant();

        /// <summary>
        /// The version system property as a string
        /// </summary>
        internal static readonly string UpdatedAtSystemPropertyString = MobileServiceSystemProperties.UpdatedAt.ToString().ToLowerInvariant();

        /// <summary>
        /// The version system property as a string
        /// </summary>
        internal static readonly string CreatedAtSystemPropertyString = MobileServiceSystemProperties.CreatedAt.ToString().ToLowerInvariant();

        /// <summary>
        /// The deleted system property as a string
        /// </summary>
        internal static readonly string DeletedSystemPropertyString = MobileServiceSystemProperties.Deleted.ToString().ToLowerInvariant();

        /// <summary>
        /// A regex for validating string ids
        /// </summary>
        private static readonly Regex stringIdValidationRegex = new Regex(@"([\u0000-\u001F]|[\u007F-\u009F]|[""\+\?\\\/\`]|^\.{1,2}$)");

        /// <summary>
        /// The long type.
        /// </summary>
        private static readonly Type longType = typeof(long);

        /// <summary>
        /// The int type.
        /// </summary>
        private static readonly Type intType = typeof(int);

        /// <summary>
        /// The max length of valid string ids.
        /// </summary>
        internal const int MaxStringIdLength = 255;

        /// <summary>
        /// The JSON serializer settings to use with the 
        /// <see cref="MobileServiceSerializer"/>.
        /// </summary>
        public MobileServiceJsonSerializerSettings SerializerSettings { get; set; }

        /// <summary>
        /// Creates a new instance of the <see cref="MobileServiceSerializer"/>
        /// class.
        /// </summary>
        public MobileServiceSerializer()
        {
            SerializerSettings = new MobileServiceJsonSerializerSettings();
        }
    
    }
}
