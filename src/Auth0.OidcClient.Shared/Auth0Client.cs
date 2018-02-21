﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using IdentityModel.Client;
using IdentityModel.OidcClient;
using IdentityModel.OidcClient.Results;

namespace Auth0.OidcClient
{
    public class Auth0Client
    {
        private readonly Auth0ClientOptions _options;
        private readonly IdentityModel.OidcClient.OidcClient _oidcClient;

        /// <summary>
        /// Creates a new instance of the Auth0 OIDC Client.
        /// </summary>
        /// <param name="options">The <see cref="Auth0ClientOptions"/> specifying the configuration for the Auth0 OIDC Client.</param>
        public Auth0Client(Auth0ClientOptions options)
        {
            _options = options;

            var authority = $"https://{options.Domain}";
#if __ANDROID__
            string packageName = Plugin.CurrentActivity.CrossCurrentActivity.Current.Activity.Application.ApplicationInfo.PackageName;
#endif
            var oidcClientOptions = new OidcClientOptions
            {
                Authority = authority,
                ClientId = options.ClientId,
                ClientSecret = options.ClientSecret,
                Scope = options.Scope,
                LoadProfile = options.LoadProfile,
#if __IOS__
				RedirectUri = $"{Foundation.NSBundle.MainBundle.BundleIdentifier}://{options.Domain}/ios/{Foundation.NSBundle.MainBundle.BundleIdentifier}/callback",
				Browser = new PlatformWebView(),
#elif __ANDROID__
				RedirectUri = options.RedirectUri ?? $"{packageName}://{options.Domain}/android/{packageName}/callback".ToLower(),
                Browser = new PlatformWebView(),
#elif WINDOWS_UWP
                RedirectUri = Windows.Security.Authentication.Web.WebAuthenticationBroker.GetCurrentApplicationCallbackUri().AbsoluteUri,
                Browser = options.Browser ?? new PlatformWebView(),
#else
                RedirectUri = options.RedirectUri ?? $"https://{options.Domain}/mobile",
                Browser = options.Browser ?? new PlatformWebView(),
#endif
                Flow = OidcClientOptions.AuthenticationFlow.AuthorizationCode,
#if WINDOWS_UWP
                ResponseMode = OidcClientOptions.AuthorizeResponseMode.FormPost,
#else
                ResponseMode = OidcClientOptions.AuthorizeResponseMode.Redirect,
#endif
                Policy =
                {
                    RequireAuthorizationCodeHash = false,
                    RequireAccessTokenHash = false
                }
            };
            _oidcClient = new IdentityModel.OidcClient.OidcClient(oidcClientOptions);
        }

        private Dictionary<string, string> AppendTelemetry(object values)
        {
            var dictionary = ObjectToDictionary(values);

            if (_options.EnableTelemetry)
                dictionary.Add("auth0Client", CreateTelemetry());

            return dictionary;
        }

        private string CreateTelemetry()
        {
#if __ANDROID__
            string platform = "xamarin-android";
#elif __IOS__
            string platform = "xamarin-ios";
#elif WINFORMS
            string platform = "winforms";
#elif WPF
            string platform = "wpf";
#elif WINDOWS_UWP
            var platform = "uwp";
#endif
            var version = GetType().GetTypeInfo().Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()
                .Version;

            var telemetryString = $"{{\"name\":\"oidc-net\",\"version\":\"{version}\",\"platform\":\"{platform}\"}}";
            var telemetryBytes = Encoding.UTF8.GetBytes(telemetryString);

            return Convert.ToBase64String(telemetryBytes);
        }

        /// <summary>
        /// Launches a browser to log the user in.
        /// </summary>
        /// <param name="extraParameters">Any extra parameters that need to be passed to the authorization endpoint.</param>
        /// <returns></returns>
        public Task<LoginResult> LoginAsync(object extraParameters = null)
        {
            return _oidcClient.LoginAsync(extraParameters: AppendTelemetry(extraParameters));
        }

        private Dictionary<string, string> ObjectToDictionary(object values)
        {
            var dictionary = values as Dictionary<string, string>;
            if (dictionary != null)
                return dictionary;

            dictionary = new Dictionary<string, string>();
            if (values != null)
                foreach (var prop in values.GetType().GetRuntimeProperties())
                {
                    var value = prop.GetValue(values) as string;
                    if (!string.IsNullOrEmpty(value))
                        dictionary.Add(prop.Name, value);
                }

            return dictionary;
        }

        /// <summary>
        /// Generates an <see cref="AuthorizeState"/> containing the URL, state, nonce and code challenge which can
        /// be used to redirect the user to the authorization URL, and subsequently process any response by calling
        /// the <see cref="ProcessResponseAsync"/> method.
        /// </summary>
        /// <param name="extraParameters"></param>
        /// <returns></returns>
        public Task<AuthorizeState> PrepareLoginAsync(object extraParameters = null)
        {
            return _oidcClient.PrepareLoginAsync(AppendTelemetry(extraParameters));
        }

        /// <summary>
        /// Process the response from the Auth0 redirect URI
        /// </summary>
        /// <param name="data">The data containing the full redirect URI.</param>
        /// <param name="state">The <see cref="AuthorizeState"/> which was generated when the <see cref="PrepareLoginAsync"/>
        /// method was called.</param>
        /// <returns></returns>
        public Task<LoginResult> ProcessResponseAsync(string data, AuthorizeState state)
        {
            return _oidcClient.ProcessResponseAsync(data, state);
        }

        /// <summary>
        /// Generates a new set of tokens based on a refresh token. 
        /// </summary>
        /// <param name="refreshToken">The refresh token which was issued during the authorization flow, or subsequent
        /// calls to <see cref="RefreshTokenAsync"/>.</param>
        /// <returns></returns>
        public Task<RefreshTokenResult> RefreshTokenAsync(string refreshToken)
        {
            return _oidcClient.RefreshTokenAsync(refreshToken);
        }
    }
}