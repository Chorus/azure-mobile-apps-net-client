// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using Newtonsoft.Json;

namespace Microsoft.WindowsAzure.MobileServices
{
    //internal class MobileServiceHttpResponse : MobileServiceHttpResponse<string>
    //{
    //    public MobileServiceHttpResponse(string content, string etag, LinkHeaderValue link)
    //        : base(content, etag, link)
    //    {

    //    }
    //}

    internal class MobileServiceHttpResponse<T>
    {
        public ODataResponse<T> Content { get; private set; }

        public string Etag { get; private set; }

        public LinkHeaderValue Link { get; private set; }

        public MobileServiceHttpResponse(ODataResponse<T> content, string etag, LinkHeaderValue link)
        {
            Content = content;
            Etag = etag;
            Link = link;
        }
    }

    public class ODataResponse<T> 
    {
        [JsonProperty("@odata.context")]
        public string Context { get; set; }

        [JsonProperty("value")]
        public T[] Value { get; set; }


    }
}
