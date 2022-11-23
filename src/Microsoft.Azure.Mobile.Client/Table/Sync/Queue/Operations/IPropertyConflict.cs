#nullable enable
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Sync
{
    public interface IPropertyConflict
    {
        JValue? BaseValue { get; }
        bool Handled { get; }
        bool IsLocalChanged { get; }
        bool IsRemoteChanged { get; }
        JValue? LocalValue { get; }
        string PropertyName { get; }
        JValue? RemoteValue { get; }
        JValue? ResolvedValue { get; }
        bool LocalEqualsRemote { get; }

        void TakeLocal();
        void TakeRemote();
        void UpdateValue(JValue? newValue);
    }
}
