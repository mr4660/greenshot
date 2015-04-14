﻿/*
 * A Picasa Plugin for Greenshot
 * Copyright (C) 2011  Francis Noel
 * 
 * For more information see: http://getgreenshot.org/
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 1 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Xml;
using Greenshot.IniFile;
using Greenshot.Plugin;
using GreenshotPlugin.Core;
using System.Net;

namespace GreenshotPicasaPlugin {
	/// <summary>
	/// Description of PicasaUtils.
	/// </summary>
	public static class PicasaUtils {
		private const string PicasaScope = "https://picasaweb.google.com/data/";
		private static readonly log4net.ILog LOG = log4net.LogManager.GetLogger(typeof(PicasaUtils));
		private static readonly PicasaConfiguration Config = IniConfig.GetIniSection<PicasaConfiguration>();
		private const string AuthUrl = "https://accounts.google.com/o/oauth2/auth?response_type={response_type}&client_id={ClientId}&redirect_uri={RedirectUrl}&state={State}&scope={scope}";
		private const string TokenUrl = "https://www.googleapis.com/oauth2/v3/token";
		private const string UploadUrl = "https://picasaweb.google.com/data/feed/api/user/{0}/albumid/{1}";

		/// <summary>
		/// Authenticate by using the LocalServerCodeReceiver
		/// If this works, generate a token
		/// </summary>
		/// <param name="settings"></param>
		private static void Authenticate(OAuth2Settings settings) {
			var codeReceiver = new LocalServerCodeReceiver();
			IDictionary<string, string> result = codeReceiver.ReceiveCode(settings);

			string code;
			if (result.TryGetValue("code", out code)) {
				GenerateToken(code, settings);
			}
			string error;
			if (result.TryGetValue("error", out error)) {
				if ("access_denied" == error) {
					throw new UnauthorizedAccessException("Access denied");
				} else {
					throw new Exception(error);
				}
			}
		}

		/// <summary>
		/// Upload parameters by post
		/// </summary>
		/// <param name="url"></param>
		/// <param name="parameters"></param>
		/// <returns>response</returns>
		public static string HttpPost(string url, IDictionary<string, object> parameters, OAuth2Settings settings) {
			var webRequest = (HttpWebRequest)NetworkHelper.CreateWebRequest(url);
			webRequest.Method = "POST";
			webRequest.KeepAlive = true;
			webRequest.Credentials = CredentialCache.DefaultCredentials;

			if (!string.IsNullOrEmpty(settings.AccessToken)) {
				webRequest.Headers.Add("Authorization", "Bearer " + settings.AccessToken);
			}
			return NetworkHelper.UploadFormUrlEncoded(webRequest, parameters);
		}

		private static void GenerateToken(string code, OAuth2Settings settings) {
			// Use the returned code to get a refresh code
			IDictionary<string, object> data = new Dictionary<string, object>();
			data.Add("code", code);
			data.Add("client_id", settings.ClientId);
			data.Add("redirect_uri", settings.RedirectUrl);
			data.Add("client_secret", settings.ClientSecret);
			data.Add("grant_type", "authorization_code");

			var accessTokenJsonResult = HttpPost(settings.FormattedTokenUrl, data, settings);
			IDictionary<string, object> refreshTokenResult = JSONHelper.JsonDecode(accessTokenJsonResult);
			// gives as described here: https://developers.google.com/identity/protocols/OAuth2InstalledApp
			//  "access_token":"1/fFAGRNJru1FTz70BzhT3Zg",
			//	"expires_in":3920,
			//	"token_type":"Bearer",
			//	"refresh_token":"1/xEoDL4iW3cxlI7yDbSRFYNG01kVKM2C-259HOF2aQbI"
			settings.AccessToken = (string)refreshTokenResult["access_token"] as string;
			settings.RefreshToken = (string)refreshTokenResult["refresh_token"] as string;

			object seconds = refreshTokenResult["expires_in"];
			if (seconds != null) {
				settings.AccessTokenExpires = DateTimeOffset.Now.AddSeconds((double)seconds);
			}
		}

		/// <summary>
		/// Go out and retrieve a new access token via refresh-token with the TokenUrl in the settings
		/// Will upate the access token, refresh token, expire date
		/// </summary>
		/// <param name="settings"></param>
		private static void GenerateAccessToken(OAuth2Settings settings) {
			IDictionary<string, object> data = new Dictionary<string, object>();
			data.Add("refresh_token", settings.RefreshToken);
			data.Add("client_id", settings.ClientId);
			data.Add("client_secret", settings.ClientSecret);
			data.Add("grant_type", "refresh_token");

			var accessTokenJsonResult = HttpPost(settings.FormattedTokenUrl, data, settings);
			// gives as described here: https://developers.google.com/identity/protocols/OAuth2InstalledApp
			//  "access_token":"1/fFAGRNJru1FTz70BzhT3Zg",
			//	"expires_in":3920,
			//	"token_type":"Bearer",

			IDictionary<string, object> accessTokenResult = JSONHelper.JsonDecode(accessTokenJsonResult);
			settings.AccessToken = (string)accessTokenResult["access_token"] as string;
			object seconds = accessTokenResult["expires_in"];
			if (seconds != null) {
				settings.AccessTokenExpires = DateTimeOffset.Now.AddSeconds((double)seconds);
			}
		}

		/// <summary>
		/// Do the actual upload to Picasa
		/// </summary>
		/// <param name="surfaceToUpload">Image to upload</param>
		/// <param name="outputSettings"></param>
		/// <param name="title"></param>
		/// <param name="filename"></param>
		/// <returns>PicasaResponse</returns>
		public static string UploadToPicasa(ISurface surfaceToUpload, SurfaceOutputSettings outputSettings, string title, string filename) {
			// Fill the OAuth2Settings
			OAuth2Settings settings = new OAuth2Settings();
			settings.AuthUrlPattern = AuthUrl;
			settings.TokenUrlPattern = TokenUrl;
			settings.AdditionalAttributes.Add("response_type", "code");
			settings.AdditionalAttributes.Add("scope", PicasaScope);
			settings.ClientId = PicasaCredentials.ClientId;
			settings.ClientSecret = PicasaCredentials.ClientSecret;

			// Copy the settings from the config, which is kept in memory
			settings.RefreshToken = Config.RefreshToken;
			settings.AccessToken = Config.AccessToken;
			settings.AccessTokenExpires = Config.AccessTokenExpires;

			try {
				// Get Refresh / Access token
				if (string.IsNullOrEmpty(settings.RefreshToken)) {
					Authenticate(settings);
				} 

				if (settings.IsAccessTokenExpired) {
					GenerateAccessToken(settings);
				}

				var webRequest = (HttpWebRequest)NetworkHelper.CreateWebRequest(string.Format(UploadUrl, Config.UploadUser, Config.UploadAlbum));
				webRequest.Method = "POST";
				webRequest.KeepAlive = true;
				webRequest.Credentials = CredentialCache.DefaultCredentials;
				webRequest.Headers.Add("Authorization", "Bearer " + settings.AccessToken);
				if (Config.AddFilename) {
					webRequest.Headers.Add("Slug", NetworkHelper.EscapeDataString(filename));
				}
				SurfaceContainer container = new SurfaceContainer(surfaceToUpload, outputSettings, filename);
				container.Upload(webRequest);
				
				string response = NetworkHelper.GetResponse(webRequest);

				return ParseResponse(response);
			} finally {
				// Copy the settings back to the config
				Config.RefreshToken = settings.RefreshToken;
				Config.AccessToken = settings.AccessToken;
				Config.AccessTokenExpires = settings.AccessTokenExpires;
			}
		}
		
		/// <summary>
		/// Parse the upload URL from the response
		/// </summary>
		/// <param name="response"></param>
		/// <returns></returns>
		public static string ParseResponse(string response) {
			if (response == null) {
				return null;
			}
			try {
				XmlDocument doc = new XmlDocument();
				doc.LoadXml(response);
				XmlNodeList nodes = doc.GetElementsByTagName("link", "*");
				if(nodes.Count > 0) {
					string url = null;
					foreach(XmlNode node in nodes) {
						if (node.Attributes != null) {
							url = node.Attributes["href"].Value;
							string rel = node.Attributes["rel"].Value;
							// Pictures with rel="http://schemas.google.com/photos/2007#canonical" are the direct link
							if (rel != null && rel.EndsWith("canonical")) {
								break;
							}
						}
					}
					return url;
				}
			} catch(Exception e) {
				LOG.ErrorFormat("Could not parse Picasa response due to error {0}, response was: {1}", e.Message, response);
			}
			return null;
		}
	}
}
