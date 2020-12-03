using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices
{
    public partial class MobileServiceClient : IMobileServiceClient
    {
        /// <summary>
        /// Invokes a user-defined custom API of a Microsoft Azure Mobile Service using an HTTP POST.
        /// </summary>
        /// <typeparam name="T">The type of instance returned from the Microsoft Azure Mobile Service.</typeparam>
        /// <param name="apiName">The name of the custom API.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> token to observe</param>
        /// <returns>The response content from the custom api invocation.</returns>
        public Task<T> InvokeApiAsync<T>(string apiName, CancellationToken cancellationToken = default)
        {
            return InvokeApiAsync<string, T>(apiName, null, null, null, cancellationToken);
        }

        /// <summary>
        /// Invokes a user-defined custom API of a Microsoft Azure Mobile Service using an HTTP POST with
        /// support for sending HTTP content.
        /// </summary>
        /// <typeparam name="T">The type of instance sent to the Microsoft Azure Mobile Service.</typeparam>
        /// <typeparam name="U">The type of instance returned from the Microsoft Azure Mobile Service.</typeparam>
        /// <param name="apiName">The name of the custom API.</param>
        /// <param name="body">The value to be sent as the HTTP body.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> token to observe</param>
        /// <returns>The response content from the custom api invocation.</returns>
        public Task<U> InvokeApiAsync<T, U>(string apiName, T body, CancellationToken cancellationToken = default)
        {
            return this.InvokeApiAsync<T, U>(apiName, body, null, null, cancellationToken);
        }

        /// <summary>
        /// Invokes a user-defined custom API of a Microsoft Azure Mobile Service using the specified HTTP Method.
        /// Additional data can be passed using the query string.
        /// </summary>
        /// <typeparam name="T">The type of instance sent to the Microsoft Azure Mobile Service.</typeparam>
        /// <param name="apiName">The name of the custom API.</param>
        /// <param name="method">The HTTP method.</param>
        /// <param name="parameters">
        /// A dictionary of user-defined parameters and values to include in the request URI query string.
        /// </param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> token to observe</param>
        /// <returns>The response content from the custom api invocation.</returns>
        public Task<T> InvokeApiAsync<T>(string apiName, HttpMethod method, IDictionary<string, string> parameters, CancellationToken cancellationToken = default)
        {
            return this.InvokeApiAsync<string, T>(apiName, null, method, parameters, cancellationToken);
        }

        /// <summary>
        /// Invokes a user-defined custom API of a Microsoft Azure Mobile Service using the specified HTTP Method.
        /// Additional data can be sent though the HTTP content or the query string.
        /// </summary>
        /// <typeparam name="T">The type of instance sent to the Microsoft Azure Mobile Service.</typeparam>
        /// <typeparam name="U">The type of instance returned from the Microsoft Azure Mobile Service.</typeparam>
        /// <param name="apiName">The name of the custom API.</param>
        /// <param name="body">The value to be sent as the HTTP body.</param>
        /// <param name="method">The HTTP method.</param>
        /// <param name="parameters">
        /// A dictionary of user-defined parameters and values to include in the request URI query string.
        /// </param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> token to observe</param>
        /// <returns>The response content from the custom api invocation.</returns>
        public async Task<U> InvokeApiAsync<T, U>(
            string apiName, 
            T body, 
            HttpMethod method, 
            IDictionary<string, string> parameters,
            CancellationToken cancellationToken = default)
        {
            Arguments.IsNotNullOrWhiteSpace(apiName, nameof(apiName));

            MobileServiceSerializer serializer = Serializer;
            string content = null;
            if (body != null)
            {
                content = serializer.Serialize(body).ToString();
            }

            string response = await InternalInvokeApiAsync(apiName, content, method, parameters, MobileServiceFeatures.TypedApiCall, cancellationToken);
            if (string.IsNullOrEmpty(response))
            {
                return default;
            }
            return serializer.Deserialize<U>(JToken.Parse(response));
        }

        /// <summary>
        /// Invokes a user-defined custom API of a Microsoft Azure Mobile Service using an HTTP POST.
        /// </summary>
        /// <param name="apiName">The name of the custom API.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> token to observe</param>
        /// <returns></returns>
        public Task<JToken> InvokeApiAsync(string apiName, CancellationToken cancellationToken = default)
        {
            return this.InvokeApiAsync(apiName, null, null, null, cancellationToken);
        }

        /// <summary>
        /// Invokes a user-defined custom API of a Microsoft Azure Mobile Service using an HTTP POST, with
        /// support for sending HTTP content.
        /// </summary>
        /// <param name="apiName">The name of the custom API.</param>
        /// <param name="body">The value to be sent as the HTTP body.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> token to observe</param>
        /// <returns>The response content from the custom api invocation.</returns>
        public Task<JToken> InvokeApiAsync(string apiName, JToken body, CancellationToken cancellationToken = default)
        {
            return this.InvokeApiAsync(apiName, body, defaultHttpMethod, null, cancellationToken);
        }

        /// <summary>
        /// Invokes a user-defined custom API of a Microsoft Azure Mobile Service using the specified HTTP Method.
        /// Additional data will sent to through the query string.
        /// </summary>
        /// <param name="apiName">The name of the custom API.</param>
        /// <param name="method">The HTTP method.</param>
        /// <param name="parameters">
        /// A dictionary of user-defined parameters and values to include in the request URI query string.
        /// </param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> token to observe</param>
        /// <returns>The response content from the custom api invocation.</returns>
        public Task<JToken> InvokeApiAsync(
            string apiName, 
            HttpMethod method, 
            IDictionary<string, string> parameters, 
            CancellationToken cancellationToken = default)
        {
            return this.InvokeApiAsync(apiName, null, method, parameters, cancellationToken);
        }

        /// <summary>
        /// Invokes a user-defined custom API of a Microsoft Azure Mobile Service using the specified HTTP method.
        /// Additional data can be sent though the HTTP content or the query string.
        /// </summary>
        /// <param name="apiName">The name of the custom API.</param>
        /// <param name="body">The value to be sent as the HTTP body.</param>
        /// <param name="method">The HTTP Method.</param>
        /// <param name="parameters">
        /// A dictionary of user-defined parameters and values to include in the request URI query string.
        /// </param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> token to observe</param>
        /// <returns>The response content from the custom api invocation.</returns>
        public async Task<JsonDocument> InvokeApiAsync(
            string apiName, 
            JsonElement body,
            HttpMethod method, 
            IDictionary<string, string> parameters,
            CancellationToken cancellationToken = default)
        {
            Arguments.IsNotNullOrWhiteSpace(apiName, nameof(apiName));

            string content = null;
            if (!body.Equals(default(JsonElement)))
            {
                content = body.ValueKind switch
                {
                    JsonValueKind.Null => "null",
                    JsonValueKind.True => body.ToString().ToLowerInvariant(),
                    JsonValueKind.False => body.ToString().ToLowerInvariant(),
                    _ => body.ToString(),
                };
            }

            string response = await InternalInvokeApiAsync(apiName, content, method, parameters, MobileServiceFeatures.JsonApiCall, cancellationToken);
            return JsonDocument.Parse(response);
        }

        /// <summary>
        /// Invokes a user-defined custom API of a Windows Azure Mobile Service using the specified HttpMethod.
        /// Additional data can be sent though the HTTP content or the query string.
        /// </summary>
        /// <param name="apiName">The name of the custom AP.</param>
        /// <param name="content">The HTTP content.</param>
        /// <param name="method">The HTTP method.</param>
        /// <param name="requestHeaders">
        /// A dictionary of user-defined headers to include in the HttpRequest.
        /// </param>
        /// <param name="parameters">
        /// A dictionary of user-defined parameters and values to include in the request URI query string.
        /// </param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> token to observe</param>
        /// <returns>The HTTP Response from the custom api invocation.</returns>
        public async Task<HttpResponseMessage> InvokeApiAsync(string apiName, HttpContent content, HttpMethod method, IDictionary<string, string> requestHeaders, IDictionary<string, string> parameters, CancellationToken cancellationToken = default)
        {
            method ??= defaultHttpMethod;
            HttpResponseMessage response = await this.HttpClient.RequestAsync(method, CreateAPIUriString(apiName, parameters), this.CurrentUser, content, requestHeaders: requestHeaders, features: MobileServiceFeatures.GenericApiCall, cancellationToken: cancellationToken);
            return response;
        }

        /// <summary>
        /// Invokes a user-defined custom API of a Microsoft Azure Mobile Service.
        /// </summary>
        /// <param name="apiName">The name of the custom API.</param>
        /// <param name="content">The HTTP content, as a string, in json format.</param>
        /// <param name="method">The HTTP method.</param>
        /// <param name="parameters">
        /// A dictionary of user-defined parameters and values to include in the request URI query string.
        /// </param>
        /// <param name="features">
        /// Value indicating which features of the SDK are being used in this call. Useful for telemetry.
        /// </param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> token to observe</param>
        /// <returns>The response content from the custom api invocation.</returns>
        private async Task<string> InternalInvokeApiAsync(string apiName, string content, HttpMethod method, IDictionary<string, string> parameters, MobileServiceFeatures features, CancellationToken cancellationToken = default)
        {
            method ??= defaultHttpMethod;
            if (parameters != null && parameters.Count > 0)
            {
                features |= MobileServiceFeatures.AdditionalQueryParameters;
            }
            var response = await this.HttpClient.RequestAsync<string>(method, CreateAPIUriString(apiName, parameters), this.CurrentUser, content, false, features: features, cancellationToken: cancellationToken);
            return response.Content;
        }

        /// <summary>
        /// Helper function to assemble the Uri for a given custom api.
        /// </summary>
        /// <param name="apiName"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        private string CreateAPIUriString(string apiName, IDictionary<string, string> parameters = null)
        {
            string uriFragment = apiName.StartsWith("/") ? apiName : $"api/{apiName}";
            string queryString = MobileServiceUrlBuilder.GetQueryString(parameters, useTableAPIRules: false);
            return MobileServiceUrlBuilder.CombinePathAndQuery(uriFragment, queryString);
        }

        /// <summary>
        /// Invokes a user-defined custom API of a Windows Azure Mobile Service using the specified HttpMethod.
        /// Additional data can be sent though the HTTP content or the query string.
        /// </summary>
        /// <param name="apiName">The name of the custom AP.</param>
        /// <param name="content">The HTTP content.</param>
        /// <param name="method">The HTTP method.</param>
        /// <param name="requestHeaders">
        /// A dictionary of user-defined headers to include in the HttpRequest.
        /// </param>
        /// <param name="parameters">
        /// A dictionary of user-defined parameters and values to include in the request URI query string.
        /// </param>
        /// <returns>The HTTP Response from the custom api invocation.</returns>
        public async Task<HttpResponseMessage> InvokeApiAsync(string apiName, HttpContent content, HttpMethod method, IDictionary<string, string> requestHeaders, IDictionary<string, string> parameters)
        {
            method ??= defaultHttpMethod;
            HttpResponseMessage response = await this.HttpClient.RequestAsync(method, CreateAPIUriString(apiName, parameters), this.CurrentUser, content, requestHeaders: requestHeaders, features: MobileServiceFeatures.GenericApiCall);
            return response;
        }

    }
}
