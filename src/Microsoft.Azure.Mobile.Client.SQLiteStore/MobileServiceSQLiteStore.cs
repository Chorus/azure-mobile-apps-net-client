﻿// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MobileServices.Query;
using Microsoft.WindowsAzure.MobileServices.Sync;
using Newtonsoft.Json.Linq;
using SQLitePCL;

namespace Microsoft.WindowsAzure.MobileServices.SQLiteStore
{
    /// <summary>
    /// SQLite based implementation of <see cref="IMobileServiceLocalStore"/>
    /// </summary>
    public class MobileServiceSQLiteStore : MobileServiceLocalStore
    {
        /// <summary>
        /// The maximum number of parameters allowed in any "upsert" prepared statement.
        /// Note: The default maximum number of parameters allowed by sqlite is 999
        /// See: http://www.sqlite.org/limits.html#max_variable_number
        /// </summary>
        private const int MaxParametersPerQuery = 800;

        private readonly Dictionary<string, TableDefinition> tableMap = new Dictionary<string, TableDefinition>(StringComparer.OrdinalIgnoreCase);
        private readonly sqlite3 connection;
        private readonly SemaphoreSlim operationSemaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Parameterless constructor for unit testing.
        /// </summary>
        protected MobileServiceSQLiteStore() { }

        /// <summary>
        /// Initializes a new instance of <see cref="MobileServiceSQLiteStore"/>
        /// </summary>
        /// <param name="fileName">Name of the local SQLite database file.</param>
        public MobileServiceSQLiteStore(string fileName)
        {
            if (fileName == null)
            {
                throw new ArgumentNullException("fileName");
            }

            if (this.connection == null)
            {
                // Fully qualify the path
                var dbPath = fileName.StartsWith("/") ? fileName : Path.Combine(MobileServiceClient.DefaultDatabasePath, fileName);
                MobileServiceClient.EnsureFileExists(dbPath);

                this.connection = SQLitePCLRawHelpers.GetSqliteConnection(dbPath);
            }
        }

        /// <summary>
        /// Defines the local table on the store.
        /// </summary>
        /// <param name="tableName">Name of the local table.</param>
        /// <param name="item">An object that represents the structure of the table.</param>
        public override void DefineTable(string tableName, JObject item)
        {
            if (tableName == null)
            {
                throw new ArgumentNullException("tableName");
            }
            if (item == null)
            {
                throw new ArgumentNullException("item");
            }

            if (this.Initialized)
            {
                throw new InvalidOperationException("Cannot define a table after the store has been initialized.");
            }

            // add id if it is not defined
            if (!item.TryGetValue(MobileServiceSystemColumns.Id, StringComparison.OrdinalIgnoreCase, out JToken ignored))
            {
                item[MobileServiceSystemColumns.Id] = String.Empty;
            }

            var tableDefinition = (from property in item.Properties()
                                   let storeType = SqlHelpers.GetStoreType(property.Value.Type, allowNull: false)
                                   select new ColumnDefinition(property.Name, property.Value.Type, storeType))
                                  .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

            var sysProperties = GetSystemProperties(item);

            this.tableMap.Add(tableName, new TableDefinition(tableDefinition, sysProperties));
        }

        /// <summary>
        /// Initialize the provider, including creating all tables.
        /// </summary>
        /// <returns>A task that resolves when initialization is complete.</returns>
        protected override async Task OnInitialize()
        {
            this.CreateAllTables();
            await this.InitializeConfig();
        }

        /// <summary>
        /// Reads data from local store by executing the query.
        /// </summary>
        /// <param name="query">The query to execute on local store.</param>
        /// <returns>A task that will return with results when the query finishes.</returns>
        public override Task<JToken> ReadAsync(MobileServiceTableQueryDescription query)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            this.EnsureInitialized();

            var formatter = new SqlQueryFormatter(query);
            string sql = formatter.FormatSelect();

            return this.operationSemaphore.WaitAsync()
                .ContinueWith(t =>
                {
                    try
                    {
                        IList<JObject> rows = this.ExecuteQueryInternal(query.TableName, sql, formatter.Parameters);
                        JToken result = new JArray(rows.ToArray());

                        if (query.IncludeTotalCount)
                        {
                            sql = formatter.FormatSelectCount();
                            IList<JObject> countRows = null;

                            countRows = this.ExecuteQueryInternal(query.TableName, sql, formatter.Parameters);


                            long count = countRows[0].Value<long>("count");
                            result = new JObject()
                            {
                                { "results", result },
                                { "count", count }
                            };
                        }

                        return result;
                    }
                    finally
                    {
                        this.operationSemaphore.Release();
                    }
                });
        }

        /// <summary>
        /// Updates or inserts data in local table.
        /// </summary>
        /// <param name="tableName">Name of the local table.</param>
        /// <param name="items">A list of items to be inserted.</param>
        /// <param name="ignoreMissingColumns"><code>true</code> if the extra properties on item can be ignored; <code>false</code> otherwise.</param>
        /// <returns>A task that completes when item has been upserted in local table.</returns>
        public override Task UpsertAsync(string tableName, IEnumerable<JObject> items, bool ignoreMissingColumns)
        {
            if (tableName == null)
            {
                throw new ArgumentNullException("tableName");
            }
            if (items == null)
            {
                throw new ArgumentNullException("items");
            }

            this.EnsureInitialized();

            return UpsertAsyncInternal(tableName, items, ignoreMissingColumns);
        }


        private Task UpsertAsyncInternal(string tableName, IEnumerable<JObject> items, bool ignoreMissingColumns)
        {
            TableDefinition table = GetTable(tableName);

            var first = items.FirstOrDefault();
            if (first == null)
            {
                return Task.FromResult(0);
            }

            // Get the columns which we want to map into the database.
            var columns = new List<ColumnDefinition>();
            foreach (var prop in first.Properties())
            {

                // If the column is coming from the server we can just ignore it,
                // otherwise, throw to alert the caller that they have passed an invalid column
                if (!table.TryGetValue(prop.Name, out ColumnDefinition column) && !ignoreMissingColumns)
                {
                    throw new InvalidOperationException(string.Format("Column with name '{0}' is not defined on the local table '{1}'.", prop.Name, tableName));
                }

                if (column != null)
                {
                    columns.Add(column);
                }
            }

            if (columns.Count == 0)
            {
                // no query to execute if there are no columns in the table
                return Task.FromResult(0);
            }

            return this.operationSemaphore.WaitAsync()
                .ContinueWith(t =>
                {
                    try
                    {
                        this.ExecuteNonQueryInternal("BEGIN TRANSACTION", null);

                        BatchInsert(tableName, items, columns.Where(c => c.Name.Equals(MobileServiceSystemColumns.Id)).Take(1).ToList());
                        BatchUpdate(tableName, items, columns);

                        this.ExecuteNonQueryInternal("COMMIT TRANSACTION", null);
                    }
                    finally
                    {
                        this.operationSemaphore.Release();
                    }
                });
        }

        /// <summary>
        /// Deletes items from local table that match the given query.
        /// </summary>
        /// <param name="query">A query to find records to delete.</param>
        /// <returns>A task that completes when delete query has executed.</returns>
        /// <exception cref="ArgumentNullException">You must supply a query value</exception>
        public override Task DeleteAsync(MobileServiceTableQueryDescription query)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            this.EnsureInitialized();

            var formatter = new SqlQueryFormatter(query);
            string sql = formatter.FormatDelete();

            return this.operationSemaphore.WaitAsync()
                .ContinueWith(t =>
                {
                    try
                    {
                        this.ExecuteNonQueryInternal(sql, formatter.Parameters);
                    }
                    finally
                    {
                        this.operationSemaphore.Release();
                    }
                });
        }

        /// <summary>
        /// Deletes items from local table with the given list of ids
        /// </summary>
        /// <param name="tableName">Name of the local table.</param>
        /// <param name="ids">A list of ids of the items to be deleted</param>
        /// <returns>A task that completes when delete query has executed.</returns>
        public override Task DeleteAsync(string tableName, IEnumerable<string> ids)
        {
            if (tableName == null)
            {
                throw new ArgumentNullException("tableName");
            }
            if (ids == null)
            {
                throw new ArgumentNullException("ids");
            }
            if (!ids.Any())
            {
                return Task.CompletedTask;
            }

            this.EnsureInitialized();

            return this.operationSemaphore.WaitAsync()
                .ContinueWith(t =>
                {
                    try
                    {
                        this.ExecuteNonQueryInternal("BEGIN TRANSACTION", null);

                        foreach (var batch in ids.Split(MaxParametersPerQuery))
                        {
                            BatchDelete(tableName, batch);
                        }

                        this.ExecuteNonQueryInternal("COMMIT TRANSACTION", null);
                    }
                    finally
                    {
                        this.operationSemaphore.Release();
                    }
                });
        }

        private void BatchDelete(string tableName, IEnumerable<string> ids)
        {
            var parameters = ids
                .Select((value, index) => (value, index))
                .ToDictionary(pair => "@id" + pair.index, pair => (object)pair.value);

            string sql = string.Format("DELETE FROM {0} WHERE {1} IN ({2})",
                                       SqlHelpers.FormatTableName(tableName),
                                       MobileServiceSystemColumns.Id,
                                       string.Join(",", parameters.Keys));

            this.ExecuteNonQueryInternal(sql, parameters);
        }

        /// <summary>
        /// Executes a lookup against a local table.
        /// </summary>
        /// <param name="tableName">Name of the local table.</param>
        /// <param name="id">The id of the item to lookup.</param>
        /// <returns>A task that will return with a result when the lookup finishes.</returns>
        public override Task<JObject> LookupAsync(string tableName, string id)
        {
            if (tableName == null)
            {
                throw new ArgumentNullException("tableName");
            }
            if (id == null)
            {
                throw new ArgumentNullException("id");
            }

            this.EnsureInitialized();

            string sql = string.Format("SELECT * FROM {0} WHERE {1} = @id", SqlHelpers.FormatTableName(tableName), MobileServiceSystemColumns.Id);
            var parameters = new Dictionary<string, object>
            {
                {"@id", id}
            };

            return this.operationSemaphore.WaitAsync()
                .ContinueWith(t =>
                {
                    try
                    {
                        IList<JObject> results = this.ExecuteQueryInternal(tableName, sql, parameters);
                        return results.FirstOrDefault();
                    }
                    finally
                    {
                        this.operationSemaphore.Release();
                    }
                });
        }

        private TableDefinition GetTable(string tableName)
        {
            if (!this.tableMap.TryGetValue(tableName, out TableDefinition table))
            {
                throw new InvalidOperationException(string.Format("Table with name '{0}' is not defined.", tableName));
            }
            return table;
        }

        internal virtual async Task SaveSetting(string name, string value)
        {
            var setting = new JObject()
            {
                { "id", name },
                { "value", value }
            };
            await this.UpsertAsyncInternal(MobileServiceLocalSystemTables.Config, new[] { setting }, ignoreMissingColumns: false);
        }

        private async Task InitializeConfig()
        {
            foreach (KeyValuePair<string, TableDefinition> table in this.tableMap)
            {
                if (!MobileServiceLocalSystemTables.All.Contains(table.Key))
                {
                    // preserve system properties setting for non-system tables
                    string name = String.Format("systemProperties|{0}", table.Key);
                    string value = ((int)table.Value.SystemProperties).ToString();
                    await this.SaveSetting(name, value);
                }
            }
        }

        private void CreateAllTables()
        {
            foreach (KeyValuePair<string, TableDefinition> table in this.tableMap)
            {
                this.CreateTableFromObject(table.Key, table.Value.Values);
            }
        }

        private void BatchUpdate(string tableName, IEnumerable<JObject> items, List<ColumnDefinition> columns)
        {
            if (columns.Count <= 1)
            {
                return; // For update to work there has to be at least once column besides Id that needs to be updated
            }

            ValidateParameterCount(columns.Count);

            string sqlBase = String.Format("UPDATE {0} SET ", SqlHelpers.FormatTableName(tableName));

            foreach (JObject item in items)
            {
                var sql = new StringBuilder(sqlBase);
                var parameters = new Dictionary<string, object>();

                ColumnDefinition idColumn = columns.FirstOrDefault(c => c.Name.Equals(MobileServiceSystemColumns.Id));
                if (idColumn == null)
                {
                    continue;
                }

                foreach (var column in columns.Where(c => c != idColumn))
                {
                    string paramName = AddParameter(item, parameters, column);

                    sql.AppendFormat("{0} = {1}", SqlHelpers.FormatMember(column.Name), paramName);
                    sql.Append(",");
                }

                if (parameters.Any())
                {
                    sql.Remove(sql.Length - 1, 1); // remove the trailing comma

                }

                sql.AppendFormat(" WHERE {0} = {1}", SqlHelpers.FormatMember(MobileServiceSystemColumns.Id), AddParameter(item, parameters, idColumn));

                this.ExecuteNonQueryInternal(sql.ToString(), parameters);
            }
        }

        private void BatchInsert(string tableName, IEnumerable<JObject> items, List<ColumnDefinition> columns)
        {
            if (columns.Count == 0) // we need to have some columns to insert the item
            {
                return;
            }

            // Generate the prepared insert statement
            string sqlBase = String.Format(
                "INSERT OR IGNORE INTO {0} ({1}) VALUES ",
                SqlHelpers.FormatTableName(tableName),
                String.Join(", ", columns.Select(c => c.Name).Select(SqlHelpers.FormatMember))
            );

            // Use int division to calculate how many times this record will fit into our parameter quota
            int batchSize = ValidateParameterCount(columns.Count);

            foreach (var batch in items.Split(maxLength: batchSize))
            {
                var sql = new StringBuilder(sqlBase);
                var parameters = new Dictionary<string, object>();

                foreach (JObject item in batch)
                {
                    AppendInsertValuesSql(sql, parameters, columns, item);
                    sql.Append(",");
                }

                if (parameters.Any())
                {
                    sql.Remove(sql.Length - 1, 1); // remove the trailing comma
                    this.ExecuteNonQueryInternal(sql.ToString(), parameters);
                }
            }
        }

        private static int ValidateParameterCount(int parametersCount)
        {
            int batchSize = MaxParametersPerQuery / parametersCount;
            if (batchSize == 0)
            {
                throw new InvalidOperationException(string.Format("The number of fields per entity in an upsert operation is limited to {0}.", MaxParametersPerQuery));
            }
            return batchSize;
        }

        private static void AppendInsertValuesSql(StringBuilder sql, Dictionary<string, object> parameters, List<ColumnDefinition> columns, JObject item)
        {
            sql.Append("(");
            int colCount = 0;
            foreach (var column in columns)
            {
                if (colCount > 0)
                {
                    sql.Append(",");
                }

                sql.Append(AddParameter(item, parameters, column));

                colCount++;
            }
            sql.Append(")");
        }

        internal virtual void CreateTableFromObject(string tableName, IEnumerable<ColumnDefinition> columns)
        {
            ColumnDefinition idColumn = columns.FirstOrDefault(c => c.Name.Equals(MobileServiceSystemColumns.Id));
            var colDefinitions = columns.Where(c => c != idColumn).Select(c => String.Format("{0} {1}", SqlHelpers.FormatMember(c.Name), c.StoreType)).ToList();
            if (idColumn != null)
            {
                colDefinitions.Insert(0, String.Format("{0} {1} PRIMARY KEY", SqlHelpers.FormatMember(idColumn.Name), idColumn.StoreType));
            }

            String tblSql = string.Format("CREATE TABLE IF NOT EXISTS {0} ({1})", SqlHelpers.FormatTableName(tableName), String.Join(", ", colDefinitions));
            this.ExecuteNonQueryInternal(tblSql, parameters: null);

            string infoSql = string.Format("PRAGMA table_info({0});", SqlHelpers.FormatTableName(tableName));
            IDictionary<string, JObject> existingColumns = this.ExecuteQueryInternal((TableDefinition)null, infoSql, parameters: null)
                                                               .ToDictionary(c => c.Value<string>("name"), StringComparer.OrdinalIgnoreCase);

            // new columns that do not exist in existing columns
            var columnsToCreate = columns.Where(c => !existingColumns.ContainsKey(c.Name));

            foreach (ColumnDefinition column in columnsToCreate)
            {
                string createSql = string.Format("ALTER TABLE {0} ADD COLUMN {1} {2}",
                                                 SqlHelpers.FormatTableName(tableName),
                                                 SqlHelpers.FormatMember(column.Name),
                                                 column.StoreType);
                this.ExecuteNonQueryInternal(createSql, parameters: null);
            }

            // NOTE: In SQLite you cannot drop columns, only add them.
        }

        private static string AddParameter(JObject item, Dictionary<string, object> parameters, ColumnDefinition column)
        {
            JToken rawValue = item.GetValue(column.Name, StringComparison.OrdinalIgnoreCase);
            object value = SqlHelpers.SerializeValue(rawValue, column.StoreType, column.JsonType);
            string paramName = CreateParameter(parameters, value);
            return paramName;
        }

        private static string CreateParameter(Dictionary<string, object> parameters, object value)
        {
            string paramName = "@p" + parameters.Count;
            parameters[paramName] = value;
            return paramName;
        }

        /// <summary>
        /// Executes a sql statement on a given table in local SQLite database.
        /// </summary>
        /// <param name="sql">The SQL statement to execute.</param>
        /// <param name="parameters">The query parameters.</param>
        protected virtual void ExecuteNonQueryInternal(string sql, IDictionary<string, object> parameters)
        {
            parameters = parameters ?? new Dictionary<string, object>();

            int rc = raw.sqlite3_prepare_v2(connection, sql, out sqlite3_stmt stmt);
            SQLitePCLRawHelpers.VerifySQLiteResponse(rc, raw.SQLITE_OK, connection);
            using (stmt)
            {
                foreach (KeyValuePair<string, object> parameter in parameters)
                {
                    var index = raw.sqlite3_bind_parameter_index(stmt, parameter.Key);
                    SQLitePCLRawHelpers.Bind(connection, stmt, index, parameter.Value);
                }

                int result = raw.sqlite3_step(stmt);
                SQLitePCLRawHelpers.VerifySQLiteResponse(result, raw.SQLITE_DONE, connection);
            }
        }

        /// <summary>
        /// Executes a SQL query against the store.  This is useful for running arbitrary queries
        /// that are supported by SQLite but not the SDK LINQ Provider
        /// </summary>
        /// <param name="tableName">The name of the table</param>
        /// <param name="sql">The SQL command</param>
        /// <param name="parameters">The list of parameters</param>
        /// <returns>The result of the query (untyped objects)</returns>
        /// <exception cref="ArgumentNullException">tableName and sql must be provided</exception>
        public Task<IList<JObject>> ExecuteQueryAsync(string tableName, string sql, IDictionary<string, object> parameters)
        {
            if (tableName == null)
            {
                throw new ArgumentNullException("tableName");
            }
            if (sql == null)
            {
                throw new ArgumentNullException("sql");
            }

            this.EnsureInitialized();
            return this.operationSemaphore.WaitAsync()
                .ContinueWith(t =>
                {
                    try
                    {
                        return this.ExecuteQueryInternal(tableName, sql, parameters);
                    }
                    finally
                    {
                        this.operationSemaphore.Release();
                    }
                });
        }

        /// <summary>
        /// Executes a sql statement on a given table in local SQLite database.
        /// </summary>
        /// <param name="tableName">The name of the table.</param>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="parameters">The query parameters.</param>
        /// <returns>The result of query.</returns>
        protected virtual IList<JObject> ExecuteQueryInternal(string tableName, string sql, IDictionary<string, object> parameters)
        {
            TableDefinition table = GetTable(tableName);
            return this.ExecuteQueryInternal(table, sql, parameters);
        }

        /// <summary>
        /// Executes a sql statement on a given table in local SQLite database.
        /// </summary>
        /// <param name="table">The table definition.</param>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="parameters">The query parameters.</param>
        /// <returns>The result of query.</returns>
        protected virtual IList<JObject> ExecuteQueryInternal(TableDefinition table, string sql, IDictionary<string, object> parameters)
        {
            table = table ?? new TableDefinition();
            parameters = parameters ?? new Dictionary<string, object>();
            var rows = new List<JObject>();

            sqlite3_stmt statement = SQLitePCLRawHelpers.GetSqliteStatement(sql, connection);
            using (statement)
            {
                foreach (KeyValuePair<string, object> parameter in parameters)
                {
                    var index = raw.sqlite3_bind_parameter_index(statement, parameter.Key);
                    SQLitePCLRawHelpers.Bind(connection, statement, index, parameter.Value);
                }
                int rc;
                while ((rc = raw.sqlite3_step(statement)) == raw.SQLITE_ROW)
                {
                    var row = ReadRow(table, statement);
                    rows.Add(row);
                }

                SQLitePCLRawHelpers.VerifySQLiteResponse(rc, raw.SQLITE_DONE, connection);
            }

            return rows;
        }

        private JObject ReadRow(TableDefinition table, sqlite3_stmt statement)
        {
            var row = new JObject();
            for (int i = 0; i < raw.sqlite3_column_count(statement); i++)
            {
                string name = raw.sqlite3_column_name(statement, i).utf8_to_string();
                object value = SQLitePCLRawHelpers.GetValue(statement, i);

                if (table.TryGetValue(name, out ColumnDefinition column))
                {
                    JToken jVal = SqlHelpers.DeserializeValue(value, column.StoreType, column.JsonType);
                    row[name] = jVal;
                }
                else
                {
                    row[name] = value == null ? null : JToken.FromObject(value);
                }
            }
            return row;
        }

        private static MobileServiceSystemProperties GetSystemProperties(JObject item)
        {
            var sysProperties = MobileServiceSystemProperties.None;

            if (item[MobileServiceSystemColumns.Version] != null)
            {
                sysProperties |= MobileServiceSystemProperties.Version;
            }
            if (item[MobileServiceSystemColumns.CreatedAt] != null)
            {
                sysProperties |= MobileServiceSystemProperties.CreatedAt;
            }
            if (item[MobileServiceSystemColumns.UpdatedAt] != null)
            {
                sysProperties |= MobileServiceSystemProperties.UpdatedAt;
            }
            if (item[MobileServiceSystemColumns.Deleted] != null)
            {
                sysProperties |= MobileServiceSystemProperties.Deleted;
            }
            return sysProperties;
        }

        /// <summary>
        /// Required part of the IDisposable pattern.
        /// </summary>
        /// <param name="disposing">True if disposing as part of a stack.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.connection.Dispose();
            }
        }
    }
}
