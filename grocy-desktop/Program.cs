using GrocyDesktop.Helpers;
using GrocyDesktop.Management;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace GrocyDesktop
{
	internal static class Program
	{
		internal static readonly string RunningVersion = Regex.Replace(Assembly.GetExecutingAssembly().GetName().Version.ToString(), @"^(.+?)(\.0+)$", "$1");
		internal static readonly string BaseExecutingPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location.TrimEnd('\\'));
		internal static readonly string PortableFilePath = Path.Combine(BaseExecutingPath, "portable");
		static bool IsValidPath(string path)
		{
			if (!Path.IsPathRooted(path))
			{
				return false;
			}

			char[] invalidPathChars = Path.GetInvalidPathChars();
			if (path.Any(c => invalidPathChars.Contains(c)))
			{
				return false;
			}

			return true;
		}


		internal static string BaseFixedUserDataFolderPath
		{
			get
			{
				if (UwpHelper.IsRunningAsUwp())
				{
					return Path.Combine(Environment.GetEnvironmentVariable("userprofile"), ".grocy-desktop");
				}
				else
				{
					if (File.Exists(PortableFilePath))
					{
						try
						{
							string PortableSettingsPath = File.ReadLines(PortableFilePath).First();
							if (PortableSettingsPath != null && IsValidPath(PortableSettingsPath))
							{
								return PortableSettingsPath;
							}
							else
								return Path.Combine(BaseExecutingPath, "grocy-desktop");
						}
						catch (Exception ex)
						{
							return Path.Combine(BaseExecutingPath, "grocy-desktop");
						}
					}

					return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "grocy-desktop");
				}
			}
		}
		internal static readonly string RuntimeDependenciesBasePath = Path.Combine(BaseFixedUserDataFolderPath, "runtime-dependencies");
		internal static readonly string RuntimeDependenciesExecutingPath = Path.Combine(RuntimeDependenciesBasePath, RunningVersion);

		[STAThread]
		private static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			DpiAwarenessHelper.SetupDpiAwareness();

			// For CefSharp.BrowserSubprocess
			AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

			Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
			Application.ThreadException += Application_ThreadException;
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

			Application.Run(new FrmMain());
		}

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			new FrmDisplayException((Exception)e.ExceptionObject).Show();
		}

		private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
		{
			new FrmDisplayException(e.Exception).Show();
		}

		// For CefSharp.BrowserSubprocess
		private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
		{
			if (args.Name.StartsWith("CefSharp"))
			{
				string assemblyName = args.Name.Split(new[] { ',' }, 2)[0] + ".dll";
				string potentialAssemblyPath = Path.Combine(GrocyDesktopDependencyManager.CefExecutingPath, assemblyName);

				return File.Exists(potentialAssemblyPath) ? Assembly.LoadFile(potentialAssemblyPath) : null;
			}

			return null;
		}
	}
}
