using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;

using Nini.Config;
using log4net;

using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Handlers.Base;
using OpenMetaverse;

namespace OpenSim.Addons.AssetImage
{
	public class AssetImage : ServiceConnector
	{
		private IAssetImageService m_MapService;
		public bool m_Enabled;
		private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		private string m_ConfigName;
		public static string Url;

		public AssetImage(IConfigSource config, IHttpServer server, string configName) :
		base(config, server, configName)
		{
			
			IConfig m_ConfigName = config.Configs["AssetImageService"];
			if (m_ConfigName == null)
			{
				this.m_Enabled = false;
				m_log.DebugFormat("[AssetImageService]: Configuration Error Not Enabled", new object[0]);
				return;
			}
			this.m_Enabled = true;

			string @string = m_ConfigName.GetString("Url", string.Empty);
			if (@string == string.Empty )
			{
				this.m_Enabled = false;
				m_log.ErrorFormat("[AssetImageService]: missing service specifications Not Enabled", new object[0]);
				return;
			}
			if (this.m_Enabled)
			{
			Url = @string;
			server.AddStreamHandler(new AssetImageServerGetHandler(m_MapService));
			}
		}
	}

	class AssetImageServerGetHandler : BaseStreamHandler
	{
		public static ManualResetEvent ev = new ManualResetEvent(true);

		public AssetImageServerGetHandler(IAssetImageService service) :
		base("GET", "/image")
		{
			
		}

		protected override byte[] ProcessRequest(string path, Stream request, IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
		{
			ev.WaitOne();
			lock (ev)
			{
				ev.Reset();
			}

			UUID scopeID = UUID.Zero;

			string[] bits = path.Trim('/').Split(new char[] {'/'});
			if (bits.Length > 1)
			{
				scopeID = new UUID(bits[1]);

			}

			HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(AssetImage.Url.ToString() + scopeID.ToString());
			webRequest.Timeout = 30000; //30 Second Timeout

			try
			{
				HttpWebResponse webResponse = (HttpWebResponse)webRequest.GetResponse();
				StreamReader reader = new StreamReader(webResponse.GetResponseStream());
			
				httpResponse.StatusCode = (int)HttpStatusCode.OK;
				httpResponse.ContentType = "image/jpeg";
			
				lock (ev)
				{
					ev.Set();
				}

				var bytes = default(byte[]);
				using (var memstream = new MemoryStream())
				{
					reader.BaseStream.CopyTo(memstream);
					bytes = memstream.ToArray();
				}

				return bytes;
				}
			catch (WebException ex)
			{
				throw ex;
			}
		}
	}
}
