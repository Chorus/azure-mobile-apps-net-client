using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.WindowsAzure.MobileServices
{
    public interface ITable
    {
        string Id { get; set; }

        string Version { get; set; }

        string CreatedAt { get; set; }

        string UpdatedAt { get; set; }

        bool Deleted { get; set; }
    }
}
