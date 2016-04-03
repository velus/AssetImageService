using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Imaging;
//using OpenMetaverse.StructuredData;
using Mono.Addins;
using OpenSim.Framework;
//using OpenSim.Framework.Capabilities;
//using OpenSim.Framework.Monitoring;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
//using OpenSim.Region.CoreModules.World.Land;
//using Caps=OpenSim.Framework.Capabilities.Caps;
//using OSDArray=OpenMetaverse.StructuredData.OSDArray;
//using OSDMap=OpenMetaverse.StructuredData.OSDMap;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

[assembly: Addin("AssetImageModule", "0.1")]
[assembly: AddinDependency("OpenSim.Region.Framework", OpenSim.VersionInfo.VersionNumber)]

namespace OpenSim.Addons.AssetImage
{
	[Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "AssetImageModule")]
	public class AssetImageModule: INonSharedRegionModule, IAssetImageModule
	{

		protected Scene m_scene;
		private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		private byte[] myMapImageJPEG;

		#region INonSharedRegionModule Members
		public virtual void Initialise(IConfigSource config)
		{
			m_log.Debug("[IMAGE]: Starting");
		}

		public virtual void AddRegion(Scene scene)
		{
			lock (scene)
			{
				m_scene = scene;

				m_scene.RegisterModuleInterface<IAssetImageModule>(this);

				AddHandlers();
			}
		}

		public virtual void RemoveRegion(Scene scene)
		{
			
		}

		public virtual void RegionLoaded(Scene scene)
		{
			
		}

		public virtual void Close()
		{
		}

		public Hashtable OnHTTPThrottled(Hashtable keysvals)
		{
			Hashtable reply = new Hashtable();
			int statuscode = 500;
			reply["str_response_string"] = "";
			reply["int_response_code"] = statuscode;
			reply["content_type"] = "text/plain";
			return reply;
		}

		private static ImageCodecInfo GetEncoderInfo(String mimeType)
		{
			ImageCodecInfo[] encoders;
			encoders = ImageCodecInfo.GetImageEncoders();
			for (int j = 0; j < encoders.Length; ++j)
			{
				if (encoders[j].MimeType == mimeType)
					return encoders[j];
			}
			return null;
		}

		public Hashtable OnHTTPGetImage(Hashtable keysvals)
		{
			myMapImageJPEG = new byte[0];
			UUID scopeID = UUID.Zero;
			string regpath = keysvals["uri"].ToString();
			string[] bits = regpath.Trim('/').Split(new char[] {'='});
			if (bits.Length > 1)
			{
				scopeID = new UUID(bits[1]);

			}
			Hashtable reply = new Hashtable();
			int statuscode = 200;
			byte[] jpeg = new byte[0];

			if (scopeID != UUID.Zero)
			{
				
				if (myMapImageJPEG.Length == 0)
				{
					MemoryStream imgstream = null;
					Bitmap mapTexture = new Bitmap(1, 1);
					ManagedImage managedImage;
					Image image = (Image)mapTexture;

					try
					{
						// Taking our jpeg2000 data, decoding it, then saving it to a byte array with regular jpeg data

						imgstream = new MemoryStream();

						// non-async because we know we have the asset immediately.
						AssetBase mapasset = m_scene.AssetService.Get(scopeID.ToString());

						// Decode image to System.Drawing.Image
						if (OpenJPEG.DecodeToImage(mapasset.Data, out managedImage, out image))
						{
							// Save to bitmap
							mapTexture = new Bitmap(image);

							EncoderParameters myEncoderParameters = new EncoderParameters();
							myEncoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 95L);

							// Save bitmap to stream
							mapTexture.Save(imgstream, GetEncoderInfo("image/jpeg"), myEncoderParameters);

							// Write the stream to a byte array for output
							jpeg = imgstream.ToArray();
							myMapImageJPEG = jpeg;
						}
					}
					catch (Exception)
					{
						// Dummy!
						m_log.Warn("[IMAGE]: Unable to generate Image");
					}
					finally
					{
						// Reclaim memory, these are unmanaged resources
						// If we encountered an exception, one or more of these will be null
						if (mapTexture != null)
							mapTexture.Dispose();

						if (image != null)
							image.Dispose();

						if (imgstream != null)
							imgstream.Dispose();
					}
				}
				else
				{
					// Use cached version so we don't have to loose our mind
					jpeg = myMapImageJPEG;
				}
			}

			reply["str_response_string"] = Convert.ToBase64String(jpeg);
			reply["int_response_code"] = statuscode;
			reply["content_type"] = "image/jpeg";

			return reply;
		}

		private void AddHandlers()
		{
			

			string regionimage = "/ImageID=";
			regionimage = regionimage.Replace("-", "");
			MainServer.Instance.AddHTTPHandler(regionimage,
				new GenericHTTPDOSProtector(OnHTTPGetImage, OnHTTPThrottled, new BasicDosProtectorOptions()
					{
						AllowXForwardedFor = false,
						ForgetTimeSpan = TimeSpan.FromMinutes(2),
						MaxRequestsInTimeframe = 4,
						ReportingName = "IMAGEDOSPROTECTOR",
						RequestTimeSpan = TimeSpan.FromSeconds(10),
						ThrottledAction = BasicDOSProtector.ThrottleAction.DoThrottledMethod
					}).Process);
		}


		public Type ReplaceableInterface
		{
			get { return null; }
		}

		public virtual string Name
		{
			get { return "AssetImageModule"; }
		}

		#endregion
	}
}

