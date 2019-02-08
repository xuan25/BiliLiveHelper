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
using System.Windows.Interop;
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
        private BiliLiveInfo biliLiveInfo;
        private bool IsConnected;
        private List<BiliLiveJsonParser.Item> RecievedItems;
        private ProformanceMonitor proformanceMonitor;
        private int Timeout = 10000;
        private int RetryInterval = 3000;
        private uint ListCapacity = 1000;
        private uint HistoryCapacity = 1000;

        public string Log;

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

        [Serializable]
        private class Config
        {
            public double Left;
            public double Top;
            public double Width;
            public double Height;

            public uint ListCapacity;
            public uint HistoryCapacity;
            public int Timeout;
            public int RetryInterval;

            public Config(double left, double top, double width, double height, uint listCapacity, uint historyCapacity, int timeout, int retryInterval)
            {
                Left = left;
                Top = top;
                Width = width;
                Height = height;

                ListCapacity = listCapacity;
                HistoryCapacity = historyCapacity;
                Timeout = timeout;
                RetryInterval = retryInterval;
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            IsConnected = false;
            RecievedItems = new List<BiliLiveJsonParser.Item>();
            Log = string.Empty;
            rhythmStormCount = 0;

            LoadConfig();
        }

        // About startup

        private void Main_Loaded(object sender, RoutedEventArgs e)
        {
            DanmakuBox.Items.Clear();
            GiftBox.Items.Clear();

            ConnectBtn.Content = "载入中...";
            ConnectBtn.IsEnabled = false;
            RoomIdBox.IsEnabled = false;

            ((Storyboard)Resources["ShowWindow"]).Completed += delegate
            {
                new Thread(delegate ()
                {
                    LoadStatus();

                    Dispatcher.Invoke(new Action(() =>
                    {
                        RoomIdBox.Focus();
                        RoomIdBox.Select(RoomIdBox.Text.Length, 0);
                    }));

                    proformanceMonitor = new ProformanceMonitor();
                    proformanceMonitor.CpuProformanceRecieved += ProformanceMonitor_CpuProformanceRecieved;
                    proformanceMonitor.GpuProformanceRecieved += ProformanceMonitor_GpuProformanceRecieved;
                    bool[] availability = proformanceMonitor.StartMonitoring();
                    Dispatcher.Invoke(new Action(() =>
                    {
                        if (!availability[0])
                            CpuUsage.Visibility = Visibility.Hidden;
                        if (!availability[1])
                            GpuUsage.Visibility = Visibility.Hidden;
                    }));
                    
                }).Start();
            };
            ((Storyboard)Resources["ShowWindow"]).Begin();
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

        // About resize

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            HwndSource hwndSource = (HwndSource)PresentationSource.FromVisual(this);
            if (hwndSource != null)
            {
                hwndSource.AddHook(new HwndSourceHook(this.WndProc));
            }
        }

        private const int WM_NCHITTEST = 0x0084;
        public enum HitTest : int
        {
            HTERROR = -2,
            HTTRANSPARENT = -1,
            HTNOWHERE = 0,
            HTCLIENT = 1,
            HTCAPTION = 2,
            HTSYSMENU = 3,
            HTGROWBOX = 4,
            HTSIZE = HTGROWBOX,
            HTMENU = 5,
            HTHSCROLL = 6,
            HTVSCROLL = 7,
            HTMINBUTTON = 8,
            HTMAXBUTTON = 9,
            HTLEFT = 10,
            HTRIGHT = 11,
            HTTOP = 12,
            HTTOPLEFT = 13,
            HTTOPRIGHT = 14,
            HTBOTTOM = 15,
            HTBOTTOMLEFT = 16,
            HTBOTTOMRIGHT = 17,
            HTBORDER = 18,
            HTREDUCE = HTMINBUTTON,
            HTZOOM = HTMAXBUTTON,
            HTSIZEFIRST = HTLEFT,
            HTSIZELAST = HTBOTTOMRIGHT,
            HTOBJECT = 19,
            HTCLOSE = 20,
            HTHELP = 21,
        }
        private readonly int borderThickness = 16;
        private readonly int borderOffset = 8;
        private Point mousePoint = new Point();
        protected IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_NCHITTEST:
                    this.mousePoint.X = (lParam.ToInt32() & 0xFFFF);
                    this.mousePoint.Y = (lParam.ToInt32() >> 16);

                    // Empty
                    if(this.mousePoint.Y - this.Top <= this.borderOffset && this.mousePoint.X - this.Left <= this.borderOffset
                        || this.ActualHeight + this.Top - this.mousePoint.Y <= this.borderOffset && this.mousePoint.X - this.Left <= this.borderOffset
                        || this.mousePoint.Y - this.Top <= this.borderOffset && this.ActualWidth + this.Left - this.mousePoint.X <= this.borderOffset
                        || this.ActualWidth + this.Left - this.mousePoint.X <= this.borderOffset && this.ActualHeight + this.Top - this.mousePoint.Y <= this.borderOffset
                        || this.mousePoint.X - this.Left <= this.borderOffset
                        || this.ActualWidth + this.Left - this.mousePoint.X <= this.borderOffset
                        || this.mousePoint.Y - this.Top <= this.borderOffset
                        || this.ActualHeight + this.Top - this.mousePoint.Y <= this.borderOffset)
                    {
                        handled = true;
                        return new IntPtr((int)HitTest.HTTRANSPARENT);
                    }
                    // TopLeft
                    if (this.mousePoint.Y - this.Top <= this.borderThickness && this.mousePoint.X - this.Left <= this.borderThickness)
                    {
                        handled = true;
                        return new IntPtr((int)HitTest.HTTOPLEFT);
                    }
                    // BottomLeft    
                    else if (this.ActualHeight + this.Top - this.mousePoint.Y <= this.borderThickness && this.mousePoint.X - this.Left <= this.borderThickness)
                    {
                        handled = true;
                        return new IntPtr((int)HitTest.HTBOTTOMLEFT);
                    }
                    // TopRight
                    else if (this.mousePoint.Y - this.Top <= this.borderThickness && this.ActualWidth + this.Left - this.mousePoint.X <= this.borderThickness)
                    {
                        handled = true;
                        return new IntPtr((int)HitTest.HTTOPRIGHT);
                    }
                    // BottomRight
                    else if (this.ActualWidth + this.Left - this.mousePoint.X <= this.borderThickness && this.ActualHeight + this.Top - this.mousePoint.Y <= this.borderThickness)
                    {
                        handled = true;
                        return new IntPtr((int)HitTest.HTBOTTOMRIGHT);
                    }
                    // Left
                    else if (this.mousePoint.X - this.Left <= this.borderThickness)
                    {
                        handled = true;
                        return new IntPtr((int)HitTest.HTLEFT);
                    }
                    // Right
                    else if (this.ActualWidth + this.Left - this.mousePoint.X <= this.borderThickness)
                    {
                        handled = true;
                        return new IntPtr((int)HitTest.HTRIGHT);
                    }
                    // Top
                    else if (this.mousePoint.Y - this.Top <= this.borderThickness)
                    {
                        handled = true;
                        return new IntPtr((int)HitTest.HTTOP);
                    }
                    // Bottom
                    else if (this.ActualHeight + this.Top - this.mousePoint.Y <= this.borderThickness)
                    {
                        handled = true;
                        return new IntPtr((int)HitTest.HTBOTTOM);
                    }
                    else // Inside
                    {
                        handled = true;
                        return new IntPtr((int)HitTest.HTCLIENT);
                    }
            }
            return IntPtr.Zero;
        }

        // About button

        private void ConnectBtn_Click(object sender, RoutedEventArgs e)
        {
            if (IsConnected)
                Disconnect();
            else
                Connect();
            Status status = new Status(RoomIdBox.Text, IsConnected, RecievedItems.ToArray());
            SaveStatus(status);
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

            biliLiveListener = new BiliLiveListener(uint.Parse(RoomIdBox.Text), Timeout);
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
            if(biliLiveInfo != null)
                biliLiveInfo.StopInfoListener();
        }

        private void BiliLiveListener_Connected()
        {
            IsConnected = true;
            uint roomId = 0;
            Dispatcher.Invoke(new Action(() =>
            {
                ConnectBtn.IsEnabled = true;
                ConnectBtn.Content = "断开";
                RoomIdBox.Visibility = Visibility.Hidden;
                InfoGrid.Visibility = Visibility.Visible;
                TitleBox.Text = "弹幕姬 - " + RoomIdBox.Text;

                AppendMessage("已连接", (Color)ColorConverter.ConvertFromString("#FF19E62C"));

                roomId = uint.Parse(RoomIdBox.Text);
            }));

            
            biliLiveInfo = new BiliLiveInfo(roomId);
            BiliLiveInfo.Info info = null;
            while (info == null)
                info = biliLiveInfo.GetInfo(Timeout);
            BiliLiveInfo_InfoUpdate(info);
            biliLiveInfo.InfoUpdate += BiliLiveInfo_InfoUpdate;
            biliLiveInfo.StartInfoListener(Timeout, Timeout);
        }

        private void BiliLiveListener_Disconnected()
        {
            IsConnected = false;
            Dispatcher.Invoke(new Action(() =>
            {
                ConnectBtn.IsEnabled = true;
                ConnectBtn.Content = "连接";
                PopularityBox.Text = "0";
                AreaBox.Text = "正在获取分区...";
                InfoGrid.ToolTip = null;
                RoomIdBox.Visibility = Visibility.Visible;
                InfoGrid.Visibility = Visibility.Hidden;
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
                    if (Timeout > 0)
                        pingReply = new Ping().Send("live.bilibili.com", Timeout);
                    else
                        pingReply = new Ping().Send("live.bilibili.com");
                }
                catch (Exception)
                {

                }
                if(pingReply != null && pingReply.Status == IPStatus.Success)
                {
                    Thread.Sleep(RetryInterval);
                    Dispatcher.Invoke(new Action(() =>
                    {
                        AppendMessage("尝试重连", (Color)ColorConverter.ConvertFromString("#FFE61919"));
                        biliLiveListener.Disconnect();
                        biliLiveListener = new BiliLiveListener(uint.Parse(RoomIdBox.Text), Timeout);
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
                    Thread.Sleep(RetryInterval);
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
                    InfoGrid.Visibility = Visibility.Hidden;
                    TitleBox.Text = "弹幕姬";
                }));
            }
        }

        // Info recieved

        private void BiliLiveInfo_InfoUpdate(BiliLiveInfo.Info info)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                if (info.LiveStatus == 1)
                    TitleBox.Text = "直播中 - " + info.Title;
                else
                    TitleBox.Text = "准备中 - " + info.Title;
                AreaBox.Text = string.Format("{0} · {1}", info.ParentAreaName, info.AreaName);
                InfoGrid.ToolTip = Regex.Replace(Regex.Replace(Regex.Unescape(info.Description.Replace("&nbsp;", " ")), @"<[^>]+>|</[^>]+>", string.Empty), @"(\r?\n)+", "\r\n").Trim();
            }));

        }

        // Listener recieved

        private void BiliLiveListener_JsonRecieved(string message)
        {
            Log += message + "\r\n";
            if (debugWindow != null)
            {
                debugWindow.AppendLog(message);
            }
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
                Dispatcher.Invoke(new Action(() =>
                {
                    if (item.Type == BiliLiveJsonParser.Item.Types.DANMU_MSG || item.Type == BiliLiveJsonParser.Item.Types.WELCOME || item.Type == BiliLiveJsonParser.Item.Types.WELCOME_GUARD || item.Type == BiliLiveJsonParser.Item.Types.ROOM_BLOCK_MSG)
                        while (DanmakuBox.Items.Count >= ListCapacity)
                        {
                            RemoveFirstItem(DanmakuBox);
                        }
                    else
                        while (GiftBox.Items.Count >= ListCapacity)
                        {
                            RemoveFirstItem(GiftBox);
                        }
                }));

                if (!(item.Type == BiliLiveJsonParser.Item.Types.DANMU_MSG && ((BiliLiveJsonParser.Danmaku)item.Content).Type != 0))
                {
                    RecievedItems.Add(item);
                    while (RecievedItems.Count > HistoryCapacity)
                        RecievedItems.RemoveAt(0);
                }
                    
                switch (item.Type)
                {
                    case BiliLiveJsonParser.Item.Types.DANMU_MSG:
                        AppendDanmaku((BiliLiveJsonParser.Danmaku)item.Content);
                        break;
                    case BiliLiveJsonParser.Item.Types.SEND_GIFT:
                        AppendGift((BiliLiveJsonParser.Gift)item.Content);
                        break;
                    case BiliLiveJsonParser.Item.Types.COMBO_END:
                        AppendGiftCombo((BiliLiveJsonParser.GiftCombo)item.Content);
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
                    case BiliLiveJsonParser.Item.Types.GUARD_BUY:
                        AppendGuardBuy((BiliLiveJsonParser.GuardBuy)item.Content);
                        break;
                }
                
            }
        }

        private void RemoveFirstItem(ListBox listBox)
        {
            if (listBox.IsMouseOver)
            {
                Decorator decorator = (Decorator)VisualTreeHelper.GetChild(DanmakuBox, 0);
                ScrollViewer scrollViewer = (ScrollViewer)decorator.Child;
                scrollViewer.ScrollToVerticalOffset(-((ListBoxItem)listBox.Items[0]).ActualHeight);
            }
            listBox.Items.RemoveAt(0);
        }

        private void AppendDanmaku(BiliLiveJsonParser.Danmaku danmaku)
        {
            if(danmaku.Type != 0)
            {
                AppendRhythmStorm(danmaku);
                return;
            }
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

                ListBoxItem listBoxItem = new ListBoxItem() { Content = textBlock, HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center };
                listBoxItem.MouseRightButtonUp += ListBoxItem_MouseRightButtonUp;
                listBoxItem.MouseLeftButtonUp += ListBoxItem_MouseLeftButtonUp;
                listBoxItem.MouseLeave += ListBoxItem_MouseLeave;
                listBoxItem.Loaded += ListBoxItem_Loaded;
                DanmakuBox.Items.Add(listBoxItem);
                RefreshScroll(DanmakuBox);
            }));
        }

        private uint rhythmStormCount;
        private Thread rhythmStormThread;
        private DateTime lastRhythmTime;
        private void AppendRhythmStorm(BiliLiveJsonParser.Danmaku danmaku)
        {
            rhythmStormCount++;
            Dispatcher.Invoke(new Action(() =>
            {
                RhythmStormTextBox.Text = danmaku.Content;
                RhythmStormCountBox.Text = " x" + rhythmStormCount;
                RhythmStormBox.Visibility = Visibility.Visible;
                ((Storyboard)Resources["ShowRhythmStorm"]).Begin();
            }));
            lastRhythmTime = DateTime.Now;
            if (rhythmStormThread == null)
            {
                rhythmStormThread = new Thread(delegate ()
                {
                    while (true)
                    {
                        Thread.Sleep(1000);
                        if (DateTime.Now > lastRhythmTime.AddSeconds(10))
                        {
                            rhythmStormCount = 0;
                            rhythmStormThread = null;
                            break;
                        }
                    }
                });
                rhythmStormThread.Start();
            }
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

                ListBoxItem listBoxItem = new ListBoxItem() { Content = textBlock, HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center };
                listBoxItem.MouseRightButtonUp += ListBoxItem_MouseRightButtonUp;
                listBoxItem.MouseLeftButtonUp += ListBoxItem_MouseLeftButtonUp;
                listBoxItem.MouseLeave += ListBoxItem_MouseLeave;
                listBoxItem.Loaded += ListBoxItem_Loaded;
                GiftBox.Items.Add(listBoxItem);
                RefreshScroll(GiftBox);
            }));
        }

        private void AppendGiftCombo(BiliLiveJsonParser.GiftCombo giftCombo)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                TextBlock textBlock = new TextBlock() { TextWrapping = TextWrapping.Wrap };

                Run user = new Run()
                {
                    Text = giftCombo.Sender.Name,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFC8C83C")),
                    Tag = giftCombo.Sender.Id
                };
                //user.MouseLeftButtonDown += User_MouseLeftButtonDown;
                textBlock.Inlines.Add(user);

                textBlock.Inlines.Add(new Run() { Text = " 赠送 ", Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCBDAF7")) });
                textBlock.Inlines.Add(new Run() { Text = giftCombo.GiftName, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFA82BE")) });
                textBlock.Inlines.Add(new Run() { Text = " 连击", Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDC8C32")) });
                textBlock.Inlines.Add(new Run() { Text = " x" + giftCombo.Number, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF64D2F0")) });

                ListBoxItem listBoxItem = new ListBoxItem() { Content = textBlock, HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center };
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

                ListBoxItem listBoxItem = new ListBoxItem() { Content = textBlock, HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center };
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

                ListBoxItem listBoxItem = new ListBoxItem() { Content = textBlock, HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center };
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

                ListBoxItem listBoxItem = new ListBoxItem() { Content = textBlock, HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center };
                listBoxItem.MouseRightButtonUp += ListBoxItem_MouseRightButtonUp;
                listBoxItem.MouseLeftButtonUp += ListBoxItem_MouseLeftButtonUp;
                listBoxItem.MouseLeave += ListBoxItem_MouseLeave;
                listBoxItem.Loaded += ListBoxItem_Loaded;
                DanmakuBox.Items.Add(listBoxItem);
                RefreshScroll(DanmakuBox);
            }));
        }

        private void AppendGuardBuy(BiliLiveJsonParser.GuardBuy guardBuy)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                TextBlock textBlock = new TextBlock() { TextWrapping = TextWrapping.Wrap };

                Run user = new Run()
                {
                    Text = guardBuy.User.Name,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF64D2F0")),
                    Tag = guardBuy.User.Id
                };
                user.MouseLeftButtonDown += User_MouseLeftButtonDown;
                textBlock.Inlines.Add(user);

                textBlock.Inlines.Add(new Run() { Text = " 开通了 ", Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCBDAF7")) });
                textBlock.Inlines.Add(new Run() { Text = guardBuy.GiftName, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFA82BE")) });

                ListBoxItem listBoxItem = new ListBoxItem() { Content = textBlock, HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center };
                listBoxItem.MouseRightButtonUp += ListBoxItem_MouseRightButtonUp;
                listBoxItem.MouseLeftButtonUp += ListBoxItem_MouseLeftButtonUp;
                listBoxItem.MouseLeave += ListBoxItem_MouseLeave;
                listBoxItem.Loaded += ListBoxItem_Loaded;
                GiftBox.Items.Add(listBoxItem);
                RefreshScroll(GiftBox);
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

                ListBoxItem listBoxItem = new ListBoxItem() { Content = textBlock, HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center };
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

        private enum TitleFlag{ DRAGMOVE, CLOSE, SETTING }
        private TitleFlag titleflag;

        private void Header_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (CloseBtn.IsMouseOver)
            {
                titleflag = TitleFlag.CLOSE;
            }
            else if (SettingBtn.IsMouseOver)
            {
                titleflag = TitleFlag.SETTING;
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
                titleflag = TitleFlag.DRAGMOVE;
            }
        }

        private void Header_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (CloseBtn.IsMouseOver == true && titleflag == TitleFlag.CLOSE)
            {
                this.Close();
            }
            else if (SettingBtn.IsMouseOver == true && titleflag == TitleFlag.SETTING)
            {
                SwitchSettingPanel();
            }
        }

        // About settings

        private void SwitchSettingPanel()
        {
            if (!ListGrid.IsHitTestVisible)
            {
                HideSetting();
            }
            else
            {
                ListCapacitySettingBox.Text = ListCapacity.ToString();
                HistoryCapacitySettingBox.Text = HistoryCapacity.ToString();
                TimeoutSettingBox.Text = (Timeout / 1000).ToString();
                RetryIntervalSettingBox.Text = (RetryInterval / 1000).ToString();
                ShowSetting();
            }
        }

        private void ClearDanmakuBtn_Click(object sender, RoutedEventArgs e)
        {
            DanmakuBox.Items.Clear();
        }

        private void ClearGiftBtn_Click(object sender, RoutedEventArgs e)
        {
            GiftBox.Items.Clear();
        }

        private void ConfirmSettingBtn_Click(object sender, RoutedEventArgs e)
        {
            ConfirmSetting();
        }

        private void ConfirmSetting()
        {
            ListCapacity = uint.Parse(ListCapacitySettingBox.Text);
            HistoryCapacity = uint.Parse(HistoryCapacitySettingBox.Text);
            Timeout = int.Parse(TimeoutSettingBox.Text) * 1000;
            RetryInterval = int.Parse(RetryIntervalSettingBox.Text) * 1000;
            HideSetting();
            while (RecievedItems.Count > HistoryCapacity)
                RecievedItems.RemoveAt(0);
            while (DanmakuBox.Items.Count > ListCapacity)
                DanmakuBox.Items.RemoveAt(0);
            while (GiftBox.Items.Count > ListCapacity)
                GiftBox.Items.RemoveAt(0);

            Config config = new Config(this.Left, this.Top, this.Width, this.Height, this.ListCapacity, this.HistoryCapacity, this.Timeout, this.RetryInterval);
            SaveConfig(config);
        }

        private void CancelSettingBtn_Click(object sender, RoutedEventArgs e)
        {
            HideSetting();
        }

        private void ShowSetting()
        {
            ListGrid.IsHitTestVisible = false;
            ((Storyboard)Resources["ShowSetting"]).Begin();
        }

        private void HideSetting()
        {
            ListGrid.IsHitTestVisible = true;
            ((Storyboard)Resources["HideSetting"]).Begin();
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
                    ((TextBox)sender).Select(match.Value.Length, 0);
                }
                e.CancelCommand();
            }
        }

        private void NumberBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
                e.Handled = true;
            else if (sender == RoomIdBox && e.Key == Key.Enter)
            {
                e.Handled = true;
                Connect();
            }
            else if (sender == ListCapacitySettingBox && e.Key == Key.Enter)
            {
                e.Handled = true;
                ConfirmSetting();
            }
            else if (sender == HistoryCapacitySettingBox && e.Key == Key.Enter)
            {
                e.Handled = true;
                ConfirmSetting();
            }
            else if (sender == TimeoutSettingBox && e.Key == Key.Enter)
            {
                e.Handled = true;
                ConfirmSetting();
            }
            else if (sender == RetryIntervalSettingBox && e.Key == Key.Enter)
            {
                e.Handled = true;
                ConfirmSetting();
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
            Config config = new Config(this.Left, this.Top, this.Width, this.Height, this.ListCapacity, this.HistoryCapacity, this.Timeout, this.RetryInterval);
            Status status = new Status(RoomIdBox.Text, IsConnected, RecievedItems.ToArray());
            if (IsConnected)
                Disconnect();
            ((Storyboard)Resources["HideWindow"]).Completed += delegate
            {
                new Thread(delegate ()
                {
                    Thread.Sleep(0);
                    proformanceMonitor.StopMonitoring();
                    SaveConfig(config);
                    SaveStatus(status);
                    Environment.Exit(0);
                }).Start();
            };
            ((Storyboard)Resources["HideWindow"]).Begin();
            e.Cancel = true;
        }

        public void StopProformanceMonitor()
        {
            if(proformanceMonitor != null)
                proformanceMonitor.StopMonitoring();
        }

        // Save & Load

        private void SaveConfig(Config config)
        {
            string fileDirectory = Path.GetTempPath() + "BiliLiveHelper\\";
            if (!Directory.Exists(fileDirectory))
                Directory.CreateDirectory(fileDirectory);
            string fileName = "Config.dat";
            Stream stream = new FileStream(fileDirectory + fileName, FileMode.Create, FileAccess.ReadWrite);
            BinaryFormatter binaryFormatter = new BinaryFormatter();
            binaryFormatter.Serialize(stream, config);
            stream.Close();
        }

        private bool LoadConfig()
        {
            string path = Path.GetTempPath() + "BiliLiveHelper\\Config.dat";
            if (!File.Exists(path))
            {
                return false;
            }
            try
            {
                Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
                BinaryFormatter binaryFormatter = new BinaryFormatter();
                Config config = (Config)binaryFormatter.Deserialize(stream);
                stream.Close();
                ApplyConfig(config);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void ApplyConfig(Config config)
        {
            this.Top = config.Top;
            this.Left = config.Left;
            this.Height = config.Height;
            this.Width = config.Width;

            this.ListCapacity = config.ListCapacity;
            this.HistoryCapacity = config.HistoryCapacity;
            this.Timeout = config.Timeout;
            this.RetryInterval = config.RetryInterval;
        }

        private void SaveStatus(Status status)
        {
            string fileDirectory = Path.GetTempPath() + "BiliLiveHelper\\";
            if (!Directory.Exists(fileDirectory))
                Directory.CreateDirectory(fileDirectory);
            string fileName = "Status.dat";
            Stream stream = new FileStream(fileDirectory + fileName, FileMode.Create, FileAccess.ReadWrite);
            BinaryFormatter binaryFormatter = new BinaryFormatter();
            binaryFormatter.Serialize(stream, status);
            stream.Close();
        }

        private bool LoadStatus()
        {
            string path = Path.GetTempPath() + "BiliLiveHelper\\Status.dat";
            if (!File.Exists(path))
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    ConnectBtn.Content = "连接";
                    ConnectBtn.IsEnabled = true;
                    RoomIdBox.IsEnabled = true;
                }));
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
                Dispatcher.Invoke(new Action(() =>
                {
                    ConnectBtn.Content = "连接";
                    ConnectBtn.IsEnabled = true;
                    RoomIdBox.IsEnabled = true;
                }));
                return false;
            }
        }

        private void ApplyStatue(Status status)
        {
            //IsConnected = status.IsConnected;
            Dispatcher.Invoke(new Action(() =>
            {
                RoomIdBox.Text = status.RoomId;
            }));
            foreach (BiliLiveJsonParser.Item i in status.Items)
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    AppendItem(i);
                }));
                Thread.Sleep(0);
            }
            Dispatcher.Invoke(new Action(() =>
            {
                if (status.IsConnected)
                    Connect();
                else
                {
                    ConnectBtn.Content = "连接";
                    ConnectBtn.IsEnabled = true;
                    RoomIdBox.IsEnabled = true;
                }
            }));
        }

        // Open page

        private void InfoGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            string url = "http://live.bilibili.com/" + RoomIdBox.Text;
            new Thread(delegate ()
            {
                System.Diagnostics.Process.Start(url);
            }).Start();
        }

        // Debug window

        private DebugWindow debugWindow;
        private void OpenDebugWindow(object sender, CanExecuteRoutedEventArgs e)
        {
            if (debugWindow == null)
            {
                debugWindow = new DebugWindow(Log);
                debugWindow.MessageSent += delegate (string msg) { BiliLiveListener_JsonRecieved(msg); };
                debugWindow.Closing += delegate { debugWindow = null; };
                debugWindow.Show();
            }
        }
    }
}
