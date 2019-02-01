﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace BiliLiveHelper
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private BiliLiveListener biliLiveListener;
        private bool IsConnected;
        private List<BiliLiveJsonParser.Item> RecievedItems;

        [Serializable]
        private class Status
        {
            public string RoomId;
            public bool IsConnected;
            public BiliLiveJsonParser.Item[] Items;

            public Status(string roomId, bool isConnected, BiliLiveJsonParser.Item[] items)
            {
                RoomId = roomId;
                IsConnected = isConnected;
                Items = items;
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            IsConnected = false;
            RecievedItems = new List<BiliLiveJsonParser.Item>();

            DanmakuBox.Items.Clear();
            GiftBox.Items.Clear();
        }

        // About startup

        private void Main_Loaded(object sender, RoutedEventArgs e)
        {
            ((Storyboard)Resources["ShowWindow"]).Begin();
            RoomIdBox.Focus();

            ProformanceMonitor proformanceMonitor = new ProformanceMonitor();
            proformanceMonitor.CpuProformanceRecieved += ProformanceMonitor_CpuProformanceRecieved;
            proformanceMonitor.GpuProformanceRecieved += ProformanceMonitor_GpuProformanceRecieved;
            bool[] availability = proformanceMonitor.StartMonitoring();
            if (!availability[0])
                CpuUsage.Visibility = Visibility.Hidden;
            if (!availability[1])
                GpuUsage.Visibility = Visibility.Hidden;

            LoadConfig();
        }

        private void ProformanceMonitor_CpuProformanceRecieved(uint percentage)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                CpuUsage.Text = string.Format("{0}%", percentage);
            }));
        }

        private void ProformanceMonitor_GpuProformanceRecieved(uint percentage)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                GpuUsage.Text = string.Format("{0}%", percentage);
            }));
        }

        // About button

        private void ConnectBtn_Click(object sender, RoutedEventArgs e)
        {
            if (IsConnected)
                Disconnect();
            else
                Connect();
        }

        // About Connection

        private void Connect()
        {
            if (RoomIdBox.Text.Length == 0)
            {
                AppendMessage("请输入直播间房间号", (Color)ColorConverter.ConvertFromString("#FFE61919"));
                return;
            }
            ConnectBtn.IsEnabled = false;
            ConnectBtn.Content = "正在连接...";
            RoomIdBox.IsEnabled = false;

            biliLiveListener = new BiliLiveListener(uint.Parse(RoomIdBox.Text));
            biliLiveListener.PopularityRecieved += BiliLiveListener_PopularityRecieved;
            biliLiveListener.JsonRecieved += BiliLiveListener_JsonRecieved;
            biliLiveListener.Connected += BiliLiveListener_Connected;
            biliLiveListener.ConnectionFailed += BiliLiveListener_ConnectionFailed;
            biliLiveListener.Connect();
        }

        private void Disconnect()
        {
            biliLiveListener.Disconnected += BiliLiveListener_Disconnected;
            ConnectBtn.IsEnabled = false;
            ConnectBtn.Content = "正在断开...";
            RoomIdBox.IsEnabled = true;
            biliLiveListener.Disconnect();
        }

        private void BiliLiveListener_Connected()
        {
            IsConnected = true;
            Dispatcher.Invoke(new Action(() =>
            {
                ConnectBtn.IsEnabled = true;
                ConnectBtn.Content = "断开";
                RoomIdBox.Visibility = Visibility.Hidden;
                PopularityGrid.Visibility = Visibility.Visible;
                TitleBox.Text = "弹幕姬 - " + RoomIdBox.Text;

                AppendMessage("已连接", (Color)ColorConverter.ConvertFromString("#FF19E62C"));
            }));
        }

        private void BiliLiveListener_Disconnected()
        {
            IsConnected = false;
            Dispatcher.Invoke(new Action(() =>
            {
                ConnectBtn.IsEnabled = true;
                ConnectBtn.Content = "连接";
                PopularityBox.Text = "0";
                RoomIdBox.Visibility = Visibility.Visible;
                PopularityGrid.Visibility = Visibility.Hidden;
                TitleBox.Text = "弹幕姬";

                AppendMessage("已断开", (Color)ColorConverter.ConvertFromString("#FFE61919"));
            }));
        }

        private void BiliLiveListener_ConnectionFailed(string message)
        {
            AppendMessage(message, (Color)ColorConverter.ConvertFromString("#FFE61919"));
            if (IsConnected)
            {
                PingReply pingReply = null;
                try
                {
                    pingReply = new Ping().Send("live.bilibili.com", BiliLiveListener.TIME_OUT);
                }
                catch (Exception)
                {

                }
                if(pingReply != null && pingReply.Status == IPStatus.Success)
                {
                    Thread.Sleep(1000);
                    Dispatcher.Invoke(new Action(() =>
                    {
                        AppendMessage("尝试重连", (Color)ColorConverter.ConvertFromString("#FFE61919"));
                        biliLiveListener.Disconnect();
                        biliLiveListener = new BiliLiveListener(uint.Parse(RoomIdBox.Text));
                        biliLiveListener.PopularityRecieved += BiliLiveListener_PopularityRecieved;
                        biliLiveListener.JsonRecieved += BiliLiveListener_JsonRecieved;
                        biliLiveListener.Connected += BiliLiveListener_Connected;
                        biliLiveListener.ConnectionFailed += BiliLiveListener_ConnectionFailed;
                        biliLiveListener.Connect();
                    }));
                }
                else
                {
                    AppendMessage("网络连接失败", (Color)ColorConverter.ConvertFromString("#FFE61919"));
                    Thread.Sleep(10000);
                    BiliLiveListener_ConnectionFailed("检测网络");
                }
            }
            else
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    ConnectBtn.IsEnabled = true;
                    ConnectBtn.Content = "连接";
                    PopularityBox.Text = "0";
                    RoomIdBox.IsEnabled = true;
                    RoomIdBox.Visibility = Visibility.Visible;
                    PopularityGrid.Visibility = Visibility.Hidden;
                    TitleBox.Text = "弹幕姬";
                }));
            }
        }

        // Listener recieved

        private void BiliLiveListener_JsonRecieved(string message)
        {
            Console.WriteLine("Json: " + message);
            BiliLiveJsonParser.Item item = BiliLiveJsonParser.Parse(message);
            AppendItem(item);
        }

        private void BiliLiveListener_PopularityRecieved(string message)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                PopularityBox.Text = uint.Parse(message).ToString("N0");
            }));
        }

        // Append list item

        private void AppendItem(BiliLiveJsonParser.Item item)
        {
            if (item != null)
            {
                RecievedItems.Add(item);
                switch (item.Type)
                {
                    case BiliLiveJsonParser.Item.Types.DANMU_MSG:
                        AppendDanmaku((BiliLiveJsonParser.Danmaku)item.Content);
                        break;
                    case BiliLiveJsonParser.Item.Types.SEND_GIFT:
                        AppendGift((BiliLiveJsonParser.Gift)item.Content);
                        break;
                    case BiliLiveJsonParser.Item.Types.WELCOME:
                        AppendWelcome((BiliLiveJsonParser.Welcome)item.Content);
                        break;
                    case BiliLiveJsonParser.Item.Types.WELCOME_GUARD:
                        AppendWelcomeGuard((BiliLiveJsonParser.WelcomeGuard)item.Content);
                        break;
                    case BiliLiveJsonParser.Item.Types.ROOM_BLOCK_MSG:
                        AppendRoomBlock((BiliLiveJsonParser.RoomBlock)item.Content);
                        break;
                }
            }
        }

        private void AppendDanmaku(BiliLiveJsonParser.Danmaku danmaku)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                TextBlock textBlock = new TextBlock() { TextWrapping = TextWrapping.Wrap };

                Run user = new Run()
                {
                    Text = danmaku.Sender.Name,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFADBCD9")),
                    Tag = danmaku.Sender.Id
                };
                user.MouseLeftButtonDown += User_MouseLeftButtonDown;
                textBlock.Inlines.Add(user);

                textBlock.Inlines.Add(new Run() { Text = ": ", Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF818181")) });

                Run content = new Run()
                {
                    Text = danmaku.Content,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFFFF")),
                    Tag = danmaku.Sender.Name + ": "
                };
                content.MouseLeftButtonDown += Content_MouseLeftButtonDown;
                textBlock.Inlines.Add(content);

                ListBoxItem listBoxItem = new ListBoxItem() { Content = textBlock };
                listBoxItem.MouseRightButtonUp += ListBoxItem_MouseRightButtonUp;
                listBoxItem.MouseLeftButtonUp += ListBoxItem_MouseLeftButtonUp;
                listBoxItem.MouseLeave += ListBoxItem_MouseLeave;
                listBoxItem.Loaded += ListBoxItem_Loaded;
                DanmakuBox.Items.Add(listBoxItem);
                RefreshScroll(DanmakuBox);
            }));
        }

        private void AppendGift(BiliLiveJsonParser.Gift gift)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                TextBlock textBlock = new TextBlock() { TextWrapping = TextWrapping.Wrap};

                Run user = new Run()
                {
                    Text = gift.Sender.Name,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFC8C83C")),
                    Tag = gift.Sender.Id
                };
                user.MouseLeftButtonDown += User_MouseLeftButtonDown;
                textBlock.Inlines.Add(user);

                textBlock.Inlines.Add(new Run() { Text = " 赠送 ", Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCBDAF7")) });
                textBlock.Inlines.Add(new Run() { Text = gift.GiftName, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFA82BE")) });
                textBlock.Inlines.Add(new Run() { Text = " x" + gift.Number, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF64D2F0")) });

                ListBoxItem listBoxItem = new ListBoxItem() { Content = textBlock };
                listBoxItem.MouseRightButtonUp += ListBoxItem_MouseRightButtonUp;
                listBoxItem.MouseLeftButtonUp += ListBoxItem_MouseLeftButtonUp;
                listBoxItem.MouseLeave += ListBoxItem_MouseLeave;
                listBoxItem.Loaded += ListBoxItem_Loaded;
                GiftBox.Items.Add(listBoxItem);
                RefreshScroll(GiftBox);
            }));
        }

        private void AppendWelcome(BiliLiveJsonParser.Welcome welcome)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                TextBlock textBlock = new TextBlock() { TextWrapping = TextWrapping.Wrap };

                Run user = new Run()
                {
                    Text = welcome.User.Name,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDC8C32")),
                    Tag = welcome.User.Id
                };
                user.MouseLeftButtonDown += User_MouseLeftButtonDown;
                textBlock.Inlines.Add(user);

                textBlock.Inlines.Add(new Run() { Text = " 进入直播间", Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCBDAF7")) });

                ListBoxItem listBoxItem = new ListBoxItem() { Content = textBlock };
                listBoxItem.MouseRightButtonUp += ListBoxItem_MouseRightButtonUp;
                listBoxItem.MouseLeftButtonUp += ListBoxItem_MouseLeftButtonUp;
                listBoxItem.MouseLeave += ListBoxItem_MouseLeave;
                listBoxItem.Loaded += ListBoxItem_Loaded;
                DanmakuBox.Items.Add(listBoxItem);
                RefreshScroll(DanmakuBox);
            }));
        }

        private void AppendWelcomeGuard(BiliLiveJsonParser.WelcomeGuard welcomeGuard)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                TextBlock textBlock = new TextBlock() { TextWrapping = TextWrapping.Wrap };

                Run user = new Run()
                {
                    Text = welcomeGuard.User.Name,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF64D2F0")),
                    Tag = welcomeGuard.User.Id
                };
                user.MouseLeftButtonDown += User_MouseLeftButtonDown;
                textBlock.Inlines.Add(user);

                textBlock.Inlines.Add(new Run() { Text = " 进入直播间", Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCBDAF7")) });

                ListBoxItem listBoxItem = new ListBoxItem() { Content = textBlock };
                listBoxItem.MouseRightButtonUp += ListBoxItem_MouseRightButtonUp;
                listBoxItem.MouseLeftButtonUp += ListBoxItem_MouseLeftButtonUp;
                listBoxItem.MouseLeave += ListBoxItem_MouseLeave;
                listBoxItem.Loaded += ListBoxItem_Loaded;
                DanmakuBox.Items.Add(listBoxItem);
                RefreshScroll(DanmakuBox);
            }));
        }

        private void AppendRoomBlock(BiliLiveJsonParser.RoomBlock roomBlock)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                TextBlock textBlock = new TextBlock() { TextWrapping = TextWrapping.Wrap };

                Run user = new Run()
                {
                    Text = roomBlock.User.Name,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDC4646")),
                    Tag = roomBlock.User.Id
                };
                user.MouseLeftButtonDown += User_MouseLeftButtonDown;
                textBlock.Inlines.Add(user);

                textBlock.Inlines.Add(new Run() { Text = " 已被禁言", Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDC4646")) });

                ListBoxItem listBoxItem = new ListBoxItem() { Content = textBlock };
                listBoxItem.MouseRightButtonUp += ListBoxItem_MouseRightButtonUp;
                listBoxItem.MouseLeftButtonUp += ListBoxItem_MouseLeftButtonUp;
                listBoxItem.MouseLeave += ListBoxItem_MouseLeave;
                listBoxItem.Loaded += ListBoxItem_Loaded;
                DanmakuBox.Items.Add(listBoxItem);
                RefreshScroll(DanmakuBox);
            }));
        }

        private void AppendMessage(string message, Color color)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                TextBlock textBlock = new TextBlock() { TextWrapping = TextWrapping.Wrap };

                Run content = new Run()
                {
                    Text = message,
                    Foreground = new SolidColorBrush(color),
                    Tag = "Error: "
                };
                content.MouseLeftButtonDown += Content_MouseLeftButtonDown;
                textBlock.Inlines.Add(content);

                ListBoxItem listBoxItem = new ListBoxItem() { Content = textBlock };
                listBoxItem.MouseRightButtonUp += ListBoxItem_MouseRightButtonUp;
                listBoxItem.MouseLeftButtonUp += ListBoxItem_MouseLeftButtonUp;
                listBoxItem.MouseLeave += ListBoxItem_MouseLeave;
                listBoxItem.Loaded += ListBoxItem_Loaded;
                DanmakuBox.Items.Add(listBoxItem);
                RefreshScroll(DanmakuBox);
            }));
        }

        // Append item anm

        private void ListBoxItem_Loaded(object sender, RoutedEventArgs e)
        {
            ColorAnimation colorAnimation = new ColorAnimation(Color.FromArgb(100, 51, 153, 255), Color.FromArgb(0, 51, 153, 255), new Duration(TimeSpan.FromSeconds(1)));
            Storyboard.SetTarget(colorAnimation, (ListBoxItem)sender);
            Storyboard.SetTargetProperty(colorAnimation, new PropertyPath("Background.Color"));

            Storyboard storyboard = new Storyboard();
            storyboard.Children.Add(colorAnimation);

            storyboard.Begin();
        }

        // About scroll

        private void RefreshScroll(ListBox listBox)
        {
            if (!listBox.IsMouseOver)
            {
                Decorator decorator = (Decorator)VisualTreeHelper.GetChild(listBox, 0);
                ScrollViewer scrollViewer = (ScrollViewer)decorator.Child;
                scrollViewer.ScrollToEnd();
            }
        }

        // About item mouse event

        private void ListBoxItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ListBoxItem listBoxItem = (ListBoxItem)sender;
            listBoxItem.IsSelected = false;
        }

        private void ListBoxItem_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            ListBoxItem listBoxItem = (ListBoxItem)sender;
            listBoxItem.IsSelected = false;
        }

        private void ListBoxItem_MouseLeave(object sender, MouseEventArgs e)
        {
            ListBoxItem listBoxItem = (ListBoxItem)sender;
            listBoxItem.IsSelected = false;
        }

        // About text click

        private void User_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            object tag = ((Run)sender).Tag;
            new Thread(delegate ()
            {
                System.Diagnostics.Process.Start(string.Format("https://space.bilibili.com/{0}/", tag));
            }).Start();
        }

        private void Content_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Run run = (Run)sender;
            Clipboard.SetText(run.Tag + run.Text);
        }

        // About Header control

        private enum TitleFlag{ DragMove, Close }
        private TitleFlag titleflag;

        private void Header_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (CloseBtn.IsMouseOver)
            {
                titleflag = TitleFlag.Close;
            }
            else
            {
                try
                {
                    this.DragMove();
                }
                catch (InvalidOperationException)
                {

                }
                titleflag = TitleFlag.DragMove;
            }
        }

        private void Header_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (CloseBtn.IsMouseOver == true && titleflag == TitleFlag.Close)
            {
                this.Close();
            }
        }

        // About input number checking

        private void NumberBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(String)))
            {
                String text = (String)e.DataObject.GetData(typeof(String));
                Match match = Regex.Match(text, "[0-9]+");
                if (match.Success)
                {
                    ((TextBox)sender).Text = match.Value;
                }
                e.CancelCommand();
            }
        }

        private void NumberBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
                e.Handled = true;
            else if (e.Key == Key.Enter)
            {
                e.Handled = true;
                Connect();
            }
                
        }

        private void NumberBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!isNumberic(e.Text))
                e.Handled = true;
        }

        public bool isNumberic(string _string)
        {
            if (string.IsNullOrEmpty(_string))
                return false;
            foreach (char c in _string)
            {
                if (!char.IsDigit(c))
                    return false;
            }
            return true;
        }

        // About Hint

        private void RoomIdBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (RoomIdBox.Text.Length == 0)
                HintBox.Visibility = Visibility.Visible;
            else
                HintBox.Visibility = Visibility.Hidden;
        }

        // About Closing

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Status status = new Status(RoomIdBox.Text, IsConnected, RecievedItems.ToArray());
            if (IsConnected)
                Disconnect();
            ((Storyboard)Resources["HideWindow"]).Completed += delegate
            {
                SaveConfig(status);
                Environment.Exit(0);
            };
            ((Storyboard)Resources["HideWindow"]).Begin();
            e.Cancel = true;
        }

        private void SaveConfig(Status status)
        {
            string fileDirectory = System.IO.Path.GetTempPath() + "BiliLiveHelper\\";
            if (!Directory.Exists(fileDirectory))
                Directory.CreateDirectory(fileDirectory);
            string fileName = "Config";
            Stream stream = new FileStream(fileDirectory + fileName + ".dat", FileMode.Create, FileAccess.ReadWrite);
            BinaryFormatter binaryFormatter = new BinaryFormatter();
            binaryFormatter.Serialize(stream, status);
            stream.Close();
        }

        private bool LoadConfig()
        {
            string path = System.IO.Path.GetTempPath() + "BiliLiveHelper\\Config.dat";
            if (!File.Exists(path))
            {
                return false;
            }
            try
            {
                Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
                BinaryFormatter binaryFormatter = new BinaryFormatter();
                Status status = (Status)binaryFormatter.Deserialize(stream);
                stream.Close();
                ApplyStatue(status);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void ApplyStatue(Status status)
        {
            new Thread(delegate ()
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    RoomIdBox.Text = status.RoomId;
                }));
                foreach (BiliLiveJsonParser.Item i in status.Items)
                    Dispatcher.Invoke(new Action(() =>
                    {
                        AppendItem(i);
                    }));
                if (status.IsConnected)
                    Dispatcher.Invoke(new Action(() =>
                    {
                        Connect();
                    }));
            }).Start();
            
        }

        // Clear

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            DanmakuBox.Items.Clear();
            GiftBox.Items.Clear();
            RecievedItems.Clear();
        }
    }
}
