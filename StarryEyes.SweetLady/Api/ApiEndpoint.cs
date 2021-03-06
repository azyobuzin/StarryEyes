﻿using System.Collections.Generic;
using System.Linq;
using System.Net;
using Codeplex.OAuth;
using StarryEyes.SweetLady.Authorize;

namespace StarryEyes.SweetLady.Api
{
    public static class ApiEndpoint
    {
        public const int DEFAULT_TIMEOUT = 6000;

        public const string USER_AGENT_STR = "Nuclear.Fusion/Krile \"Nuclear\" with ReactiveOAuth";

        public static readonly string EndpointApiV1 = "https://api.twitter.com/1/";

        public static readonly string EndpointSearch = "https://search.twitter.com/";

        public static readonly string EndpointUpload = "https://upload.twitter.com/1/";

        public static string JoinUrl(this string endpoint, string url)
        {
            if (url.StartsWith("/"))
                return endpoint + url.Substring(1);
            else
                return endpoint + url;
        }

        /// <summary>
        /// Set consumer key.
        /// </summary>
        public static string ConsumerKey { get; set; }

        /// <summary>
        /// Set consumer secret.
        /// </summary>
        public static string ConsumerSecret { get; set; }

        /// <summary>
        /// Get OAuth client.
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        internal static OAuthClient GetOAuthClient(this AuthenticateInfo info, bool useGzip = true)
        {
            if (useGzip)
                return info.AccessToken.GetOAuthClient().UseGZip();
            else
                return info.AccessToken.GetOAuthClient();
        }

        internal static OAuthClient GetOAuthClient(this AccessToken token)
        {
            return new OAuthClient(ConsumerKey, ConsumerSecret, token)
            {
                ApplyBeforeRequest = req => req.UserAgent = USER_AGENT_STR
            };
        }

        internal static OAuthClient SetEndpoint(this OAuthClient client, string url)
        {
            client.Url = url;
            return client;
        }

        internal static OAuthClient SetParameters(this OAuthClient client, ParameterCollection collection)
        {
            client.Parameters = collection;
            return client;
        }

        internal static OAuthClient SetMethodType(this OAuthClient client, MethodType methodType)
        {
            client.MethodType = methodType;
            return client;
        }

        internal static OAuthClient UseGZip(this OAuthClient client)
        {
            client.ApplyBeforeRequest = req => req.AutomaticDecompression = DecompressionMethods.GZip;
            return client;
        }

        /// <summary>
        /// Build parameters from dictionary.
        /// </summary>
        internal static ParameterCollection Parametalize(this Dictionary<string, object> dict)
        {
            var ret = new ParameterCollection();
            dict.Keys.Select(key => new { key, value = dict[key] })
                .Where(t => t.value != null)
                .ForEach(t => ret.Add(new Parameter(t.key, t.value)));
            return ret;
        }
    }
}
