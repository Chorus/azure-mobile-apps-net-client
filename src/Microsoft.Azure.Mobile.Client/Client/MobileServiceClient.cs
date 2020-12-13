// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using Microsoft.WindowsAzure.MobileServices.Eventing;
using Microsoft.WindowsAzure.MobileServices.Internal;
using Microsoft.WindowsAzure.MobileServices.Sync;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices
{
    /// <summary>
    /// Provides basic access to a Microsoft Azure Mobile Service.
    /// </summary>
    public partial class MobileServiceClient : IMobileServiceClient, IDisposable
    {
        /// <summary>
        /// Name of the config setting that stores the installation ID.
        /// </summary>
        private const string ConfigureAsyncInstallationConfigPath = "MobileServices.Installation.config";

        /// <summary>
        /// Name of the JSON member in the config setting that stores the
        /// installation ID.
        /// </summary>
        private const string ConfigureAsyncApplicationIdKey = "applicationInstallationId";

        /// <summary>
        /// Relative URI fragment of the refresh user endpoint.
        /// </summary>
        private const string RefreshUserAsyncUriFragment = "/.auth/refresh";

        private static readonly HttpMethod defaultHttpMethod = HttpMethod.Post;

        /// <summary>
        /// Default empty array of HttpMessageHandlers.
        /// </summary>
        private static readonly HttpMessageHandler[] EmptyHttpMessageHandlers = new HttpMessageHandler[0];

        /// <summary>
        /// Absolute URI of the Microsoft Azure Mobile App.
        /// </summary>
        public Uri MobileAppUri { get; private set; }

        /// <summary>
        /// The current authenticated user provided after a successful call to
        /// MobileServiceClient.Login().
        /// </summary>
        public MobileServiceUser CurrentUser { get; set; }

        private string loginUriPrefix;

        /// <summary>
        /// Prefix for the login endpoints. If not set defaults to /.auth/login
        /// </summary>
        public string LoginUriPrefix
        {
            get
            {
                return loginUriPrefix;
            }
            set
            {
                loginUriPrefix = value;
                if (!string.IsNullOrEmpty(value))
                {
                    loginUriPrefix = MobileServiceUrlBuilder.AddLeadingSlash(value);
                }
            }
        }

        private Uri alternateLoginHost;

        /// <summary>
        /// Alternate URI for login
        /// </summary>
        public Uri AlternateLoginHost
        {
            get
            {
                return alternateLoginHost;
            }
            set
            {
                if (value == null)
                {
                    alternateLoginHost = MobileAppUri;
                }
                else if (value.IsAbsoluteUri && value.Segments.Length == 1 && value.Scheme == "https")
                {
                    alternateLoginHost = value;
                }
                else
                {
                    throw new ArgumentException("Invalid AlternateLoginHost", nameof(value));
                }

                this.AlternateAuthHttpClient = new MobileServiceHttpClient(EmptyHttpMessageHandlers, alternateLoginHost, this.InstallationId);
            }
        }

        /// <summary>
        /// The id used to identify this installation of the application to
        /// provide telemetry data.
        /// </summary>
        public string InstallationId { get; private set; }

        /// <summary>
        /// The event manager that exposes and manages the event stream used by the mobile services types to
        /// publish and consume events.
        /// </summary>
        public IMobileServiceEventManager EventManager { get; private set; }

        /// <summary>
        /// The location of any files we need to create for offline-sync
        /// </summary>
        public static string DefaultDatabasePath
        {
            get
            {
                return Platform.Instance.DefaultDatabasePath;
            }
        }

        /// <summary>
        /// Ensures that a file exists, creating it if necessary
        /// </summary>
        /// <param name="path">The fully-qualified pathname to check</param>
        public static void EnsureFileExists(string path)
        {
            Platform.Instance.EnsureFileExists(path);
        }

        /// <summary>
        /// Gets or sets the settings used for serialization.
        /// </summary>
        public MobileServiceJsonSerializerSettings SerializerSettings
        {
            get
            {
                return this.Serializer.SerializerSettings;
            }

            set
            {
                this.Serializer.SerializerSettings = value ?? throw new ArgumentNullException(nameof(value));
            }
        }

        /// <summary>
        /// Instance of <see cref="IMobileServiceSyncContext"/>
        /// </summary>
        public IMobileServiceSyncContext SyncContext
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the serializer that is used with the table.
        /// </summary>
        internal MobileServiceSerializer Serializer { get; set; }

        /// <summary>
        /// Gets the <see cref="MobileServiceHttpClient"/> associated with the Azure Mobile App.
        /// </summary>
        internal MobileServiceHttpClient HttpClient { get; private set; }

        /// <summary>
        /// Gets the <see cref="MobileServiceHttpClient"/> associated with the Alternate login
        /// Azure Mobile App.
        /// </summary>
        internal MobileServiceHttpClient AlternateAuthHttpClient { get; private set; }

        /// <summary>
        /// Initializes a new instance of the MobileServiceClient class.
        /// </summary>
        /// <param name="mobileAppUri">
        /// Absolute URI of the Microsoft Azure Mobile App.
        /// </param>
        /// <param name="handlers">
        /// Chain of <see cref="HttpMessageHandler" /> instances.
        /// All but the last should be <see cref="DelegatingHandler"/>s.
        /// </param>
        public MobileServiceClient(string mobileAppUri, params HttpMessageHandler[] handlers)
            : this(new Uri(mobileAppUri, UriKind.Absolute), handlers)
        {
        }

        /// <summary>
        /// Initializes a new instance of the MobileServiceClient class.
        /// </summary>
        /// <param name="mobileAppUri">
        /// Absolute URI of the Microsoft Azure Mobile App.
        /// </param>
        /// <param name="handlers">
        /// Chain of <see cref="HttpMessageHandler" /> instances.
        /// All but the last should be <see cref="DelegatingHandler"/>s.
        /// </param>
        public MobileServiceClient(Uri mobileAppUri, params HttpMessageHandler[] handlers)
        {
            Arguments.IsNotNull(mobileAppUri, nameof(mobileAppUri));

            if (mobileAppUri.IsAbsoluteUri)
            {
                // Trailing slash in the MobileAppUri is important. Fix it right here before we pass it on further.
                MobileAppUri = new Uri(MobileServiceUrlBuilder.AddTrailingSlash(mobileAppUri.AbsoluteUri), UriKind.Absolute);
            }
            else
            {
                throw new ArgumentException($"'{mobileAppUri}' is not an absolute Uri", nameof(mobileAppUri));
            }

            this.InstallationId = GetApplicationInstallationId();

            handlers ??= EmptyHttpMessageHandlers;
            this.HttpClient = new MobileServiceHttpClient(handlers, this.MobileAppUri, this.InstallationId);
            this.Serializer = new MobileServiceSerializer();
            this.EventManager = new MobileServiceEventManager();
            this.SyncContext = new MobileServiceSyncContext(this);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MobileServiceClient"/> class.
        /// </summary>
        /// <param name="options">the connection options.</param>
        public MobileServiceClient(IMobileServiceClientOptions options) : this(options.MobileAppUri, null)
        {
            AlternateLoginHost = options.AlternateLoginHost;
            LoginUriPrefix = options.LoginUriPrefix;

            var handlers = options.GetDefaultMessageHandlers(this) ?? EmptyHttpMessageHandlers;
            if (handlers.Any())
            {
                HttpClient = new MobileServiceHttpClient(handlers, MobileAppUri, InstallationId);
            }
        }

        /// <summary>
        ///  This is for unit testing only
        /// </summary>
        protected MobileServiceClient()
        {
        }

        /// <summary>
        /// Returns a <see cref="IMobileServiceSyncTable"/> instance, which provides
        /// untyped data operations for that table.
        /// </summary>
        /// <param name="tableName">The name of the table.</param>
        /// <returns>The table.</returns>
        public IMobileServiceSyncTable GetSyncTable(string tableName)
        {
            return GetSyncTable(tableName, MobileServiceTableKind.Table);
        }

        internal MobileServiceSyncTable GetSyncTable(string tableName, MobileServiceTableKind kind)
        {
            ValidateTableName(tableName);

            return new MobileServiceSyncTable(tableName, kind, this);
        }

        /// <summary>
        /// Returns a <see cref="IMobileServiceTable{T}"/> instance, which provides
        /// strongly typed data operations for that table.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the instances in the table.
        /// </typeparam>
        /// <returns>
        /// The table.
        /// </returns>
        public IMobileServiceTable<T> GetTable<T>() where T : ITable
        {
            string tableName = SerializerSettings.ContractResolver.ResolveTableName(typeof(T));
            return new MobileServiceTable<T>(tableName, this);
        }


        /// <summary>
        /// Returns a <see cref="IMobileServiceSyncTable{T}"/> instance, which provides
        /// strongly typed data operations for local table.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the instances in the table.
        /// </typeparam>
        /// <returns>
        /// The table.
        /// </returns>
        public IMobileServiceSyncTable<T> GetSyncTable<T>()
        {
            string tableName = this.SerializerSettings.ContractResolver.ResolveTableName(typeof(T));
            return new MobileServiceSyncTable<T>(tableName, MobileServiceTableKind.Table, this);
        }

        /// <summary>
        /// Implemenation of <see cref="IDisposable"/>
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Implemenation of <see cref="IDisposable"/> for
        /// derived classes to use.
        /// </summary>
        /// <param name="disposing">
        /// Indicates if being called from the Dispose() method
        /// or the finalizer.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                ((MobileServiceSyncContext)this.SyncContext).Dispose();
                // free managed resources
                this.HttpClient.Dispose();
            }
        }

        private static void ValidateTableName(string tableName)
        {
            Arguments.IsNotNullOrWhiteSpace(tableName, nameof(tableName));
        }

        /// <summary>
        /// Gets the ID used to identify this installation of the
        /// application to provide telemetry data.  It will either be retrieved
        /// from local settings or generated fresh.
        /// </summary>
        /// <returns>
        /// An installation ID.
        /// </returns>
        private string GetApplicationInstallationId()
        {
            // Try to get the AppInstallationId from settings
            IApplicationStorage applicationStorage = Platform.Instance.ApplicationStorage;
            applicationStorage.TryReadSetting(ConfigureAsyncInstallationConfigPath, out var installationId);

            // Generate a new AppInstallationId if we failed to find one
            if (installationId == null)
            {
                installationId = Guid.NewGuid().ToString();
                applicationStorage.WriteSetting(ConfigureAsyncInstallationConfigPath, installationId);
            }
            return installationId.ToString();
        }

    }
}
