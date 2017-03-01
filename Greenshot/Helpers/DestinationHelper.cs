﻿#region Greenshot GNU General Public License

// Greenshot - a free and open source screenshot tool
// Copyright (C) 2007-2017 Thomas Braun, Jens Klingen, Robin Krom
// 
// For more information see: http://getgreenshot.org/
// The Greenshot project is hosted on GitHub https://github.com/greenshot/greenshot
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 1 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

#endregion

#region Usings

using System;
using System.Collections.Generic;
using System.Linq;
using GreenshotPlugin.Core;
using GreenshotPlugin.IniFile;
using GreenshotPlugin.Interfaces;
using Dapplo.Log;

#endregion

namespace Greenshot.Helpers
{
	/// <summary>
	///     Description of DestinationHelper.
	/// </summary>
	public static class DestinationHelper
	{
		private static readonly LogSource Log = new LogSource();
		private static readonly Dictionary<string, IDestination> RegisteredDestinations = new Dictionary<string, IDestination>();
		private static readonly CoreConfiguration CoreConfig = IniConfig.GetIniSection<CoreConfiguration>();

		/// Initialize the destinations
		static DestinationHelper()
		{
			foreach (var destinationType in InterfaceUtils.GetSubclassesOf(typeof(IDestination), true))
			{
				// Only take our own
				if (!"Greenshot.Destinations".Equals(destinationType.Namespace))
				{
					continue;
				}
				if (destinationType.IsAbstract)
				{
					continue;
				}
				IDestination destination;
				try
				{
					destination = (IDestination) Activator.CreateInstance(destinationType);
				}
				catch (Exception e)
				{
					Log.Error().WriteLine("Can't create instance of {0}", destinationType);
					Log.Error().WriteLine(e);
					continue;
				}
				if (destination.IsActive)
				{
					Log.Debug().WriteLine("Found destination {0} with designation {1}", destinationType.Name, destination.Designation);
					RegisterDestination(destination);
				}
				else
				{
					Log.Debug().WriteLine("Ignoring destination {0} with designation {1}", destinationType.Name, destination.Designation);
				}
			}
		}

		/// <summary>
		///     Register your destination here, if it doesn't come from a plugin and needs to be available
		/// </summary>
		/// <param name="destination"></param>
		public static void RegisterDestination(IDestination destination)
		{
			if (CoreConfig.ExcludeDestinations == null || !CoreConfig.ExcludeDestinations.Contains(destination.Designation))
			{
				// don't test the key, an exception should happen wenn it's not unique
				RegisteredDestinations.Add(destination.Designation, destination);
			}
		}

		/// <summary>
		///     Method to get all the destinations from the plugins
		/// </summary>
		/// <returns>List of IDestination</returns>
		private static IEnumerable<IDestination> GetPluginDestinations()
		{
			var destinations = new List<IDestination>();
			foreach (var pluginAttribute in PluginHelper.Instance.Plugins.Keys)
			{
				var plugin = PluginHelper.Instance.Plugins[pluginAttribute];
				try
				{
					destinations
						.AddRange(plugin.Destinations()
						.Where(destination => CoreConfig.ExcludeDestinations == null || !CoreConfig.ExcludeDestinations.Contains(destination.Designation)));
				}
				catch (Exception ex)
				{
					Log.Error().WriteLine("Couldn't get destinations from the plugin {0}", pluginAttribute.Name);
					Log.Error().WriteLine(ex);
				}
			}
			destinations.Sort();
			return destinations;
		}

		/// <summary>
		///     Get a list of all destinations, registered or supplied by a plugin
		/// </summary>
		/// <returns></returns>
		public static List<IDestination> GetAllDestinations()
		{
			var destinations = new List<IDestination>();
			destinations.AddRange(RegisteredDestinations.Values);
			destinations.AddRange(GetPluginDestinations());
			destinations.Sort();
			return destinations;
		}

		/// <summary>
		///     Get a destination by a designation
		/// </summary>
		/// <param name="designation">Designation of the destination</param>
		/// <returns>IDestination or null</returns>
		public static IDestination GetDestination(string designation)
		{
			if (designation == null)
			{
				return null;
			}
			if (RegisteredDestinations.ContainsKey(designation))
			{
				return RegisteredDestinations[designation];
			}
			foreach (var destination in GetPluginDestinations())
			{
				if (designation.Equals(destination.Designation))
				{
					return destination;
				}
			}
			return null;
		}

		/// <summary>
		///     A simple helper method which will call ExportCapture for the destination with the specified designation
		/// </summary>
		/// <param name="manuallyInitiated"></param>
		/// <param name="designation"></param>
		/// <param name="surface"></param>
		/// <param name="captureDetails"></param>
		public static ExportInformation ExportCapture(bool manuallyInitiated, string designation, ISurface surface, ICaptureDetails captureDetails)
		{
			var destination = GetDestination(designation);
			if (destination != null && destination.IsActive)
			{
				return destination.ExportCapture(manuallyInitiated, surface, captureDetails);
			}
			return null;
		}
	}
}