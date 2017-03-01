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
using GreenshotPlugin.Core;
using GreenshotPlugin.Interfaces;
using Dapplo.Log;

#endregion

namespace Greenshot.Helpers
{
	/// <summary>
	///     Description of ProcessorHelper.
	/// </summary>
	public static class ProcessorHelper
	{
		private static readonly LogSource Log = new LogSource();
		private static readonly Dictionary<string, IProcessor> RegisteredProcessors = new Dictionary<string, IProcessor>();

		/// Initialize the Processors
		static ProcessorHelper()
		{
			foreach (var ProcessorType in InterfaceUtils.GetSubclassesOf(typeof(IProcessor), true))
			{
				// Only take our own
				if (!"Greenshot.Processors".Equals(ProcessorType.Namespace))
				{
					continue;
				}
				try
				{
					if (!ProcessorType.IsAbstract)
					{
						IProcessor Processor;
						try
						{
							Processor = (IProcessor) Activator.CreateInstance(ProcessorType);
						}
						catch (Exception e)
						{
							Log.Error().WriteLine("Can't create instance of {0}", ProcessorType);
							Log.Error().WriteLine(e);
							continue;
						}
						if (Processor.isActive)
						{
							Log.Debug().WriteLine("Found Processor {0} with designation {1}", ProcessorType.Name, Processor.Designation);
							RegisterProcessor(Processor);
						}
						else
						{
							Log.Debug().WriteLine("Ignoring Processor {0} with designation {1}", ProcessorType.Name, Processor.Designation);
						}
					}
				}
				catch (Exception ex)
				{
					Log.Error().WriteLine("Error loading processor {0}, message: ", ProcessorType.FullName, ex.Message);
				}
			}
		}

		/// <summary>
		///     Register your Processor here, if it doesn't come from a plugin and needs to be available
		/// </summary>
		/// <param name="Processor"></param>
		public static void RegisterProcessor(IProcessor Processor)
		{
			// don't test the key, an exception should happen wenn it's not unique
			RegisteredProcessors.Add(Processor.Designation, Processor);
		}

		private static List<IProcessor> GetPluginsProcessors()
		{
			var processors = new List<IProcessor>();
			foreach (var pluginAttribute in PluginHelper.Instance.Plugins.Keys)
			{
				var plugin = PluginHelper.Instance.Plugins[pluginAttribute];
				try
				{
					var procs = plugin.Processors();
					if (procs != null)
					{
						processors.AddRange(procs);
					}
				}
				catch (Exception ex)
				{
					Log.Error().WriteLine("Couldn't get processors from the plugin {0}", pluginAttribute.Name);
					Log.Error().WriteLine(ex);
				}
			}
			processors.Sort();
			return processors;
		}

		/// <summary>
		///     Get a list of all Processors, registered or supplied by a plugin
		/// </summary>
		/// <returns></returns>
		public static List<IProcessor> GetAllProcessors()
		{
			var processors = new List<IProcessor>();
			processors.AddRange(RegisteredProcessors.Values);
			processors.AddRange(GetPluginsProcessors());
			processors.Sort();
			return processors;
		}

		/// <summary>
		///     Get a Processor by a designation
		/// </summary>
		/// <param name="designation">Designation of the Processor</param>
		/// <returns>IProcessor or null</returns>
		public static IProcessor GetProcessor(string designation)
		{
			if (designation == null)
			{
				return null;
			}
			if (RegisteredProcessors.ContainsKey(designation))
			{
				return RegisteredProcessors[designation];
			}
			foreach (var processor in GetPluginsProcessors())
			{
				if (designation.Equals(processor.Designation))
				{
					return processor;
				}
			}
			return null;
		}

		/// <summary>
		///     A simple helper method which will call ProcessCapture for the Processor with the specified designation
		/// </summary>
		/// <param name="designation"></param>
		/// <param name="surface"></param>
		/// <param name="captureDetails"></param>
		public static void ProcessCapture(string designation, ISurface surface, ICaptureDetails captureDetails)
		{
			if (RegisteredProcessors.ContainsKey(designation))
			{
				var Processor = RegisteredProcessors[designation];
				if (Processor.isActive)
				{
					Processor.ProcessCapture(surface, captureDetails);
				}
			}
		}
	}
}