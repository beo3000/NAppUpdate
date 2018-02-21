using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using NAppUpdate.Framework.Common;
using NAppUpdate.Framework.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace NAppUpdate.Framework.Tasks
{
	[UpdateTaskAlias("zipUpdate")]
	public class UpdateFromZipTask : UpdateTaskBase
	{

		public UpdateFromZipTask()
		{
			UpdateConditions = new Conditions.BooleanCondition();
		}


		[NauField("updateTo",
			"File name on the remote location; same name as local path will be used if left blank"
			, true)]
		public string UpdateTo { get; set; }

		[NauField("sha256-checksum", "SHA-256 checksum to validate the file after download (optional)", false)]
		public string Sha256Checksum { get; set; }

		internal string updateDirectory { get; set; } = Path.Combine(UpdateManager.Instance.Config.TempFolder, Guid.NewGuid().ToString());
		internal string destinationPath { get; private set; } = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);


		public List<string> filesList { get; private set; }

		//public string Description { get; set; }

		//public Conditions.BooleanCondition UpdateConditions { get; set; }

		/// <summary>
		/// Do all work, especially if it is lengthy, required to prepare the update task, except from
		/// the final trivial operations required to actually perform the update.
		/// </summary>
		/// <param name="source">An update source object, in case more data is required</param>
		/// <returns>True if successful, false otherwise</returns>
		public override void Prepare(Sources.IUpdateSource source)
		{
			//http://www.360doc.com/content/13/0830/18/11482448_311009019.shtml
			var assemblyFile = new FileInfo(GetType().Assembly.Location);
			UpdateManager.Instance.Config.DependenciesForColdUpdate = new List<string> { assemblyFile.Name };

			if (string.IsNullOrEmpty(UpdateTo))
				return;

			// Clear temp folder
			if (Directory.Exists(updateDirectory))
			{
				try
				{
					Directory.Delete(updateDirectory, true);
				}
				catch { }
			}

			Directory.CreateDirectory(updateDirectory);

			// Download the zip to a temp file that is deleted automatically when the app exits
			string zipLocation = null;
			try
			{
				if (!source.GetData(UpdateTo, /*UpdateManager.Instance.BaseUrl*/ null, OnProgress, ref zipLocation))
					return;
			}
			catch (Exception ex)
			{
				throw new UpdateProcessFailedException("Couldn't get Data from source", ex);
			}

			if (!string.IsNullOrEmpty(Sha256Checksum))
			{
				string checksum = Utils.FileChecksum.GetSHA256Checksum(zipLocation);
				if (!checksum.Equals(Sha256Checksum))
					return;
			}

			if (string.IsNullOrEmpty(zipLocation))
				return;

			// Unzip to temp folder; no need to delete the zip file as this will be done by the OS
			filesList = new List<string>();
			ZipFile zf = null;
			try
			{
				//int defaultCodePage = System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.OEMCodePage;
				ZipConstants.DefaultCodePage = 850;
				//MessageBox.Show("Default Code Page is " + defaultCodePage);

				FileStream fs = File.OpenRead(zipLocation);
				zf = new ZipFile(fs);
				foreach (ZipEntry zipEntry in zf)
				{
					if (!zipEntry.IsFile)
					{
						continue;           // Ignore directories
					}
					String entryFileName = zipEntry.Name;
					// to remove the folder from the entry:- entryFileName = Path.GetFileName(entryFileName);
					// Optionally match entrynames against a selection list here to skip as desired.
					// The unpacked length is available in the zipEntry.Size property.

					byte[] buffer = new byte[4096];     // 4K is optimum
					Stream zipStream = zf.GetInputStream(zipEntry);

					// Manipulate the output filename here as desired.
					String fullZipToPath = Path.Combine(updateDirectory, entryFileName);
					string directoryName = Path.GetDirectoryName(fullZipToPath);
					if (directoryName.Length > 0)
						Directory.CreateDirectory(directoryName);

					// Unzip file in buffered chunks. This is just as fast as unpacking to a buffer the full size
					// of the file, but does not waste memory.
					// The "using" will close the stream even if an exception occurs.
					using (FileStream streamWriter = File.Create(fullZipToPath))
					{
						StreamUtils.Copy(zipStream, streamWriter, buffer);
					}
					filesList.Add(entryFileName);

					//UpdateManager.Instance.Tasks
				}
				fs.Close();
				return;
			}
			catch (Exception ex)
			{
				throw new UpdateProcessFailedException("Couldn't get unzip data", ex);
			}
			finally
			{
				if (zf != null)
				{
					zf.IsStreamOwner = true; // Makes close also shut the underlying stream
					zf.Close(); // Ensure we release resources
				}

			}
		}

		/// <summary>
		/// Execute the update. After all preparation is done, this call should be quite a short one
		/// to perform.
		/// </summary>
		/// <returns>True if successful, false otherwise</returns>
		public override TaskExecutionStatus Execute(bool coldRun)
		{
			//Creates a backup of files that are going to be overwritten
			try
			{
				foreach (string file in filesList)
				{
					if (File.Exists(file))
					{
						if (!Directory.Exists(Path.GetDirectoryName(Path.Combine(UpdateManager.Instance.Config.BackupFolder, file))))
						{
							string backupPath = Path.GetDirectoryName(Path.Combine(UpdateManager.Instance.Config.BackupFolder, file));
							Utils.FileSystem.CreateDirectoryStructure(backupPath, false);
						}
						var _backupFile = Path.Combine(UpdateManager.Instance.Config.BackupFolder, file);
						var _destinationFile = Path.Combine(destinationPath, file);
						File.Copy(_destinationFile, _backupFile, true);
					}
					else
					{
						//new file, no backup is needed
					}
				}
			}
			catch (Exception e)
			{
				//Issue at backup
			}

			try
			{
				foreach (string file in filesList)
				{
					var _destinationFile = Path.Combine(destinationPath, file);
					var _tempFile = Path.Combine(updateDirectory, file);

					FileLockWait(_destinationFile);

					if (File.Exists(_destinationFile))
					{

						//FileSystem.(new FileInfo(_destinationFile), new FileInfo(_tempFile));

						File.Delete(_destinationFile);
					}
					File.Move(_tempFile, _destinationFile);
					_tempFile = null;
				}
			}
			catch (Exception ex)
			{
				ExecutionStatus = TaskExecutionStatus.Failed;
				throw new UpdateProcessFailedException("Could not replace the file", ex);
			}

			//return TaskExecutionStatus.Successful;

			if (!Utils.PermissionsCheck.HaveWritePermissionsForFileOrFolder(destinationPath))
			{
				return TaskExecutionStatus.RequiresPrivilegedAppRestart;
			}
			return TaskExecutionStatus.RequiresAppRestart;
		}

		public IEnumerator<KeyValuePair<string, object>> GetColdUpdates()
		{
			if (filesList == null)
				yield break;

			foreach (var file in filesList)
			{
				yield return new KeyValuePair<string, object>(file, Path.Combine(updateDirectory, file));
			}

		}

		/// <summary>
		/// Rollback the update performed by this task.
		/// </summary>
		/// <returns>True if successful, false otherwise</returns>
		public override bool Rollback()
		{
			return true;
		}

		/// <summary>
		/// To mitigate problems with the files being locked even though the application mutex has been released.
		/// https://github.com/synhershko/NAppUpdate/issues/35
		/// </summary>
		private void FileLockWait(string destinationFile)
		{
			int attempt = 0;
			while (FileSystem.IsFileLocked(new FileInfo(destinationFile)))
			{
				Thread.Sleep(500);
				attempt++;
				if (attempt == 10)
				{
					throw new UpdateProcessFailedException("Failed to update, the file is locked: " + destinationFile);
				}
			}
		}
	}
}
