// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Microsoft.WindowsAzure.MobileServices
{
    internal class MobileServiceHttpResponse<T>
    {
        public T Content { get; private set; }

        public string Etag { get; private set; }

        public LinkHeaderValue Link { get; private set; }

        public MobileServiceHttpResponse(T content, string etag, LinkHeaderValue link)
        {
            Content = content;
            Etag = etag;
            Link = link;
        }
    }

    public class ODataResponse<T> 
    {
        [JsonPropertyName("@odata.context")]
        public string Context { get; set; }

        [JsonPropertyName("value")]
        public T[] Value { get; set; }


    }
}
