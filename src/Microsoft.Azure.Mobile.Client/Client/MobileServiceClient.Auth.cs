using Microsoft.WindowsAzure.MobileServices.Internal;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices
{
    public partial class MobileServiceClient : IMobileServiceClient
    {
        /// <summary>
        /// Logs a user into a Windows Azure Mobile Service with the provider and optional token object.
        /// </summary>
        /// <param name="provider">
        /// Authentication provider to use.
        /// </param>
        /// <param name="token">
        /// Provider specific object with existing OAuth token to log in with.
        /// </param>
        /// <remarks>
        /// The token object needs to be formatted depending on the specific provider. These are some
        /// examples of formats based on the providers:
        /// <list type="bullet">
        ///   <item>
        ///     <term>MicrosoftAccount</term>
        ///     <description><code>{"authenticationToken":"&lt;the_authentication_token&gt;"}</code></description>
        ///   </item>
        ///   <item>
        ///     <term>Facebook</term>
        ///     <description><code>{"access_token":"&lt;the_access_token&gt;"}</code></description>
        ///   </item>
        ///   <item>
        ///     <term>Google</term>
        ///     <description><code>{"access_token":"&lt;the_access_token&gt;"}</code></description>
        ///   </item>
        /// </list>
        /// </remarks>
        /// <returns>
        /// Task that will complete when the user has finished authentication.
        /// </returns>
        public Task<MobileServiceUser> LoginAsync(MobileServiceAuthenticationProvider provider, MobileServiceToken token)
        {
            if (!Enum.IsDefined(typeof(MobileServiceAuthenticationProvider), provider))
            {
                throw new ArgumentOutOfRangeException(nameof(provider));
            }
            return this.LoginAsync(provider.ToString(), token);
        }

        /// <summary>
        /// Logs a user into a Microsoft Azure Mobile Service with the provider and optional token object.
        /// </summary>
        /// <param name="provider">
        /// Authentication provider to use.
        /// </param>
        /// <param name="token">
        /// Provider specific object with existing OAuth token to log in with.
        /// </param>
        /// <remarks>
        /// The token object needs to be formatted depending on the specific provider. These are some
        /// examples of formats based on the providers:
        /// <list type="bullet">
        ///   <item>
        ///     <term>MicrosoftAccount</term>
        ///     <description><code>{"authenticationToken":"&lt;the_authentication_token&gt;"}</code></description>
        ///   </item>
        ///   <item>
        ///     <term>Facebook</term>
        ///     <description><code>{"access_token":"&lt;the_access_token&gt;"}</code></description>
        ///   </item>
        ///   <item>
        ///     <term>Google</term>
        ///     <description><code>{"access_token":"&lt;the_access_token&gt;"}</code></description>
        ///   </item>
        /// </list>
        /// </remarks>
        /// <returns>
        /// Task that will complete when the user has finished authentication.
        /// </returns>
        public Task<MobileServiceUser> LoginAsync(string provider, MobileServiceToken token)
        {
            Arguments.IsNotNull(token, nameof(token));

            MobileServiceTokenAuthentication auth = new MobileServiceTokenAuthentication(this, provider, token, parameters: null);
            return auth.LoginAsync();
        }

        /// <summary>
        /// Log a user out.
        /// </summary>
        public Task LogoutAsync()
        {
            this.CurrentUser = null;
            return Task.FromResult(0);
        }

        /// <summary>
        /// Refreshes access token with the identity provider for the logged in user.
        /// </summary>
        /// <returns>
        /// Task that will complete when the user has finished refreshing access token
        /// </returns>
        public async Task<MobileServiceUser> RefreshUserAsync()
        {
            if (CurrentUser == null || string.IsNullOrEmpty(CurrentUser.MobileServiceAuthenticationToken))
            {
                throw new InvalidOperationException("MobileServiceUser must be set before calling refresh");
            }

            MobileServiceHttpClient client = HttpClient;
            if (AlternateLoginHost != null)
            {
                client = AlternateAuthHttpClient;
            }
            MobileServiceToken response;
            try
            {
                response = await client.RequestWithoutHandlersAsync<MobileServiceToken>(
                    HttpMethod.Get, 
                    RefreshUserAsyncUriFragment, 
                    CurrentUser, 
                    null, 
                    MobileServiceFeatures.RefreshToken);
            }
            catch (MobileServiceInvalidOperationException ex)
            {
                if (ex.Response != null)
                {
                    string message = ex.Response.StatusCode switch
                    {
                        HttpStatusCode.BadRequest => "Refresh failed with a 400 Bad Request error. The identity provider does not support refresh, or the user is not logged in with sufficient permission.",
                        HttpStatusCode.Unauthorized => "Refresh failed with a 401 Unauthorized error. Credentials are no longer valid.",
                        HttpStatusCode.Forbidden => "Refresh failed with a 403 Forbidden error. The refresh token was revoked or expired.",
                        _ => "Refresh failed due to an unexpected error.",
                    };
                    throw new MobileServiceInvalidOperationException(message, innerException: ex, request: ex.Request, response: ex.Response);
                }
                throw;
            }
            if (!string.IsNullOrEmpty(response.AuthenticationToken))
            {
                CurrentUser.MobileServiceAuthenticationToken = response.AuthenticationToken;
            }

            return CurrentUser;
        }
    }
}
