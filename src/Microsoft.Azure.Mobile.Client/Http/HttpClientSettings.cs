using System;

namespace Microsoft.WindowsAzure.MobileServices.Http
{
    public class HttpClientSettings
    {
        /// <summary>
        /// Constructor with passing in a timeout
        /// </summary>
        /// <param name="timeout">the amount of time before a request times out</param>
        public HttpClientSettings(TimeSpan? timeout = null)
        {
            Timeout = timeout ?? TimeSpan.FromMinutes(1);
        }

        /// <summary>
        /// The timeout for all http requests
        /// </summary>
        public TimeSpan Timeout { get; }
    }
}
