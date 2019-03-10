using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace BiliLiveHelper
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        MainWindow mainWindow;
        public App()
        {
            this.DispatcherUnhandledException += new DispatcherUnhandledExceptionEventHandler(Application_DispatcherUnhandledException);
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            MessageBox.Show("An unexpected and unrecoverable problem has occourred. \r\nThe software will now exit.\r\n\r\n" + string.Format("Captured an unhandled exception：\r\n{0}\r\n\r\nException Message：\r\n{1}\r\n\r\nException StackTrace：\r\n{2}", ex.GetType(), ex.Message, ex.StackTrace), "The software will now exit.", MessageBoxButton.OK, MessageBoxImage.Error);
            mainWindow.StopProformanceMonitor();
            SaveLog(mainWindow.Log);
            //Environment.Exit(0);
        }

        private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Exception ex = e.Exception;
            MessageBox.Show("An unexpected problem has occourred. \r\nSome operation has been terminated.\r\n\r\n" + string.Format("Captured an unhandled exception：\r\n{0}\r\n\r\nException Message：\r\n{1}\r\n\r\nException StackTrace：\r\n{2}", ex.GetType(), ex.Message, ex.StackTrace), "Some operation has been terminated.", MessageBoxButton.OK, MessageBoxImage.Warning);
            e.Handled = true;
        }

        private void SaveLog(string log)
        {
            string folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\BiliLiveHelper\\Logs\\";
            Directory.CreateDirectory(folder);
            string time = DateTime.Now.ToString().Replace(':', '-').Replace('/', '-');

            int i = 0;
            while (i < 100)
            {
                string filename = time;
                if (i != 0)
                {
                    filename = filename + " (" + i + ")";
                }
                if (!File.Exists(folder + filename + ".log"))
                {
                    try
                    {
                        StreamWriter streamWriter = new StreamWriter(folder + filename + ".log", false);
                        streamWriter.Write(log);
                        streamWriter.Close();
                        break;
                    }
                    catch { }
                }
                i++;
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            LoadLang();
            mainWindow = new MainWindow();
            mainWindow.Show();
            mainWindow.Closing += delegate
            {
                SaveLog(mainWindow.Log);
            };
        }

        private void LoadLang()
        {
            List<ResourceDictionary> dictionaryList = new List<ResourceDictionary>();
            foreach (ResourceDictionary dictionary in Application.Current.Resources.MergedDictionaries)
            {
                dictionaryList.Add(dictionary);
            }
            string requestedCulture = string.Format(@"Lang\{0}.xaml", System.Globalization.CultureInfo.CurrentCulture);
            ResourceDictionary resourceDictionary = dictionaryList.FirstOrDefault(d => d.Source.OriginalString.Equals(requestedCulture));
            if (resourceDictionary == null)
            {
                requestedCulture = @"Lang\zh-CN.xaml";
                resourceDictionary = dictionaryList.FirstOrDefault(d => d.Source.OriginalString.Equals(requestedCulture));
            }
            if (resourceDictionary != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(resourceDictionary);
                Application.Current.Resources.MergedDictionaries.Add(resourceDictionary);
            }
        }
    }
}
