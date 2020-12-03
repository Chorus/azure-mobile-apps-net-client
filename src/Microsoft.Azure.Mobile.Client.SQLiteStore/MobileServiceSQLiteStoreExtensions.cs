// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System;
using System.Linq;
using System.Reflection;

namespace Microsoft.WindowsAzure.MobileServices.SQLiteStore
{
    /// <summary>
    ///  Provides extension methods on <see cref="MobileServiceSQLiteStore"/>.
    /// </summary>
    public static class MobileServiceSQLiteStoreExtensions
    {
        /// <summary>
        /// Defines a table to use for offline sync
        /// </summary>
        /// <param name="store">The offline store.</param>
        /// <typeparam name="T">The model type of the table</typeparam>
        public static void DefineTable<T>(this MobileServiceSQLiteStore store)
             where T : ITable, new()
        {
            var settings = new MobileServiceJsonSerializerSettings();
            DefineTable<T>(store, settings);
        }

        /// <summary>
        /// Defines a table to use for offline sync
        /// </summary>
        /// <param name="store">The offline store.</param>
        /// <param name="settings">The JSON Serializer settings</param>
        /// <typeparam name="T">The model type of the table</typeparam>
        public static void DefineTable<T>(this MobileServiceSQLiteStore store, MobileServiceJsonSerializerSettings settings)
            where T : ITable, new()
        {
            string tableName = string.Empty; //settings.ContractResolver.ResolveTableName(typeof(T));

            // create an empty object
            var item = new T();

            //set default values so serialized version can be used to infer types
            SetIdDefault(item);
            SetNullDefault(item);
            SetEnumDefault(item);

            store.DefineTable(tableName, item);
        }

        private static void SetEnumDefault<T>(T theObject)
            where T : ITable
        {
            foreach (var property in theObject.GetType().GetProperties())
            {
                Type actualType = property.PropertyType;
                if (actualType.GetTypeInfo().IsGenericType
                                   && actualType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    actualType = actualType.GenericTypeArguments[0];
                }

                if (actualType.GetTypeInfo().IsEnum)
                {
                    object firstValue = Enum.GetValues(actualType)
                                            .Cast<object>()
                                            .FirstOrDefault();
                    if (firstValue != null)
                    {
                        property.SetValue(theObject, firstValue);
                    }
                }
            }
        }

        private static void SetIdDefault<T>(T item) 
            where T : ITable
        {
            item.Id = string.Empty;
        }

        private static void SetNullDefault<T>(T item)
        {

        }
    }
}
