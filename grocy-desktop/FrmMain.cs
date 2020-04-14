﻿using CefSharp;
using CefSharp.WinForms;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Resources;
using System.Threading;
using System.Windows.Forms;

namespace GrocyDesktop
{
	public partial class FrmMain : Form
	{
		public FrmMain()
		{
			InitializeComponent();
		}

		private ResourceManager ResourceManager = new ResourceManager(typeof(FrmMain));
		private ChromiumWebBrowser GrocyBrowser;
		private ChromiumWebBrowser BarcodeBuddyBrowser;
		private PhpDevelopmentServerManager GrocyPhpServer;
		private PhpDevelopmentServerManager BarcodeBuddyPhpServer;
		private GrocyEnvironmentManager GrocyEnvironmentManager;
		private BarcodeBuddyEnvironmentManager BarcodeBuddyEnvironmentManager;
		private PhpProcessManager BarcodeBuddyWebsocketServerPhpProcess;
		private UserSettings UserSettings = UserSettings.Load();

		private void SetupCef()
		{
			Cef.EnableHighDPISupport();
			
			CefSettings cefSettings = new CefSettings();
			cefSettings.BrowserSubprocessPath = Path.Combine(GrocyDesktopDependencyManager.CefExecutingPath, @"x86\CefSharp.BrowserSubprocess.exe");
			cefSettings.CachePath = GrocyDesktopDependencyManager.CefCachePath;
			cefSettings.LogFile = Path.Combine(GrocyDesktopDependencyManager.CefCachePath, "cef.log");
			cefSettings.CefCommandLineArgs.Add("--enable-media-stream", "");
			cefSettings.CefCommandLineArgs.Add("--unsafely-treat-insecure-origin-as-secure", this.GrocyPhpServer.LocalUrl);
			cefSettings.CefCommandLineArgs.Add("--lang", CultureInfo.CurrentCulture.TwoLetterISOLanguageName);
			Cef.Initialize(cefSettings, performDependencyCheck: false, browserProcessHandler: null);

			if (this.UserSettings.EnableBarcodeBuddyIntegration)
			{
				this.GrocyBrowser = new ChromiumWebBrowser(this.GrocyPhpServer.LocalUrl);
				this.GrocyBrowser.Dock = DockStyle.Fill;
				this.TabPage_Grocy.Controls.Add(this.GrocyBrowser);

				this.BarcodeBuddyBrowser = new ChromiumWebBrowser(this.BarcodeBuddyPhpServer.LocalUrl);
				this.BarcodeBuddyBrowser.Dock = DockStyle.Fill;
				this.TabPage_BarcodeBuddy.Controls.Add(this.BarcodeBuddyBrowser);
			}
			else
			{
				this.TabControl_Main.Visible = false;
				this.ToolStripMenuItem_BarcodeBuddy.Visible = false;

				this.GrocyBrowser = new ChromiumWebBrowser(this.GrocyPhpServer.LocalUrl);
				this.GrocyBrowser.Dock = DockStyle.Fill;
				this.Panel_Main.Controls.Add(this.GrocyBrowser);
			}

			this.StatusStrip_Main.Visible = this.UserSettings.EnableExternalWebserverAccess;
		}

		private void SetupGrocy()
		{
			this.GrocyPhpServer = new PhpDevelopmentServerManager(GrocyDesktopDependencyManager.PhpExecutingPath, Path.Combine(GrocyDesktopDependencyManager.GrocyExecutingPath, "public"), this.UserSettings.EnableExternalWebserverAccess, null, this.UserSettings.GrocyWebserverDesiredPort);
			this.GrocyPhpServer.StartServer();
			this.GrocyEnvironmentManager = new GrocyEnvironmentManager(GrocyDesktopDependencyManager.GrocyExecutingPath, this.UserSettings.GrocyDataLocation);
			this.GrocyEnvironmentManager.Setup();
		}

		private void SetupBarcodeBuddy()
		{
			this.BarcodeBuddyEnvironmentManager = new BarcodeBuddyEnvironmentManager(GrocyDesktopDependencyManager.BarcodeBuddyExecutingPath, this.UserSettings.BarcodeBuddyDataLocation);
			this.BarcodeBuddyPhpServer = new PhpDevelopmentServerManager(GrocyDesktopDependencyManager.PhpExecutingPath, GrocyDesktopDependencyManager.BarcodeBuddyExecutingPath, this.UserSettings.EnableExternalWebserverAccess, null, this.UserSettings.BarcodeBuddyWebserverDesiredPort);
			this.BarcodeBuddyEnvironmentManager.Setup(this.GrocyPhpServer.LocalUrl.TrimEnd('/') + "/api/");
			this.BarcodeBuddyPhpServer.SetEnvironmenVariables(this.BarcodeBuddyEnvironmentManager.GetEnvironmentVariables());
			this.BarcodeBuddyPhpServer.StartServer();

			this.BarcodeBuddyWebsocketServerPhpProcess = new PhpProcessManager(GrocyDesktopDependencyManager.PhpExecutingPath, GrocyDesktopDependencyManager.BarcodeBuddyExecutingPath, "wsserver.php");
			this.BarcodeBuddyWebsocketServerPhpProcess.Start();
		}

		private async void FrmMain_Shown(object sender, EventArgs e)
		{
			await GrocyDesktopDependencyManager.UnpackIncludedDependenciesIfNeeded(this.UserSettings, this);
			this.SetupGrocy();
			if (this.UserSettings.EnableBarcodeBuddyIntegration)
			{
				this.SetupBarcodeBuddy();
			}
			this.SetupCef();

			this.ToolStripMenuItem_EnableBarcodeBuddy.Checked = this.UserSettings.EnableBarcodeBuddyIntegration;
			this.ToolStripMenuItem_EnableExternalAccess.Checked = this.UserSettings.EnableExternalWebserverAccess;

			string externalAccessInfo = string.Empty;
			if (this.UserSettings.EnableBarcodeBuddyIntegration)
			{
				this.ToolStripStatusLabel_ExternalAccessInfo.Text = this.ResourceManager.GetString("STRING_GrocyAndBarcodeBuddyExternalAccessInfo.Text")
					.Replace("%1$s", this.GrocyPhpServer.HostnameUrl)
					.Replace("%2$s", this.GrocyPhpServer.IpUrl)
					.Replace("%3$s", this.BarcodeBuddyPhpServer.HostnameUrl)
					.Replace("%4$s", this.BarcodeBuddyPhpServer.IpUrl);
			}
			else
			{
				this.ToolStripStatusLabel_ExternalAccessInfo.Text = this.ResourceManager.GetString("STRING_GrocyExternalAccessInfo.Text")
					.Replace("%1$s", this.GrocyPhpServer.HostnameUrl)
					.Replace("%2$s", this.GrocyPhpServer.IpUrl);
			}
		}

		private void FrmMain_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (this.GrocyPhpServer != null)
			{
				this.GrocyPhpServer.StopServer();
			}

			if (this.UserSettings.EnableBarcodeBuddyIntegration && this.BarcodeBuddyPhpServer != null)
			{
				this.BarcodeBuddyPhpServer.StopServer();
			}

			if (this.UserSettings.EnableBarcodeBuddyIntegration && this.BarcodeBuddyWebsocketServerPhpProcess != null)
			{
				this.BarcodeBuddyWebsocketServerPhpProcess.Stop();
			}

			this.UserSettings.Save();
		}

		private void ToolStripMenuItem_Exit_Click(object sender, EventArgs e)
		{
			this.Close();
		}

		private void ToolStripMenuItem_ShowPhpServerOutput_Click(object sender, EventArgs e)
		{
			new FrmShowText("grocy " + this.ResourceManager.GetString("STRING_PHPServerOutput.Text"), this.GrocyPhpServer.GetConsoleOutput()).Show(this);
			if (this.UserSettings.EnableBarcodeBuddyIntegration)
			{
				new FrmShowText("Barcode Buddy " + this.ResourceManager.GetString("STRING_PHPServerOutput.Text"), this.BarcodeBuddyPhpServer.GetConsoleOutput()).Show(this);
				new FrmShowText("Barcode Buddy " + this.ResourceManager.GetString("STRING_PHPServerOutput.Text") + " (Websocket Server)", this.BarcodeBuddyWebsocketServerPhpProcess.GetConsoleOutput()).Show(this);
			}
		}

		private void ToolStripMenuItem_ShowBrowserDeveloperTools_Click(object sender, EventArgs e)
		{
			this.GrocyBrowser.ShowDevTools();

			if (this.UserSettings.EnableBarcodeBuddyIntegration)
			{
				this.BarcodeBuddyBrowser.ShowDevTools();
			}
		}

		private void ToolStripMenuItem_About_Click(object sender, EventArgs e)
		{
			new FrmAbout().ShowDialog(this);
		}

		private async void ToolStripMenuItem_UpdateGrocy_Click(object sender, EventArgs e)
		{
			this.GrocyPhpServer.StopServer();
			Thread.Sleep(2000); // Just give php.exe some time to stop...
			await GrocyDesktopDependencyManager.UpdateEmbeddedGrocyRelease(this);
			this.GrocyPhpServer.StartServer();
			this.GrocyEnvironmentManager.Setup();
			this.GrocyBrowser.Load(this.GrocyPhpServer.LocalUrl);
		}

		private void ToolStripMenuItem_RecreateGrocyDatabase_Click(object sender, EventArgs e)
		{
			if (MessageBox.Show(this.ResourceManager.GetString("STRING_ThisWillDeleteAndRecreateTheGrocyDatabaseMeansAllYourDataWillBeWipedReallyContinue.Text"), this.ResourceManager.GetString("ToolStripMenuItem_RecreateGrocyDatabase.Text"), MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
			{
				this.GrocyPhpServer.StopServer();
				Thread.Sleep(2000); // Just give php.exe some time to stop...
				File.Delete(Path.Combine(this.UserSettings.GrocyDataLocation, "grocy.db"));
				Extensions.RestartApp();
			}
		}

		private async void ToolStripMenuItem_UpdateBarcodeBuddy_Click(object sender, EventArgs e)
		{
			this.BarcodeBuddyPhpServer.StopServer();
			Thread.Sleep(2000); // Just give php.exe some time to stop...
			await GrocyDesktopDependencyManager.UpdateEmbeddedBarcodeBuddyRelease(this);
			this.BarcodeBuddyPhpServer.StartServer();
			this.BarcodeBuddyEnvironmentManager.Setup(this.BarcodeBuddyPhpServer.LocalUrl);
			this.BarcodeBuddyBrowser.Load(this.BarcodeBuddyPhpServer.LocalUrl);
		}

		private void ToolStripMenuItem_EnableBarcodeBuddy_Click(object sender, EventArgs e)
		{
			this.UserSettings.EnableBarcodeBuddyIntegration = this.ToolStripMenuItem_EnableBarcodeBuddy.Checked;
			this.UserSettings.Save();
			Extensions.RestartApp();
		}

		private void ToolStripMenuItem_BackupDataGrocy_Click(object sender, EventArgs e)
		{
			using (SaveFileDialog dialog = new SaveFileDialog())
			{
				dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop).ToString();
				dialog.Filter = this.ResourceManager.GetString("STRING_ZipFiles.Text") + "|*.zip";
				dialog.CheckPathExists = true;
				dialog.DefaultExt = ".zip";
				dialog.FileName = "grocy-desktop-backup_grocy-data.zip";

				if (dialog.ShowDialog() == DialogResult.OK)
				{
					if (File.Exists(dialog.FileName))
					{
						File.Delete(dialog.FileName);
					}

					ZipFile.CreateFromDirectory(this.UserSettings.GrocyDataLocation, dialog.FileName);
					MessageBox.Show(this.ResourceManager.GetString("STRING_BackupSuccessfullyCreated.Text"), this.ResourceManager.GetString("STRING_Backup.Text"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
			}
		}

		private void ToolStripMenuItem_RestoreDataGrocy_Click(object sender, EventArgs e)
		{
			using (OpenFileDialog dialog = new OpenFileDialog())
			{
				dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop).ToString();
				dialog.Filter = this.ResourceManager.GetString("STRING_ZipFiles.Text") + "|*.zip";
				dialog.CheckPathExists = true;
				dialog.CheckFileExists = true;
				dialog.DefaultExt = ".zip";

				if (dialog.ShowDialog() == DialogResult.OK)
				{
					if (MessageBox.Show(this.ResourceManager.GetString("STRING_TheCurrentDataWillBeOverwrittenAndGrocydesktopWillRestartContinue.Text"), this.ResourceManager.GetString("STRING_Restore.Text"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
					{
						this.GrocyPhpServer.StopServer();
						Thread.Sleep(2000); // Just give php.exe some time to stop...
						Directory.Delete(this.UserSettings.GrocyDataLocation, true);
						Directory.CreateDirectory(this.UserSettings.GrocyDataLocation);
						ZipFile.ExtractToDirectory(dialog.FileName, this.UserSettings.GrocyDataLocation);
						Extensions.RestartApp();
					}
				}
			}
		}

		private void ToolStripMenuItem_ConfigureChangeDataLocationGrocy_Click(object sender, EventArgs e)
		{
			using (FolderBrowserDialog dialog = new FolderBrowserDialog())
			{
				dialog.RootFolder = Environment.SpecialFolder.Desktop;
				dialog.SelectedPath = this.UserSettings.GrocyDataLocation;

				if (dialog.ShowDialog() == DialogResult.OK)
				{
					if (MessageBox.Show(this.ResourceManager.GetString("STRING_GrocyDesktopWillRestartToApplyTheChangedSettingsContinue.Text"), this.ResourceManager.GetString("STRING_ChangeDataLocation.Text"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
					{
						this.GrocyPhpServer.StopServer();
						Extensions.CopyFolder(this.UserSettings.GrocyDataLocation, dialog.SelectedPath);
						Directory.Delete(this.UserSettings.GrocyDataLocation, true);
						this.UserSettings.GrocyDataLocation = dialog.SelectedPath;
						this.UserSettings.Save();
						Extensions.RestartApp();
					}
				}
			}
		}

		private void ToolStripMenuItem_BackupDataBarcodeBuddy_Click(object sender, EventArgs e)
		{
			using (SaveFileDialog dialog = new SaveFileDialog())
			{
				dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop).ToString();
				dialog.Filter = this.ResourceManager.GetString("STRING_ZipFiles.Text") + "|*.zip";
				dialog.CheckPathExists = true;
				dialog.DefaultExt = ".zip";
				dialog.FileName = "grocy-desktop-backup_barcodebuddy-data.zip";

				if (dialog.ShowDialog() == DialogResult.OK)
				{
					if (File.Exists(dialog.FileName))
					{
						File.Delete(dialog.FileName);
					}

					ZipFile.CreateFromDirectory(this.UserSettings.BarcodeBuddyDataLocation, dialog.FileName);
					MessageBox.Show(this.ResourceManager.GetString("STRING_BackupSuccessfullyCreated.Text"), this.ResourceManager.GetString("STRING_Backup.Text"), MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
			}
		}

		private void ToolStripMenuItem_RestoreDataBarcodeBuddy_Click(object sender, EventArgs e)
		{
			using (OpenFileDialog dialog = new OpenFileDialog())
			{
				dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop).ToString();
				dialog.Filter = this.ResourceManager.GetString("STRING_ZipFiles.Text") + "|*.zip";
				dialog.CheckPathExists = true;
				dialog.CheckFileExists = true;
				dialog.DefaultExt = ".zip";

				if (dialog.ShowDialog() == DialogResult.OK)
				{
					if (MessageBox.Show(this.ResourceManager.GetString("STRING_TheCurrentDataWillBeOverwrittenAndGrocydesktopWillRestartContinue.Text"), this.ResourceManager.GetString("STRING_Restore.Text"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
					{
						this.GrocyPhpServer.StopServer();
						Thread.Sleep(2000); // Just give php.exe some time to stop...
						Directory.Delete(this.UserSettings.BarcodeBuddyDataLocation, true);
						Directory.CreateDirectory(this.UserSettings.BarcodeBuddyDataLocation);
						ZipFile.ExtractToDirectory(dialog.FileName, this.UserSettings.BarcodeBuddyDataLocation);
						Extensions.RestartApp();
					}
				}
			}
		}

		private void ToolStripMenuItem_ConfigureChangeDataLocationBarcodeBuddy_Click(object sender, EventArgs e)
		{
			using (FolderBrowserDialog dialog = new FolderBrowserDialog())
			{
				dialog.RootFolder = Environment.SpecialFolder.Desktop;
				dialog.SelectedPath = this.UserSettings.BarcodeBuddyDataLocation;

				if (dialog.ShowDialog() == DialogResult.OK)
				{
					if (MessageBox.Show(this.ResourceManager.GetString("STRING_GrocyDesktopWillRestartToApplyTheChangedSettingsContinue.Text"), this.ResourceManager.GetString("STRING_ChangeDataLocation.Text"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
					{
						this.GrocyPhpServer.StopServer();
						Extensions.CopyFolder(this.UserSettings.BarcodeBuddyDataLocation, dialog.SelectedPath);
						Directory.Delete(this.UserSettings.BarcodeBuddyDataLocation, true);
						this.UserSettings.BarcodeBuddyDataLocation = dialog.SelectedPath;
						this.UserSettings.Save();
						Extensions.RestartApp();
					}
				}
			}
		}

		private void ToolStripMenuItem_EnableExternalAccess_Click(object sender, EventArgs e)
		{
			this.UserSettings.EnableExternalWebserverAccess = this.ToolStripMenuItem_EnableExternalAccess.Checked;
			this.UserSettings.Save();
			Extensions.RestartApp();
		}
	}
}
