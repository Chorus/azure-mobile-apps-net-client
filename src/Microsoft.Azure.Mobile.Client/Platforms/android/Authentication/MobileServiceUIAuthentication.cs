using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Android.Content;
using Xamarin.Auth._MobileServices;

namespace Microsoft.Azure.MobileServices
{
    internal class MobileServiceUIAuthentication : MobileServicePKCEAuthentication
    {
        public MobileServiceUIAuthentication (Context context, MobileServiceClient client, string providerName, string uriScheme, IDictionary<string, string> parameters)
            : base (client, providerName, uriScheme, parameters)
        {
            this.context = context;
        }

        private Context context;
        
        internal static WebAuthenticator CurrentAuthenticator;

        protected override Task<string> GetAuthorizationCodeAsync()
        {
            var tcs = new TaskCompletionSource<string>();

            if (CurrentAuthenticator != null)
            {
                tcs.TrySetException(new InvalidOperationException("Authentication is already in progress."));
                return tcs.Task;
            }
            
            CurrentAuthenticator = new WebRedirectAuthenticator (LoginUri, CallbackUri)
            {
                IsUsingNativeUI = true, // Xamarin.Auth takes care about fallback
                ClearCookiesBeforeLogin = false
            };

            Intent intent = CurrentAuthenticator.GetUI (this.context);

            CurrentAuthenticator.Error += (sender, e) =>
            {
                string message = String.Format (CultureInfo.InvariantCulture, "Authentication failed with HTTP response code {0}.", e.Message);
                InvalidOperationException ex = (e.Exception == null)
                    ? new InvalidOperationException (message)
                    : new InvalidOperationException (message, e.Exception);

                tcs.TrySetException (ex);
                CurrentAuthenticator = null;
            };

            CurrentAuthenticator.Completed += (sender, e) =>
            {
                string authorizationCode;
                if (!e.IsAuthenticated)
                {
                    tcs.TrySetException(new InvalidOperationException ("Authentication was cancelled by the user."));
                }
                else if (!e.Account.Properties.TryGetValue("authorization_code", out authorizationCode))
                {
                    tcs.TrySetException(new InvalidOperationException("Authentication failed: could not found \"authorization_code\"."));
                }
                else
                {
                    tcs.TrySetResult(authorizationCode);
                }
                CurrentAuthenticator = null;
            };

            context.StartActivity (intent);
            
            return tcs.Task;
        }
    }
}