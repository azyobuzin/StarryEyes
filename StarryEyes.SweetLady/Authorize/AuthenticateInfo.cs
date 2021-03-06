﻿using System;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Codeplex.OAuth;
using StarryEyes.SweetLady.DataModel;
using System.Linq;

namespace StarryEyes.SweetLady.Authorize
{
    /// <summary>
    /// Account information holder
    /// </summary>
    [DataContract]
    public class AuthenticateInfo
    {
        public AuthenticateInfo() { }
        public AuthenticateInfo(long id, string screen, AccessToken token)
        {
            if (id == 0)
                throw new ArgumentException("user id must not be zero.");
            if (token == null)
                throw new ArgumentNullException("token must not be null.");
            this._id = id;
            this._unreliableScreenName = screen;
            this._accessToken = token;
        }

        private AccessToken _accessToken;
        /// <summary>
        /// Token and secret
        /// </summary>
        [IgnoreDataMember, XmlIgnore, DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AccessToken AccessToken
        {
            get { return _accessToken; }
            set { _accessToken = value; }
        }

        /// <summary>
        /// For serialization.
        /// </summary>
        [DataMember]
        public string KeyAndSecret
        {
            get { return AccessToken.Key + Environment.NewLine + AccessToken.Secret; }
            set
            {
                var splited = value.Split(Environment.NewLine.ToArray(), StringSplitOptions.RemoveEmptyEntries);
                AccessToken = new AccessToken(splited[0], splited[1]);
            }
        }

        private long _id;
        /// <summary>
        /// User Numerical ID
        /// </summary>
        [DataMember]
        public long Id
        {
            get { return _id; }
            set { _id = value; }
        }

        private string _unreliableScreenName;
        /// <summary>
        /// Provide screen name, but this property is unreliable.
        /// </summary>
        [DataMember]
        public string UnreliableScreenName
        {
            get { return _unreliableScreenName; }
            set { _unreliableScreenName = value; }
        }

        private string _unreliableProfileImageUriString;
        /// <summary>
        /// Provide profile image uri, but this property is unreliable.
        /// </summary>
        [DataMember]
        public string UnreliableProfileImageUriString
        {
            get { return _unreliableProfileImageUriString; }
            set { _unreliableProfileImageUriString = value; }
        }

        /// <summary>
        /// Provide exact profile image uri, but this property is unreliable.
        /// </summary>
        [IgnoreDataMember, XmlIgnore, DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Uri UnreliableProfileImageUri
        {
            get
            {
                if (String.IsNullOrEmpty(this._unreliableProfileImageUriString))
                {
                    return null;
                }
                else
                {
                    Uri result;
                    if (Uri.TryCreate(this._unreliableProfileImageUriString, UriKind.RelativeOrAbsolute, out result))
                        return result;
                    else
                        return null;
                }
            }
        }

        /// <summary>
        /// Rate Limiting max value per period
        /// </summary>
        [IgnoreDataMember, XmlIgnore, DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int RateLimitMax { get; set; }

        /// <summary>
        /// Rate limiting remain value of current period
        /// </summary>
        [IgnoreDataMember, XmlIgnore, DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int RateLimitRemaining { get; set; }

        /// <summary>
        /// Time next period of rate limit..
        /// </summary>
        [IgnoreDataMember, XmlIgnore, DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DateTime RateLimitReset { get; set; }

        /// <summary>
        /// Exact info
        /// </summary>
        [IgnoreDataMember, XmlIgnore, DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public TwitterUser UserInfo { get; set; }
    }
}