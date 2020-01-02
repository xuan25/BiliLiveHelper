using BiliLiveHelper.Bili;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows;

namespace BiliLiveHelper.Config
{
    class ConfigManager
    {
        [Serializable]
        public class Status
        {
            public string RoomId;
            public bool IsConnected;
            public List<BiliLiveJsonParser.Item> RecievedItems;

            public Status()
            {
                RoomId = String.Empty;
                IsConnected = false;
                RecievedItems = new List<BiliLiveJsonParser.Item>();
            }
        }

        [Serializable]
        public class Config
        {
            public bool HasPosition;
            public double Left;
            public double Top;
            public double Width;
            public double Height;

            public uint ListCapacity;
            public uint HistoryCapacity;
            public int Timeout;
            public int RetryInterval;
            public int IntegrationTime;

            public Config()
            {
                HasPosition = false;
                Left = 0;
                Top = 0;
                Width = 0;
                Height = 0;
                ListCapacity = 100;
                HistoryCapacity = 100;
                Timeout = 30*1000;
                RetryInterval = 5*1000;
                IntegrationTime = 5*1000;
            }
        }

        public static Status CurrentStatus = new Status();
        public static Config CurrentConfig = new Config();

        public static void SaveStatus()
        {
            string fileDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\BiliLiveHelper\\";
            if (!Directory.Exists(fileDirectory))
                Directory.CreateDirectory(fileDirectory);
            string fileName = "Status.dat";
            Stream stream = new FileStream(fileDirectory + fileName, FileMode.Create, FileAccess.ReadWrite);
            BinaryFormatter binaryFormatter = new BinaryFormatter();
            binaryFormatter.Serialize(stream, CurrentStatus);
            stream.Close();
        }

        public static bool LoadStatus()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\BiliLiveHelper\\Status.dat";
            if (!File.Exists(path))
            {
                return false;
            }
            try
            {
                Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
                BinaryFormatter binaryFormatter = new BinaryFormatter();
                Status status = (Status)binaryFormatter.Deserialize(stream);
                CurrentStatus = status;
                stream.Close();
                return true;
            }
            catch (Exception)
            {
                return false;
            }

            
            //if (!File.Exists(path))
            //{
            //    Dispatcher.Invoke(new Action(() =>
            //    {
            //        ConnectBtn.Content = Application.Current.Resources["Connect"].ToString();
            //        ConnectBtn.IsEnabled = true;
            //        RoomIdBox.IsEnabled = true;
            //    }));
            //    return false;
            //}
            //try
            //{
            //    Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            //    BinaryFormatter binaryFormatter = new BinaryFormatter();
            //    Status status = (Status)binaryFormatter.Deserialize(stream);
            //    stream.Close();
            //    ApplyStatue(status);
            //    return true;
            //}
            //catch (Exception)
            //{
            //    Dispatcher.Invoke(new Action(() =>
            //    {
            //        ConnectBtn.Content = Application.Current.Resources["Connect"].ToString();
            //        ConnectBtn.IsEnabled = true;
            //        RoomIdBox.IsEnabled = true;
            //    }));
            //    return false;
            //}
        }


        public static void SaveConfig()
        {
            string fileDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\BiliLiveHelper\\";
            if (!Directory.Exists(fileDirectory))
                Directory.CreateDirectory(fileDirectory);
            string fileName = "Config.dat";
            Stream stream = new FileStream(fileDirectory + fileName, FileMode.Create, FileAccess.ReadWrite);
            BinaryFormatter binaryFormatter = new BinaryFormatter();
            binaryFormatter.Serialize(stream, CurrentConfig);
            stream.Close();
        }

        public static bool LoadConfig()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\BiliLiveHelper\\Config.dat";
            if (!File.Exists(path))
            {
                return false;
            }
            try
            {
                Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
                BinaryFormatter binaryFormatter = new BinaryFormatter();
                Config config = (Config)binaryFormatter.Deserialize(stream);
                CurrentConfig = config;
                stream.Close();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
