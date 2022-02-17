using System;

namespace Microsoft.WindowsAzure.MobileServices
{
    /// <summary>
    /// List of custom client options, for creating mobile clients
    /// </summary>
    public interface IMobileServiceClientOptions2 : IMobileServiceClientOptions
    {
        /// <summary>
        /// Timeout for all HTTP requests
        /// </summary>
        TimeSpan? HttpRequestTimeout { get; }
    }
}
