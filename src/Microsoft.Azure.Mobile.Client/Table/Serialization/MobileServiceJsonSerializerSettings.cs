// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.WindowsAzure.MobileServices
{
    /// <summary>
    /// JSON serializer settings to use with a <see cref="MobileServiceClient"/>.
    /// </summary>
    public class MobileServiceJsonSerializerSettings
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions;

        public JsonSerializerOptions SerializerOptions => _jsonSerializerOptions;

        /// <summary>
        /// Initializes a new instance of the MobileServiceJsonSerializerSettings
        /// class.
        /// </summary>
        public MobileServiceJsonSerializerSettings()
        {
            _jsonSerializerOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                Converters = 
                {
                    new MobileServiceIsoDateTimeConverter(),
                    new MobileServiceIsoDateTimeOffsetConverter(),
                }
            };
              
            //this.ContractResolver = new MobileServiceContractResolver();
            //this.ObjectCreationHandling = ObjectCreationHandling.Replace;
            //this.Converters.Add(new MobileServicePrecisionCheckConverter());
            //this.Converters.Add(new StringEnumConverter());
        }
    }
}
