using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PlayerIOClient.Helpers
{
    // A miniature version of RabbitIO (https://github.com/Decagon/Rabbit).
    public static class Authentication
    {
        public enum AuthenticationType { Invalid, Unknown, Facebook, Kongregate, ArmorGames, Simple, Public, UserId = Simple }

        /// <summary>Connects to the PlayerIO service using the provided credentials.</summary>
        /// <param name="user">The user id, token or email address.</param>
        /// <param name="auth">The password or temporary key.</param>
        public static Client LogOn(string gameid, string user = "", string auth = "", AuthenticationType type = AuthenticationType.Unknown)
        {
            user = Regex.Replace(user, @"\s+", string.Empty);
            gameid = Regex.Replace(gameid, @"\s+", string.Empty);

            if (type == AuthenticationType.Unknown)
                type = GetAuthType(user, auth);

            return Authenticate(gameid, user, auth, type);
        }

        private static AuthenticationType GetAuthType(string user, string auth)
        {
            if (string.IsNullOrEmpty(auth))
                throw new ArgumentNullException("auth");

            if (string.IsNullOrEmpty(user)) {
                if (Regex.IsMatch(auth, @"[0-9a-z]$", RegexOptions.IgnoreCase) && auth.Length > 90)
                    return AuthenticationType.Facebook;
                return AuthenticationType.Invalid;
            }

            if (Regex.IsMatch(auth, @"\A\b[0-9a-fA-F]+\b\Z")) {
                if (user.Length == 32 && auth.Length == 32)
                    return AuthenticationType.ArmorGames;
                if (Regex.IsMatch(user, @"^\d+$") && auth.Length == 64)
                    return AuthenticationType.Kongregate;
            }

            if (Regex.IsMatch(user, @"\b+[a-zA-Z0-9\.\-_]+@[a-zA-Z0-9\.\-]+\.[a-zA-Z0-9\.\-]+\b"))
                return AuthenticationType.Simple;

            return AuthenticationType.UserId;
        }

        private static Client Authenticate(string gameid, string user, string auth, AuthenticationType type = AuthenticationType.Invalid) =>
               type == AuthenticationType.Facebook ? PlayerIO.QuickConnect.FacebookOAuthConnect(gameid, auth, null, null) :
               type == AuthenticationType.Simple ? PlayerIO.QuickConnect.SimpleConnect(gameid, user, auth, null) :
               type == AuthenticationType.Kongregate ? PlayerIO.QuickConnect.KongregateConnect(gameid, user, auth, null) :
               type == AuthenticationType.ArmorGames ? PlayerIO.Authenticate(gameid, "public", new Dictionary<string, string> { { "userId", user }, { "authToken", auth } }, null) :
               type == AuthenticationType.Public ? PlayerIO.Connect(gameid, "public", user, auth, null) : null;
    }
}