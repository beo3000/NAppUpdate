//using ICSharpCode.SharpZipLib.Zip;
//using NAppUpdate.Framework.Tasks;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text;
//using System.Xml;

//namespace NAppUpdate.Framework.FeedReaders
//{
//	/// <summary>
//	///     My implementation of IUpdateFeedReader.
//	/// <para> 
//	///     It reads the files from each <see cref="UpdateFromZipTask"/> and retrieves each file as a <see cref="FileUpdateTask"></see>
//	/// </para>
//	/// </summary>
//	public class ZIPFeedReader : IUpdateFeedReader
//	{
//		private string _baseURL;

//		public IList<IUpdateTask> Read(string feed)
//		{
//			var doc = new XmlDocument();
//			doc.LoadXml(feed);
//			XmlNode root = doc.SelectSingleNode(@"/Feed[version=""1.0""] | /Feed") ?? doc;

//			if (root.Attributes["BaseUrl"] != null && !string.IsNullOrEmpty(root.Attributes["BaseUrl"].Value))
//				_baseURL = root.Attributes["BaseUrl"].Value;

//			XmlNodeList Tasks = root.ChildNodes;

//			var updateTasks = new List<IUpdateTask>();
//			foreach (XmlNode Task in Tasks)
//			{
//				//TODO get ZIP from server
//				using (var fs = new FileStream(@"C:\wamp64\www\AppDates\StarUpdater\DesktopApp.zip", FileMode.Open, FileAccess.Read))
//				{
//					ZipConstants.DefaultCodePage = 850;
//					using (var zf = new ZipFile(fs))
//					{
//						foreach (ZipEntry ze in zf)
//						{
//							if (ze.IsDirectory)
//							{
//								continue;
//							}
//							else
//							{
//								string[] AuxZipName = zf.Name.Split('\\');
//								string ZipFileName = AuxZipName[AuxZipName.Length - 1];
//								ZipFileUpdateTask NewFileTask = new ZipFileUpdateTask()
//								{
//									LocalPath = ze.Name,
//									UpdateTo = ze.Name,
//									CanHotSwap = false,
//									ZipFile = ZipFileName,
//									BaseUrl = _baseURL,
//								};
//								updateTasks.Add(NewFileTask);
//							}
//						}
//					}
//				}
//			}

//			return updateTasks;

//			/* foreach (XmlNode node in nodeList)
//             {
//                 var task = new SilentInstallerUpdateTask();
//                 task.Description = node["description"].InnerText;
//                 Description = task.Description;

//                 string url = node["enclosure"].Attributes["url"].Value;
//                 task.UpdateTo = url;

//                 Version = node["appcast:version"].InnerText;

//                 if (".exe".Equals(Path.GetExtension(url)) || ".msi".Equals(Path.GetExtension(url)))
//                 {
//                     string fileName = Path.GetFileName(url);
//                     task.LocalPath = fileName;
//                     //string baseDirectory = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
//                     //UpdateManager.Instance.ApplicationPath = Path.Combine(baseDirectory, fileName);

//                     var condition = new ApplicationVersionCondition(GetType().Assembly.GetName().Version.ToString());
//                     condition.Attributes.Add("version", Version);
//                     task.UpdateConditions.AddCondition(condition, BooleanCondition.ConditionType.AND);
//                 }
//                 else
//                 {
//                     var condition = new FileVersionCondition();
//                     condition.Version = Version;
//                     task.UpdateConditions.AddCondition(condition, BooleanCondition.ConditionType.AND);
//                 }

//                 updateTasks.Add(task);
//             }
//             */

//		}
//	}
//}
