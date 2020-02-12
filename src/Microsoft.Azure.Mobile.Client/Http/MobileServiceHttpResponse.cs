// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using Newtonsoft.Json.Linq;
using Microsoft.Azure.MobileServices.Table.Query;

namespace Microsoft.Azure.MobileServices
{
    internal class MobileServiceHttpResponse
    {
        public string ContentString { get; private set; }

        public OdataResult ContentObject { get; set; }

        public string Etag { get; private set; }

        public LinkHeaderValue Link { get; private set; }

        public MobileServiceHttpResponse(string content, string etag, LinkHeaderValue link)
        {
            ContentString = content;
            Etag = etag;
            Link = link;
        }

        public MobileServiceHttpResponse(OdataResult content, string etag, LinkHeaderValue link)
        {
            ContentObject = content;
            Etag = etag;
            Link = link;
        }
    }
}
