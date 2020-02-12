﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.MobileServices
{
    internal class AuthenticatorCompletedEventArgs : EventArgs
    {
        // <summary>
        /// Whether the authentication succeeded and there is a valid authorization code.
        /// </summary>
        /// <value>
        /// true if the user is authenticated; otherwise, false.
        /// </value>
        public bool IsAuthenticated { get { return AuthorizationCode != null; } }

        /// <summary>
        /// Gets the authorization code that represents this authentication.
        /// </summary>
        /// <value>
        /// The authorization code.
        /// </value>
        public string AuthorizationCode { get; private set; }

        /// <summary>
        /// Initializes a new instance of the AuthenticatorCompletedEventArgs class.
        /// </summary>
        /// <param name='authorizationCode'>
        /// The authorization code received or null if authentication failed or was canceled.
        /// </param>
        public AuthenticatorCompletedEventArgs(string authorizationCode)
        {
            AuthorizationCode = authorizationCode;
        }
    }
}
