// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using Microsoft.WindowsAzure.MobileServices.Query;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices
{
    /// <summary>
    /// Provides operations on a table for a Mobile Service.
    /// </summary>
    /// <typeparam name="T">
    /// The type of instances in the table (which implies the table).
    /// </typeparam>
    internal class MobileServiceTable<T> : IMobileServiceTable<T>
    {  /// <summary>
       /// The route separator used to denote the table in a uri like
       /// .../{app}/tables/{coll}.
       /// </summary>
        internal const string TableRouteSeparatorName = "tables";

        /// <summary>
        /// The HTTP PATCH method used for update operations.
        /// </summary>
        private static readonly HttpMethod patchHttpMethod = new HttpMethod("PATCH");

        /// <summary>
        /// The name of the include deleted query string parameter
        /// </summary>
        public const string IncludeDeletedParameterName = "__includeDeleted";

        private readonly MobileServiceTableQueryProvider queryProvider;

        /// <summary>
        /// Gets a reference to the <see cref="MobileServiceClient"/> associated 
        /// with this table.
        /// </summary>
        public MobileServiceClient MobileServiceClient { get; private set; }

        /// <summary>
        /// Gets the name of the table.
        /// </summary>
        public string TableName { get; private set; }

        /// <summary>
        /// Feature which are sent as telemetry information to the service for all
        /// outgoing calls.
        /// </summary>
        internal MobileServiceFeatures Features { get; set; }

        /// <summary>
        /// Initializes a new instance of the MobileServiceTables class.
        /// </summary>
        /// <param name="tableName">
        /// The name of the table.
        /// </param>
        /// <param name="client">
        /// The <see cref="MobileServiceClient"/> associated with this table.
        /// </param>
        public MobileServiceTable(string tableName, MobileServiceClient client)
        {
            this.queryProvider = new MobileServiceTableQueryProvider();
            MobileServiceClient = client;
            TableName = tableName;
        }

        /// <summary>
        /// Returns instances from a table.
        /// </summary>
        /// <returns>
        /// Instances from the table.
        /// </returns>
        /// <remarks>
        /// This call will not handle paging, etc., for you.
        /// </remarks>
        public Task<IEnumerable<T>> ReadAsync()
        {
            return ReadAsync(CreateQuery());
        }

        /// <summary>
        /// Returns instances from a table based on a query.
        /// </summary>
        /// <typeparam name="U">
        /// The type of instance returned by the query.
        /// </typeparam>
        /// <param name="query">
        /// The query to execute.
        /// </param>
        /// <returns>
        /// Instances from the table.
        /// </returns>
        public Task<IEnumerable<U>> ReadAsync<U>(IMobileServiceTableQuery<U> query)
        {
            Arguments.IsNotNull(query, nameof(query));
            return query.ToEnumerableAsync();
        }

        internal virtual async Task<QueryResult<T>> ReadAsync<T>(string query, IDictionary<string, string> parameters, MobileServiceFeatures features)
        {
            features = AddRequestFeatures(features, parameters);

            string uriPath;
            if (HttpUtility.TryParseQueryUri(this.MobileServiceClient.MobileAppUri, query, out Uri uri, out bool absolute))
            {
                if (absolute)
                {
                    features |= MobileServiceFeatures.ReadWithLinkHeader;
                }
                uriPath = HttpUtility.GetUriWithoutQuery(uri);
                query = uri.Query;
            }
            else
            {
                uriPath = MobileServiceUrlBuilder.CombinePaths(TableRouteSeparatorName, this.TableName);
            }

            string parametersString = MobileServiceUrlBuilder.GetQueryString(parameters);

            // Concatenate the query and the user-defined query string parameters
            if (!string.IsNullOrEmpty(parametersString))
            {
                if (!string.IsNullOrEmpty(query))
                {
                    query += '&' + parametersString;
                }
                else
                {
                    query = parametersString;
                }
            }

            string uriString = MobileServiceUrlBuilder.CombinePathAndQuery(uriPath, query);

            return await ReadAsync<T>(uriString, features);
        }

        /// <summary>
        /// Executes a query against the table.
        /// </summary>
        /// <param name="query">
        /// A query to execute.
        /// </param>
        /// <returns>
        /// A task that will return with results when the query finishes.
        /// </returns>
        public async Task<IEnumerable<U>> ReadAsync<U>(string query)
        {
            QueryResult<T> result = await ReadAsync(query, null, MobileServiceFeatures.TypedTable);
            return new QueryResultEnumerable<U>(
                result.TotalCount,
                result.NextLink,
                result.Values).Cast<U>());
        }

        /// <summary>
        /// Inserts a new instance into the table.
        /// </summary>
        /// <param name="instance">
        /// The instance to insert.
        /// </param>
        /// <returns>
        /// A task that will complete when the insertion has finished.
        /// </returns>
        public Task InsertAsync(T instance) => InsertAsync(instance, null);

        /// <summary>
        /// Inserts a new instance into the table.
        /// </summary>
        /// <param name="instance">
        /// The instance to insert.
        /// </param>
        /// <param name="parameters">
        /// A dictionary of user-defined parameters and values to include in 
        /// the request URI query string.
        /// </param>
        /// <returns>
        /// A task that will complete when the insertion has finished.
        /// </returns>
        public async Task InsertAsync(T instance, IDictionary<string, string> parameters)
        {
            Arguments.IsNotNull(instance, nameof(instance));

            MobileServiceSerializer serializer = this.MobileServiceClient.Serializer;
            JObject value = serializer.Serialize(instance) as JObject;
            value = MobileServiceSerializer.RemoveSystemProperties(value, out string unused);
            JToken insertedValue = await TransformHttpException(serializer, () => InsertAsync<T>(value, parameters, MobileServiceFeatures.TypedTable));
            serializer.Deserialize(insertedValue, instance);
        }

        /// <summary>
        /// Updates an instance in the table.
        /// </summary>
        /// <param name="instance">
        /// The instance to update.
        /// </param>
        /// <returns>
        /// A task that will complete when the update has finished.
        /// </returns>
        public Task UpdateAsync(T instance) => UpdateAsync(instance, null);

        /// <summary>
        /// Updates an instance in the table.
        /// </summary>
        /// <param name="instance">
        /// The instance to update.
        /// </param>
        /// <param name="parameters">
        /// A dictionary of user-defined parameters and values to include in 
        /// the request URI query string.
        /// </param>
        /// <returns>
        /// A task that will complete when the update has finished.
        /// </returns>
        public async Task UpdateAsync(T instance, IDictionary<string, string> parameters)
        {
            Arguments.IsNotNull(instance, nameof(instance));

            MobileServiceSerializer serializer = this.MobileServiceClient.Serializer;
            JObject value = serializer.Serialize(instance) as JObject;
            JToken updatedValue = await TransformHttpException(serializer, () => UpdateAsync(value, parameters, MobileServiceFeatures.TypedTable));
            serializer.Deserialize(updatedValue, instance);
        }

        /// <summary>
        /// Undeletes an <paramref name="instance"/> from the table.
        /// </summary>
        /// <param name="instance">The instance to undelete from the table.</param>
        /// <param name="parameters">
        /// A dictionary of user-defined parameters and values to include in 
        /// the request URI query string.
        /// </param>
        /// <returns>A task that will complete when the undelete finishes.</returns>
        public async Task UndeleteAsync(T instance, IDictionary<string, string> parameters)
        {
            Arguments.IsNotNull(instance, nameof(instance));

            MobileServiceSerializer serializer = this.MobileServiceClient.Serializer;
            JObject value = serializer.Serialize(instance) as JObject;
            JToken updatedValue = await TransformHttpException(serializer, () => UndeleteAsync(value, parameters, MobileServiceFeatures.TypedTable));
            serializer.Deserialize(updatedValue, instance);
        }

        /// <summary>
        /// Undeletes an <paramref name="instance"/> from the table.
        /// </summary>
        /// <param name="instance">The instance to undelete from the table.</param>
        /// <returns>A task that will complete when the undelete finishes.</returns>
        public Task UndeleteAsync(T instance) => UndeleteAsync(instance, null);

        /// <summary>
        /// Deletes an instance from the table.
        /// </summary>
        /// <param name="instance">
        /// The instance to delete.
        /// </param>
        /// <returns>
        /// A task that will complete when the delete has finished.
        /// </returns>
        public Task DeleteAsync(T instance) => DeleteAsync(instance, null);

        /// <summary>
        /// Deletes an instance from the table.
        /// </summary>
        /// <param name="instance">
        /// The instance to delete.
        /// </param>
        /// <param name="parameters">
        /// A dictionary of user-defined parameters and values to include in 
        /// the request URI query string.
        /// </param>
        /// <returns>
        /// A task that will complete when the delete has finished.
        /// </returns>
        public async Task DeleteAsync(T instance, IDictionary<string, string> parameters)
        {
            Arguments.IsNotNull(instance, nameof(instance));

            MobileServiceSerializer serializer = this.MobileServiceClient.Serializer;
            JObject value = serializer.Serialize(instance) as JObject;
            await TransformHttpException(serializer, () => DeleteAsync(value, parameters, MobileServiceFeatures.TypedTable));

            // Clear the instance id since it's no longer associated with that
            // id on the server (note that reflection is goodly enough to turn
            // null into the correct value for us).
            serializer.SetIdToDefault(instance);
        }

        /// <summary>
        /// Deletes an <paramref name="instance"/> from the table.
        /// </summary>
        /// <param name="instance">
        /// The instance to delete from the table.
        /// </param>
        /// <param name="parameters">
        /// A dictionary of user-defined parameters and values to include in 
        /// the request URI query string.
        /// </param>
        /// <param name="features">
        /// Value indicating which features of the SDK are being used in this call. Useful for telemetry.
        /// </param>
        /// <returns>
        /// A task that will complete when the delete finishes.
        /// </returns>
        internal async Task<JToken> DeleteAsync(JObject instance, IDictionary<string, string> parameters, MobileServiceFeatures features)
        {
            Arguments.IsNotNull(instance, nameof(instance));

            object id = MobileServiceSerializer.GetId(instance);
            features = this.AddRequestFeatures(features, parameters);
            Dictionary<string, string> headers = StripSystemPropertiesAndAddVersionHeader(ref instance, ref parameters, id);
            string uriString = GetUri(this.TableName, id, parameters);

            return await TransformHttpException(async () =>
            {
                MobileServiceHttpResponse response = await this.MobileServiceClient.HttpClient.RequestAsync(HttpMethod.Delete, uriString, this.MobileServiceClient.CurrentUser, null, false, headers, this.Features | features);
                return GetJTokenFromResponse(response);
            });
        }

        /// <summary>
        /// Get an instance from the table by its id.
        /// </summary>
        /// <param name="id">
        /// The id of the instance.
        /// </param>
        /// <returns>
        /// The desired instance.
        /// </returns>
        public new Task<T> LookupAsync(object id) => LookupAsync(id, null);

        /// <summary>
        /// Get an instance from the table by its id.
        /// </summary>
        /// <param name="id">
        /// The id of the instance.
        /// </param>
        /// <param name="parameters">
        /// A dictionary of user-defined parameters and values to include in 
        /// the request URI query string.
        /// </param>
        /// <returns>
        /// The desired instance.
        /// </returns>
        public new async Task<T> LookupAsync(object id, IDictionary<string, string> parameters)
        {
            // Ensure that the id passed in is assignable to the Id property of T
            this.MobileServiceClient.Serializer.EnsureValidIdForType<T>(id);
            JToken value = await LookupAsync(id, parameters, MobileServiceFeatures.TypedTable);
            return this.MobileServiceClient.Serializer.Deserialize<T>(value);
        }

        /// <summary>
        /// Executes a lookup against a table.
        /// </summary>
        /// <param name="id">
        /// The id of the instance to lookup.
        /// </param>
        /// <param name="parameters">
        /// A dictionary of user-defined parameters and values to include in 
        /// the request URI query string.
        /// </param>
        /// <param name="features">
        /// Value indicating which features of the SDK are being used in this call. Useful for telemetry.
        /// </param>
        /// <returns>
        /// A task that will return with a result when the lookup finishes.
        /// </returns>
        internal async Task<JToken> LookupAsync(object id, IDictionary<string, string> parameters, MobileServiceFeatures features)
        {
            MobileServiceSerializer.EnsureValidId(id);

            features = AddRequestFeatures(features, parameters);
            string uriString = GetUri(this.TableName, id, parameters);
            var response = await MobileServiceClient.HttpClient.RequestAsync(
                HttpMethod.Get, 
                uriString,
                MobileServiceClient.CurrentUser, 
                null, 
                true,
                features: Features | features);
            return GetJTokenFromResponse(response);
        }

        /// <summary>
        /// Executes a lookup against a table.
        /// </summary>
        /// <param name="id">
        /// The id of the instance to lookup.
        /// </param>
        /// <param name="parameters">
        /// A dictionary of user-defined parameters and values to include in 
        /// the request URI query string.
        /// </param>
        /// <param name="features">
        /// Value indicating which features of the SDK are being used in this call. Useful for telemetry.
        /// </param>
        /// <returns>
        /// A task that will return with a result when the lookup finishes.
        /// </returns>
        internal async Task<JToken> LookupAsync(object id, IDictionary<string, string> parameters, MobileServiceFeatures features)
        {
            MobileServiceSerializer.EnsureValidId(id);

            features = AddRequestFeatures(features, parameters);
            string uriString = GetUri(this.TableName, id, parameters);
            var response = await this.MobileServiceClient.HttpClient.RequestAsync(HttpMethod.Get, uriString, this.MobileServiceClient.CurrentUser, null, true, features: this.Features | features);
            return GetJTokenFromResponse(response);
        }

        /// <summary>
        /// Refresh the current instance with the latest values from the
        /// table.
        /// </summary>
        /// <param name="instance">
        /// The instance to refresh.
        /// </param>
        /// <returns>
        /// A task that will complete when the refresh has finished.
        /// </returns>
        public Task RefreshAsync(T instance) => RefreshAsync(instance, null);

        /// <summary>
        /// Refresh the current instance with the latest values from the
        /// table.
        /// </summary>
        /// <param name="instance">
        /// The instance to refresh.
        /// </param>
        /// <param name="parameters">
        /// A dictionary of user-defined parameters and values to include in 
        /// the request URI query string.
        /// </param>
        /// <returns>
        /// A task that will complete when the refresh has finished.
        /// </returns>
        public async Task RefreshAsync(T instance, IDictionary<string, string> parameters)
        {
            Arguments.IsNotNull(instance, nameof(instance));

            MobileServiceSerializer serializer = this.MobileServiceClient.Serializer;
            object id = serializer.GetId(instance, allowDefault: true);

            if (MobileServiceSerializer.IsDefaultId(id))
            {
                return;
            }
            if (id is string)
            {
                MobileServiceSerializer.EnsureValidStringId(id, allowDefault: true);
            }

            // Get the latest version of this element
            JObject refreshed = await this.GetSingleValueAsync(id, parameters);

            // Deserialize that value back into the current instance
            serializer.Deserialize(refreshed, instance);
        }

        /// <summary>
        /// Creates a query for the current table.
        /// </summary>
        /// <returns>
        /// A query against the table.
        /// </returns>
        public IMobileServiceTableQuery<T> CreateQuery()
            => queryProvider.Create(this, new T[0].AsQueryable(), new Dictionary<string, string>(), includeTotalCount: false);

        /// <summary>
        /// Creates a query by applying the specified filter predicate.
        /// </summary>
        /// <param name="predicate">
        /// The filter predicate.
        /// </param>
        /// <returns>
        /// A query against the table.
        /// </returns>
        public IMobileServiceTableQuery<T> Where(Expression<Func<T, bool>> predicate)
        {
            return CreateQuery().Where(predicate);
        }

        /// <summary>
        /// Creates a query by applying the specified selection.
        /// </summary>
        /// <typeparam name="U">
        /// Type representing the projected result of the query.
        /// </typeparam>
        /// <param name="selector">
        /// The selector function.
        /// </param>
        /// <returns>
        /// A query against the table.
        /// </returns>
        public IMobileServiceTableQuery<U> Select<U>(Expression<Func<T, U>> selector)
        {
            return CreateQuery().Select(selector);
        }

        /// <summary>
        /// Creates a query by applying the specified ascending order clause.
        /// </summary>
        /// <typeparam name="TKey">
        /// The type of the member being ordered by.
        /// </typeparam>
        /// <param name="keySelector">
        /// The expression selecting the member to order by.
        /// </param>
        /// <returns>
        /// A query against the table.
        /// </returns>
        public IMobileServiceTableQuery<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            return CreateQuery().OrderBy(keySelector);
        }

        /// <summary>
        /// Creates a query by applying the specified descending order clause.
        /// </summary>
        /// <typeparam name="TKey">
        /// The type of the member being ordered by.
        /// </typeparam>
        /// <param name="keySelector">
        /// The expression selecting the member to order by.
        /// </param>
        /// <returns>
        /// A query against the table.
        /// </returns>
        public IMobileServiceTableQuery<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            return CreateQuery().OrderByDescending(keySelector);
        }

        /// <summary>
        /// Creates a query by applying the specified ascending order clause.
        /// </summary>
        /// <typeparam name="TKey">
        /// The type of the member being ordered by.
        /// </typeparam>
        /// <param name="keySelector">
        /// The expression selecting the member to order by.
        /// </param>
        /// <returns>
        /// A query against the table.
        /// </returns>
        public IMobileServiceTableQuery<T> ThenBy<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            return CreateQuery().ThenBy(keySelector);
        }

        /// <summary>
        /// Creates a query by applying the specified descending order clause.
        /// </summary>
        /// <typeparam name="TKey">
        /// The type of the member being ordered by.
        /// </typeparam>
        /// <param name="keySelector">
        /// The expression selecting the member to order by.
        /// </param>
        /// <returns>
        /// A query against the table.
        /// </returns>
        public IMobileServiceTableQuery<T> ThenByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            return CreateQuery().ThenByDescending(keySelector);
        }

        /// <summary>
        /// Creates a query by applying the specified skip clause.
        /// </summary>
        /// <param name="count">
        /// The number to skip.
        /// </param>
        /// <returns>
        /// A query against the table.
        /// </returns>
        public IMobileServiceTableQuery<T> Skip(int count)
        {
            return CreateQuery().Skip(count);
        }

        /// <summary>
        /// Creates a query by applying the specified take clause.
        /// </summary>
        /// <param name="count">
        /// The number to take.
        /// </param>
        /// <returns>
        /// A query against the table.
        /// </returns>
        public IMobileServiceTableQuery<T> Take(int count)
        {
            return CreateQuery().Take(count);
        }

        /// <summary>
        /// Creates a query that will ensure it gets the total count for all
        /// the records that would have been returned ignoring any take paging/
        /// limit clause specified by client or server.
        /// </summary>
        /// <returns>
        /// A query against the table.
        /// </returns>
        public IMobileServiceTableQuery<T> IncludeTotalCount()
        {
            return CreateQuery().IncludeTotalCount();
        }

        /// <summary>
        /// Creates a query that will ensure it gets the deleted records.
        /// </summary>
        /// <returns>
        /// A query against the table.
        /// </returns>
        public IMobileServiceTableQuery<T> IncludeDeleted()
        {
            return CreateQuery().IncludeDeleted();
        }

        /// <summary>
        /// Applies to the source query the specified string key-value 
        /// pairs to be used as user-defined parameters with the request URI 
        /// query string.
        /// </summary>
        /// <param name="parameters">
        /// The parameters to apply.
        /// </param>
        /// <returns>
        /// The composed query.
        /// </returns>
        public IMobileServiceTableQuery<T> WithParameters(IDictionary<string, string> parameters)
        {
            return CreateQuery().WithParameters(parameters);
        }

        /// <summary>
        /// Gets the instances of the table asynchronously.
        /// </summary>
        /// <returns>
        /// Instances from the table.
        /// </returns>
        public Task<IEnumerable<T>> ToEnumerableAsync()
        {
            return ReadAsync();
        }

        /// <summary>
        /// Gets the instances of the table asynchronously and returns the
        /// results in a new List.
        /// </summary>
        /// <returns>
        /// Instances from the table as a List.
        /// </returns>
        public async Task<List<T>> ToListAsync()
        {
            return new QueryResultList<T>(await ReadAsync());
        }

        /// <summary>
        /// Executes a request and transforms a 412 and 409 response to respective exception type.
        /// </summary>
        private async Task<JToken> TransformHttpException(MobileServiceSerializer serializer, Func<Task<JToken>> action)
        {
            try
            {
                return await action();
            }
            catch (MobileServiceInvalidOperationException ex)
            {
                if (ex.Response.StatusCode != HttpStatusCode.PreconditionFailed &&
                    ex.Response.StatusCode != HttpStatusCode.Conflict)
                {
                    throw;
                }

                T item = default;
                try
                {
                    item = serializer.Deserialize<T>(ex.Value);
                }
                catch { }

                if (ex.Response.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    throw new MobileServicePreconditionFailedException<T>(ex, item);
                }
                else if (ex.Response.StatusCode == HttpStatusCode.Conflict)
                {
                    throw new MobileServiceConflictException<T>(ex, item);
                }
                throw;
            }
        }

        /// <summary>
        /// Get an element from a table by its id.
        /// </summary>
        /// <param name="id">
        /// The id of the element.
        /// </param>
        /// <param name="parameters">
        /// A dictionary of user-defined parameters and values to include in
        /// the request URI query string.
        /// </param>
        /// <returns>
        /// The desired element as JSON object.
        /// </returns>
        private async Task<JObject> GetSingleValueAsync(object id, IDictionary<string, string> parameters)
        {
            Arguments.IsNotNull(id, nameof(id));

            // Create a query for just this item
            string query = $"$filter=({MobileServiceSystemColumns.Id} eq {ODataExpressionVisitor.ToODataConstant(id)})";

            // Send the query
            QueryResult<T> response = await ReadAsync<T>(query, parameters, MobileServiceFeatures.TypedTable);
            return GetSingleValue(response);
        }

        private static JObject GetSingleValue(QueryResult<T> response)
        {
            // Get the first element in the response
            if (!(response.Values.FirstOrDefault() is JObject jobject))
            {
                string responseStr = response != null ? response.ToString() : "null";
                throw new InvalidOperationException($"Could not get object from response {responseStr}.");
            }

            return jobject;
        }

        /// <summary>
        /// Returns a URI for the table, optional id and parameters.
        /// </summary>
        /// <param name="tableName">
        /// The name of the table.
        /// </param>
        /// <param name="id">
        /// The id of the instance.
        /// </param>
        /// <param name="parameters">
        /// A dictionary of user-defined parameters and values to include in 
        /// the request URI query string.
        /// </param>
        /// <returns>
        /// A URI string.
        /// </returns>
        private static string GetUri(string tableName, object id = null, IDictionary<string, string> parameters = null)
        {
            Arguments.IsNotNullOrEmpty(tableName, nameof(tableName));

            string uriPath = MobileServiceUrlBuilder.CombinePaths(TableRouteSeparatorName, tableName);
            if (id != null)
            {
                string idString = Uri.EscapeDataString(string.Format(CultureInfo.InvariantCulture, "{0}", id));
                uriPath = MobileServiceUrlBuilder.CombinePaths(uriPath, idString);
            }

            string queryString = MobileServiceUrlBuilder.GetQueryString(parameters);

            return MobileServiceUrlBuilder.CombinePathAndQuery(uriPath, queryString);
        }

        /// <summary>
        /// Parses the response content into a JToken and adds the version system property
        /// if the ETag was returned from the server.
        /// </summary>
        /// <param name="response">The response to parse.</param>
        /// <returns>The parsed JToken.</returns>
        private JToken GetJTokenFromResponse<T>(MobileServiceHttpResponse<T> response)
        {
            JToken jtoken = response.Content.ParseToJToken(this.MobileServiceClient.SerializerSettings);
            if (response.Etag != null)
            {
                jtoken[MobileServiceSystemColumns.Version] = GetValueFromEtag(response.Etag);
            }

            return jtoken;
        }

        /// <summary>
        /// Adds, if applicable, the <see cref="MobileServiceFeatures.AdditionalQueryParameters"/> value to the
        /// existing list of features used in the current operation, as well as any features set for the
        /// entire table.
        /// </summary>
        /// <param name="existingFeatures">The features from the SDK being used for the current operation.</param>
        /// <param name="parameters">
        /// A dictionary of user-defined parameters and values to include in
        /// the request URI query string.
        /// </param>
        /// <returns>The features used in the current operation.</returns>
        private MobileServiceFeatures AddRequestFeatures(MobileServiceFeatures existingFeatures, IDictionary<string, string> parameters)
        {
            if (parameters != null && parameters.Count > 0)
            {
                existingFeatures |= MobileServiceFeatures.AdditionalQueryParameters;
            }

            existingFeatures |= this.Features;

            return existingFeatures;
        }

        /// <summary>
        /// if id is of type string then it strips the system properties and adds version header.
        /// </summary>
        /// <returns>The header collection with if-match header.</returns>
        private Dictionary<string, string> StripSystemPropertiesAndAddVersionHeader(ref JObject instance, ref IDictionary<string, string> parameters, object id)
        {
            instance = MobileServiceSerializer.RemoveSystemProperties(instance, out string version);
            Dictionary<string, string> headers = AddIfMatchHeader(version);
            return headers;
        }

        /// <summary>
        /// Adds If-Match header to request if version is non-null.
        /// </summary>
        private static Dictionary<string, string> AddIfMatchHeader(string version)
        {
            Dictionary<string, string> headers = null;
            if (!String.IsNullOrEmpty(version))
            {
                headers = new Dictionary<string, string>
                {
                    ["If-Match"] = GetEtagFromValue(version)
                };
            }

            return headers;
        }

        /// <summary>
        /// Gets a valid etag from a string value. Etags are surrounded
        /// by double quotes and any internal quotes must be escaped with a 
        /// '\'.
        /// </summary>
        /// <param name="value">The value to create the etag from.</param>
        /// <returns>
        /// The etag.
        /// </returns>
        private static string GetEtagFromValue(string value)
        {
            // If the value has double quotes, they will need to be escaped.
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '"' && (i == 0 || value[i - 1] != '\\'))
                {
                    value = value.Insert(i, "\\");
                }
            }

            // All etags are quoted;
            return string.Format("\"{0}\"", value);
        }

        /// <summary>
        /// Gets a value from an etag. Etags are surrounded
        /// by double quotes and any internal quotes must be escaped with a 
        /// '\'.
        /// </summary>
        /// <param name="etag">The etag to get the value from.</param>
        /// <returns>
        /// The value.
        /// </returns>
        private static string GetValueFromEtag(string etag)
        {
            int length = etag.Length;
            if (length > 1 && etag[0] == '\"' && etag[length - 1] == '\"')
            {
                etag = etag.Substring(1, length - 2);
            }

            return etag.Replace("\\\"", "\"");
        }
    }
}
