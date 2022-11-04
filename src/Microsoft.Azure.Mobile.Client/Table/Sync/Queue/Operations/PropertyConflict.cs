#nullable enable
using Newtonsoft.Json.Linq;
using System;
using System.Threading;

namespace Microsoft.WindowsAzure.MobileServices.Sync
{
    public class PropertyConflict
    {
        private readonly IMobileServiceUpdateOperationError _error;
        private int _handled;

        internal PropertyConflict(string propertyName, IMobileServiceUpdateOperationError error)
        {
            PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
            _error = error ?? throw new ArgumentNullException(nameof(error));

            // TODO: make sure the items are not null
            RemoteValue = _error.Result.GetValue(PropertyName);
            LocalValue = _error.Item.GetValue(PropertyName);
            BaseValue = _error.PreviousItem.GetValue(PropertyName);
            IsLocalChanged = !Equals(BaseValue, LocalValue);
            IsRemoteChanged = !Equals(BaseValue, RemoteValue);
        }

        public string PropertyName { get; }
        public JToken? RemoteValue { get; }
        public JToken? LocalValue { get; }
        public JToken? BaseValue { get; }
        public JToken? ResolvedValue { get; private set; }
        public bool Handled => _handled != 0;
        public bool IsLocalChanged { get; }
        public bool IsRemoteChanged { get; }

        public void TakeRemote()
        {
            SetHandled();
            ResolvedValue = RemoteValue;
        }

        public void TakeLocal()
        {
            SetHandled();
            ResolvedValue = LocalValue;
        }

        public void UpdateValue(JToken? newValue)
        {
            SetHandled();
            ResolvedValue = newValue;
        }

        private void SetHandled()
        {
            if (Interlocked.Exchange(ref _handled, 1) != 0)
            {
                throw new InvalidOperationException("This conflict has already been handled");
            }
        }
    }
}
