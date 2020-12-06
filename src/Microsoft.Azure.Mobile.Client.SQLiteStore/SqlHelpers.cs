// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System;
using System.Globalization;

namespace Microsoft.WindowsAzure.MobileServices.SQLiteStore
{
    internal class SqlHelpers
    {
        private const string _efCoreDateTimeFormat = @"yyyy\-MM\-dd HH\:mm\:ss.FFFFFFF";

        public static object SerializeValue(object value, bool allowNull)
        {
            string storeType = GetStoreType(value.GetType(), allowNull);
            return SerializeValue(value, storeType, value.GetType());
        }

        public static object SerializeValue(object value, string storeType, Type columnType)
        {
            if (value == null || Type.GetTypeCode(columnType) == TypeCode.Empty)
            {
                return null;
            }
            if (IsTextType(storeType))
            {
                return SerializeAsText(value, columnType);
            }
            if (IsRealType(storeType))
            {
                return SerializeAsReal(value, columnType);
            }
            if (IsNumberType(storeType))
            {
                return SerializeAsNumber(value, columnType);
            }

            return value.ToString();
        }

        public static object DeserializeValue(object value, string storeType, Type columnType)
        {
            if (value == null)
            {
                return null;
            }

            if (IsTextType(storeType))
            {
                return ParseText(columnType, value);
            }
            if (IsRealType(storeType))
            {
                return ParseReal(columnType, value);
            }
            if (IsNumberType(storeType))
            {
                return ParseNumber(columnType, value);
            }

            return null;
        }

        // https://www.sqlite.org/datatype3.html (2.2 Affinity Name Examples)
        public static string GetStoreCastType(Type type)
        {
            if (type == typeof(bool) ||
                type == typeof(DateTime) ||
                type == typeof(decimal))
            {
                return SqlColumnType.Numeric;
            }
            else if (type == typeof(int) ||
                    type == typeof(uint) ||
                    type == typeof(long) ||
                    type == typeof(ulong) ||
                    type == typeof(short) ||
                    type == typeof(ushort) ||
                    type == typeof(byte) ||
                    type == typeof(sbyte))
            {
                return SqlColumnType.Integer;
            }
            else if (type == typeof(float) ||
                     type == typeof(double))
            {
                return SqlColumnType.Real;
            }
            else if (type == typeof(string) ||
                    type == typeof(Guid) ||
                    type == typeof(byte[]) ||
                    type == typeof(Uri) ||
                    type == typeof(TimeSpan))
            {
                return SqlColumnType.Text;
            }

            throw new NotSupportedException(string.Format(CultureInfo.InvariantCulture, "Value of type '{0}' is not supported.", type.Name));
        }

        public static string GetStoreType(Type type, bool allowNull)
        {
            if (type == typeof(Guid))
            {
                return SqlColumnType.Guid;
            }
            if (type == typeof(byte[]))
            {
                return SqlColumnType.Blob;
            }
            if (type == typeof(Uri))
            {
                return SqlColumnType.Uri;
            }
            if (type == typeof(TimeSpan))
            {
                return SqlColumnType.TimeSpan;
            }
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                    return SqlColumnType.Boolean;
                case TypeCode.Int32:
                    return SqlColumnType.Integer;
                case TypeCode.DateTime:
                    return SqlColumnType.DateTime;
                case TypeCode.Double:
                    return SqlColumnType.Float;
                case TypeCode.String:
                    return SqlColumnType.Text;
                case TypeCode.Object:
                    return SqlColumnType.Json;
                case TypeCode.Empty:
                    if (allowNull)
                    {
                        return null;
                    }
                    throw new NotSupportedException(string.Format(CultureInfo.InvariantCulture, "Property of type '{0}' is not supported.", type));
                default:
                    throw new NotSupportedException(string.Format(CultureInfo.InvariantCulture, "Property of type '{0}' is not supported.", type));
            }
        }

        public static string FormatTableName(string tableName)
        {
            ValidateIdentifier(tableName);
            return string.Format("[{0}]", tableName);
        }

        public static string FormatMember(string memberName)
        {
            ValidateIdentifier(memberName);
            return string.Format("[{0}]", memberName);
        }

        private static bool IsNumberType(string storeType)
        {
            return storeType == SqlColumnType.Integer ||
                    storeType == SqlColumnType.Numeric ||
                    storeType == SqlColumnType.Boolean;
        }

        private static bool IsRealType(string storeType)
        {
            return storeType == SqlColumnType.Real ||
                    storeType == SqlColumnType.Float;
        }

        private static bool IsTextType(string storeType)
        {
            return storeType == SqlColumnType.Text ||
                    storeType == SqlColumnType.Blob ||
                    storeType == SqlColumnType.Guid ||
                    storeType == SqlColumnType.Json ||
                    storeType == SqlColumnType.Uri ||
                    storeType == SqlColumnType.TimeSpan ||
                    storeType == SqlColumnType.DateTime;
        }

        private static object SerializeAsNumber(object value, Type columnType)
        {
            return long.Parse(value.ToString());
        }

        private static double SerializeAsReal(object value, Type columnType)
        {
            return double.Parse(value.ToString());
        }

        private static string SerializeAsText(object value, Type columnType)
        {
            if (columnType == typeof(byte[]))
            {
                return Convert.ToBase64String(value as byte[]);
            }

            if (columnType == typeof(DateTime))
            {
                return DateTime.Parse(value.ToString())
                    .ToUniversalTime()
                    .ToString(_efCoreDateTimeFormat);
            }
            return value.ToString();
        }

        private static object ParseText(Type type, object value)
        {
            string strValue = value as string;
            if (value == null)
            {
                return strValue;
            }

            if (type == typeof(Guid))
            {
                return Guid.Parse(strValue);
            }
            if (type == typeof(byte[]))
            {
                return Convert.FromBase64String(strValue);
            }
            if (type == typeof(TimeSpan))
            {
                return TimeSpan.Parse(strValue);
            }
            if (type == typeof(Uri))
            {
                return new Uri(strValue, UriKind.RelativeOrAbsolute);
            }
            if (type == typeof(Array) || type == typeof(object))
            {
                return value;
            }
            return strValue;
        }

        private static object ParseReal(Type type, object value)
        {
            return Convert.ToDouble(value);
        }

        private static object ParseNumber(Type type, object value)
        {
            long longValue = Convert.ToInt64(value);
            if (type == typeof(bool))
            {
                bool boolValue = longValue == 1;
                return boolValue;
            }
            return longValue;
        }

        private static void ValidateIdentifier(string identifier)
        {
            if (!IsValidIdentifier(identifier))
            {
                throw new ArgumentException(string.Format("'{0}' is not a valid identifier. Identifiers must be under 128 characters in length, start with a letter or underscore, and can contain only alpha-numeric and underscore characters.", identifier), "identifier");
            }
        }

        private static bool IsValidIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier) || identifier.Length > 128)
            {
                return false;
            }

            char first = identifier[0];
            if (!(char.IsLetter(first) || first == '_'))
            {
                return false;
            }

            for (int i = 1; i < identifier.Length; i++)
            {
                char ch = identifier[i];
                if (!(char.IsLetterOrDigit(ch) || ch == '_'))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
