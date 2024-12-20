﻿#nullable enable
using Newtonsoft.Json.Linq;
using System;
using System.Threading;

namespace Microsoft.WindowsAzure.MobileServices.Sync.Conflicts
{
    public class PropertyConflict : IPropertyConflict
    {
        public static IPropertyValuesComparer Comparer = new DefaultPropertyValuesComparer();

        private readonly IMobileServiceUpdateOperationError _error;
        private int _handled;

        internal PropertyConflict(in string propertyName, IMobileServiceUpdateOperationError error)
        {
            PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
            TableName = error.TableName;
            _error = error ?? throw new ArgumentNullException(nameof(error));

            _ = error.Result ?? throw new ArgumentException($"{nameof(error)}.{nameof(error.Result)} should not be null", nameof(error));
            _ = error.Item ?? throw new ArgumentException($"{nameof(error)}.{nameof(error.Item)} should not be null", nameof(error));
            _ = error.PreviousItem ?? throw new ArgumentException($"{nameof(error)}.{nameof(error.PreviousItem)} should not be null", nameof(error));

            var remoteValueJToken = _error.Result.GetValue(PropertyName);
            RemoteValue = remoteValueJToken is null or JValue ?
                (JValue?)remoteValueJToken :
                throw new InvalidOperationException($"Remote value is an object or array which is not supported. Only primitive values are supported.");

            var localValueJToken = _error.Item.GetValue(PropertyName);
            LocalValue = localValueJToken is null or JValue ?
                (JValue?)localValueJToken :
                throw new InvalidOperationException($"Local value is an object or array which is not supported. Only primitive values are supported.");

            var baseValueJToken = _error.PreviousItem.GetValue(PropertyName);
            BaseValue = baseValueJToken is null or JValue ?
                (JValue?)baseValueJToken :
                throw new InvalidOperationException($"Base value is an object or array which is not supported. Only primitive values are supported.");

            IsLocalChanged = !AreValuesEqual(BaseValue, LocalValue);
            IsRemoteChanged = !AreValuesEqual(BaseValue, RemoteValue);
        }

        public string PropertyName { get; }
        public string TableName { get; }
        public bool IsLocalChanged { get; }
        public bool IsRemoteChanged { get; }

        public JValue? RemoteValue { get; }
        public JValue? LocalValue { get; }
        public JValue? BaseValue { get; }

        public bool Handled => _handled != 0;
        public bool IsBaseTaken => AreValuesEqual(ResolvedValue, BaseValue);
        public bool IsLocalTaken => AreValuesEqual(ResolvedValue, LocalValue);
        public bool IsRemoteTaken => AreValuesEqual(ResolvedValue, RemoteValue);
        public bool LocalEqualsRemote => AreValuesEqual(LocalValue, RemoteValue);
        public JValue? ResolvedValue { get; private set; }

        private static IPropertyValuesComparer GetComparer() =>
            Comparer ?? throw new InvalidOperationException($"{nameof(PropertyConflict)}.{nameof(Comparer)} has to be set.");

        private bool AreValuesEqual(JValue? value1, JValue? value2) =>
            GetComparer().AreValuesEqual(TableName, PropertyName, value1, value2);

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

        public void UpdateValue(JValue? newValue)
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
