using BiliLiveHelper.Bili;
using BiliLiveHelper.Monitor;
using JsonUtil;
using Native;
using System;
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
using BiliLiveHelper.Config;

namespace BiliLiveHelper
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        //Attributes
        private BiliLiveListener biliLiveListener;
        private BiliLiveInfo biliLiveInfo;
        private PerformanceMonitor proformanceMonitor;

        public string Log;

        public MainWindow()
        {
            InitializeComponent();

            Log = string.Empty;
            rhythmStormCount = 0;
            IsLatestSurvival = false;

            ConfigManager.LoadConfig();
            ApplyConfig(ConfigManager.CurrentConfig);
        }

        // About startup

        private void Main_Loaded(object sender, RoutedEventArgs e)
        {
            IntPtr windowHandle = new WindowInteropHelper(this).Handle;
            WindowLong.SetWindowLong(windowHandle, WindowLong.GWL_STYLE, (WindowLong.GetWindowLong(windowHandle, WindowLong.GWL_STYLE) | WindowLong.WS_CAPTION));
            WindowLong.SetWindowLong(windowHandle, WindowLong.GWL_EXSTYLE, (WindowLong.GetWindowLong(windowHandle, WindowLong.GWL_EXSTYLE) | WindowLong.WS_EX_TOOLWINDOW));


            DanmakuBox.Items.Clear();
            GiftBox.Items.Clear();
            ConnectBtn.Content = Application.Current.Resources["Loading"].ToString();
            ConnectBtn.IsEnabled = false;
            RoomIdBox.IsEnabled = false;

            ((Storyboard)Resources["ShowWindow"]).Completed += delegate
            {
                new Thread(delegate ()
                {
                    ConfigManager.LoadStatus();
                    ApplyStatue(ConfigManager.CurrentStatus);

                    Dispatcher.Invoke(new Action(() =>
                    {
                        RoomIdBox.Focus();
                        RoomIdBox.Select(RoomIdBox.Text.Length, 0);
                    }));

                    proformanceMonitor = new PerformanceMonitor();
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

        protected IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case HitTest.WM_NCHITTEST:
                    handled = true;
                    return HitTest.Hit(lParam, this.Top, this.Left, this.ActualHeight, this.ActualWidth);
            }
            return IntPtr.Zero;
        }

        // About button

        private void ConnectBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ConfigManager.CurrentStatus.IsConnected)
                Disconnect();
            else
                Connect();

            ConfigManager.CurrentStatus.RoomId = RoomIdBox.Text;
            ConfigManager.SaveStatus();
        }

        // About Connection

        private void Connect()
        {
            if (RoomIdBox.Text.Length == 0)
            {
                AppendMessage(Application.Current.Resources["RoomIdHint"].ToString(), (Color)ColorConverter.ConvertFromString("#FFE61919"));
                return;
            }
            ConnectBtn.IsEnabled = false;
            ConnectBtn.Content = Application.Current.Resources["Connecting"].ToString();
            RoomIdBox.IsEnabled = false;

            biliLiveListener = new BiliLiveListener(uint.Parse(RoomIdBox.Text), ConfigManager.CurrentConfig.Timeout);
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
            ConnectBtn.Content = Application.Current.Resources["Disconnecting"].ToString();
            RoomIdBox.IsEnabled = true;
            biliLiveListener.Disconnect();
            if(biliLiveInfo != null)
                biliLiveInfo.StopInfoListener();
        }

        private void BiliLiveListener_Connected()
        {
            ConfigManager.CurrentStatus.IsConnected = true;
            uint roomId = 0;
            Dispatcher.Invoke(new Action(() =>
            {
                ConnectBtn.IsEnabled = true;
                ConnectBtn.Content = Application.Current.Resources["Disconnect"].ToString();
                RoomIdBox.Visibility = Visibility.Hidden;
                InfoGrid.Visibility = Visibility.Visible;
                TitleBox.Text = Application.Current.Resources["BiliLiveHelper"].ToString() + " - " + RoomIdBox.Text;

                //AppendMessage(Application.Current.Resources["Connected"].ToString(), (Color)ColorConverter.ConvertFromString("#FF19E62C"));

                roomId = uint.Parse(RoomIdBox.Text);
            }));

            
            biliLiveInfo = new BiliLiveInfo(roomId);
            BiliLiveInfo.Info info = null;
            while (info == null)
                info = biliLiveInfo.GetInfo(ConfigManager.CurrentConfig.Timeout);
            BiliLiveInfo_InfoUpdate(info);
            biliLiveInfo.InfoUpdate += BiliLiveInfo_InfoUpdate;
            biliLiveInfo.StartInfoListener(ConfigManager.CurrentConfig.Timeout, 30 * 1000);
        }

        private void BiliLiveListener_Disconnected()
        {
            ConfigManager.CurrentStatus.IsConnected = false;
            Dispatcher.Invoke(new Action(() =>
            {
                ConnectBtn.IsEnabled = true;
                ConnectBtn.Content = Application.Current.Resources["Connect"].ToString();
                PopularityBox.Text = "0";
                AreaBox.Text = Application.Current.Resources["LoadingInfo"].ToString();
                InfoGrid.ToolTip = null;
                RoomIdBox.Visibility = Visibility.Visible;
                InfoGrid.Visibility = Visibility.Hidden;
                TitleBox.Text = Application.Current.Resources["BiliLiveHelper"].ToString();

                //AppendMessage(Application.Current.Resources["Disconnected"].ToString(), (Color)ColorConverter.ConvertFromString("#FFE61919"));
            }));
        }

        private void BiliLiveListener_ConnectionFailed(string message)
        {
            AppendMessage(message, (Color)ColorConverter.ConvertFromString("#FFE61919"));
            if (ConfigManager.CurrentStatus.IsConnected)
            {
                PingReply pingReply = null;
                try
                {
                    if (ConfigManager.CurrentConfig.Timeout > 0)
                        pingReply = new Ping().Send("live.bilibili.com", ConfigManager.CurrentConfig.Timeout);
                    else
                        pingReply = new Ping().Send("live.bilibili.com");
                }
                catch (Exception)
                {

                }
                if(pingReply != null && pingReply.Status == IPStatus.Success)
                {
                    Thread.Sleep(ConfigManager.CurrentConfig.RetryInterval);
                    Dispatcher.Invoke(new Action(() =>
                    {
                        AppendMessage(Application.Current.Resources["Retrying"].ToString(), (Color)ColorConverter.ConvertFromString("#FFE61919"));
                        biliLiveListener.Disconnect();
                        biliLiveListener = new BiliLiveListener(uint.Parse(RoomIdBox.Text), ConfigManager.CurrentConfig.Timeout);
                        biliLiveListener.PopularityRecieved += BiliLiveListener_PopularityRecieved;
                        biliLiveListener.JsonRecieved += BiliLiveListener_JsonRecieved;
                        biliLiveListener.Connected += BiliLiveListener_Connected;
                        biliLiveListener.ConnectionFailed += BiliLiveListener_ConnectionFailed;
                        biliLiveListener.Connect();
                    }));
                }
                else
                {
                    AppendMessage(Application.Current.Resources["ConnectionFailed"].ToString(), (Color)ColorConverter.ConvertFromString("#FFE61919"));
                    Thread.Sleep(ConfigManager.CurrentConfig.RetryInterval);
                    BiliLiveListener_ConnectionFailed(Application.Current.Resources["CheckingNetwork"].ToString());
                }
            }
            else
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    ConnectBtn.IsEnabled = true;
                    ConnectBtn.Content = Application.Current.Resources["Connect"].ToString();
                    PopularityBox.Text = "0";
                    RoomIdBox.IsEnabled = true;
                    RoomIdBox.Visibility = Visibility.Visible;
                    InfoGrid.Visibility = Visibility.Hidden;
                    TitleBox.Text = Application.Current.Resources["BiliLiveHelper"].ToString();
                }));
            }
        }

        // Info recieved

        private void BiliLiveInfo_InfoUpdate(BiliLiveInfo.Info info)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                if (info.LiveStatus == 1)
                    TitleBox.Text = Application.Current.Resources["Living"].ToString() + " - " + info.Title;
                else
                    TitleBox.Text = Application.Current.Resources["Preparing"].ToString() + " - " + info.Title;
                AreaBox.Text = string.Format("{0} · {1}", info.ParentAreaName, info.AreaName);
                InfoGrid.ToolTip = Regex.Replace(Regex.Replace(Regex.Unescape(info.Description.Replace("&nbsp;", " ")), @"<[^>]+>|</[^>]+>", string.Empty), @"(\r?\n)+", "\r\n").Trim();
            }));

        }

        // Listener recieved

        private void BiliLiveListener_JsonRecieved(Json.Value json)
        {
            Log += json.ToString() + "\r\n";
            if (debugWindow != null)
            {
                debugWindow.AppendLog(json.ToString());
            }
            BiliLiveJsonParser.Item item = BiliLiveJsonParser.Parse(json);
            AppendItem(item);
            AppendHistory(item);
        }

        private void BiliLiveListener_PopularityRecieved(string message)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                PopularityBox.Text = uint.Parse(message).ToString("N0");
            }));
        }

        // Append list item

        private void AppendHistory(BiliLiveJsonParser.Item item)
        {
            if (!(item.GetType() == typeof(BiliLiveJsonParser.Danmaku) && ((BiliLiveJsonParser.Danmaku)item).Type != 0))
            {
                ConfigManager.CurrentStatus.RecievedItems.Add(item);
                while (ConfigManager.CurrentStatus.RecievedItems.Count > ConfigManager.CurrentConfig.HistoryCapacity)
                    ConfigManager.CurrentStatus.RecievedItems.RemoveAt(0);
            }
        }

        private void AppendItem(BiliLiveJsonParser.Item item)
        {
            if (item != null)
            {
                // If not Rhythm storm
                if (!(item.GetType() == typeof(BiliLiveJsonParser.Danmaku) && ((BiliLiveJsonParser.Danmaku)item).Type != 0))
                {
                    Dispatcher.Invoke(new Action(() =>
                    {
                        if (item.GetType() == typeof(BiliLiveJsonParser.Danmaku) || item.GetType() == typeof(BiliLiveJsonParser.Welcome) || item.GetType() == typeof(BiliLiveJsonParser.WelcomeGuard) || item.GetType() == typeof(BiliLiveJsonParser.RoomBlock))
                            while (DanmakuBox.Items.Count >= ConfigManager.CurrentConfig.ListCapacity)
                            {
                                RemoveFirstItem(DanmakuBox);
                            }
                        else
                            while (GiftBox.Items.Count >= ConfigManager.CurrentConfig.ListCapacity)
                            {
                                RemoveFirstItem(GiftBox);
                            }
                    }));
                }

                switch (item.Cmd)
                {
                    case BiliLiveJsonParser.Item.Cmds.LIVE:
                    case BiliLiveJsonParser.Item.Cmds.PREPARING:
                        biliLiveInfo.UpdateInfo(ConfigManager.CurrentConfig.Timeout);
                        break;
                    case BiliLiveJsonParser.Item.Cmds.DANMU_MSG:
                        AppendDanmaku((BiliLiveJsonParser.Danmaku)item);
                        break;
                    case BiliLiveJsonParser.Item.Cmds.SEND_GIFT:
                        AppendGift((BiliLiveJsonParser.Gift)item);
                        break;
                    case BiliLiveJsonParser.Item.Cmds.COMBO_END:
                        AppendGiftCombo((BiliLiveJsonParser.GiftCombo)item);
                        break;
                    case BiliLiveJsonParser.Item.Cmds.WELCOME:
                        AppendWelcome((BiliLiveJsonParser.Welcome)item);
                        break;
                    case BiliLiveJsonParser.Item.Cmds.WELCOME_GUARD:
                        AppendWelcomeGuard((BiliLiveJsonParser.WelcomeGuard)item);
                        break;
                    case BiliLiveJsonParser.Item.Cmds.ROOM_BLOCK_MSG:
                        AppendRoomBlock((BiliLiveJsonParser.RoomBlock)item);
                        break;
                    case BiliLiveJsonParser.Item.Cmds.GUARD_BUY:
                        AppendGuardBuy((BiliLiveJsonParser.GuardBuy)item);
                        break;
                    case BiliLiveJsonParser.Item.Cmds.UNKNOW:
                        //AppendMessage(item.Json.ToString(), (Color)ColorConverter.ConvertFromString("#FFC8C83C"));
                        break;
                    default:
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
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - ((ListBoxItem)listBox.Items[0]).ActualHeight);
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

                textBlock.Inlines.Add(new Run() { Text = " : ", Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF818181")) });

                Run content = new Run()
                {
                    Text = danmaku.Content,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFFFF")),
                    Tag = danmaku.Sender.Name + " : "
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
                        if (DateTime.Now > lastRhythmTime.AddSeconds(ConfigManager.CurrentConfig.IntegrationTime /1000))
                        {
                            rhythmStormCount = 0;
                            rhythmStormThread = null;
                            break;
                        }
                        Thread.Sleep(1);
                    }
                });
                rhythmStormThread.Start();
            }
        }

        private BiliLiveJsonParser.Gift latestGift;
        private ListBoxItem latestGiftListBoxItem;
        private bool IsLatestSurvival;
        private Thread latestGiftThread;
        private DateTime latestGiftTime;
        private void AppendGift(BiliLiveJsonParser.Gift gift)
        {
            if (IsLatestSurvival && latestGift != null && latestGift.Sender.Id == gift.Sender.Id && latestGift.GiftName == gift.GiftName)
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    TextBlock textBlock = (TextBlock)latestGiftListBoxItem.Content;
                    Run run = (Run)textBlock.Inlines.LastInline;
                    uint number = uint.Parse(run.Text);
                    uint newNumber = number + gift.Number;
                    run.Text = newNumber.ToString();

                    ListBoxItem_Loaded(latestGiftListBoxItem, null);
                }));
            }
            else
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    TextBlock textBlock = new TextBlock() { TextWrapping = TextWrapping.Wrap };

                    Run user = new Run()
                    {
                        Text = gift.Sender.Name,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFC8C83C")),
                        Tag = gift.Sender.Id
                    };
                    user.MouseLeftButtonDown += User_MouseLeftButtonDown;
                    textBlock.Inlines.Add(user);

                    textBlock.Inlines.Add(new Run() { Text = Application.Current.Resources["Sent"].ToString(), Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCBDAF7")) });
                    textBlock.Inlines.Add(new Run() { Text = gift.GiftName, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFA82BE")) });
                    textBlock.Inlines.Add(new Run() { Text = " x", Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF64D2F0")) });
                    textBlock.Inlines.Add(new Run() { Text = gift.Number.ToString(), Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF64D2F0")) });

                    ListBoxItem listBoxItem = new ListBoxItem() { Content = textBlock, HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center };
                    listBoxItem.MouseRightButtonUp += ListBoxItem_MouseRightButtonUp;
                    listBoxItem.MouseLeftButtonUp += ListBoxItem_MouseLeftButtonUp;
                    listBoxItem.MouseLeave += ListBoxItem_MouseLeave;
                    listBoxItem.Loaded += ListBoxItem_Loaded;
                    GiftBox.Items.Add(listBoxItem);
                    RefreshScroll(GiftBox);
                    latestGiftListBoxItem = listBoxItem;
                }));
            }
            latestGift = gift;

            latestGiftTime = DateTime.Now;
            IsLatestSurvival = true;
            if (latestGiftThread == null)
            {
                latestGiftThread = new Thread(delegate ()
                {
                    while (true)
                    {
                        if (DateTime.Now > latestGiftTime.AddSeconds(ConfigManager.CurrentConfig.IntegrationTime / 1000))
                        {
                            IsLatestSurvival = false;
                            latestGiftThread = null;
                            break;
                        }
                        Thread.Sleep(1);
                    }
                });
                latestGiftThread.Start();
            }
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

                textBlock.Inlines.Add(new Run() { Text = Application.Current.Resources["Sent"].ToString(), Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCBDAF7")) });
                textBlock.Inlines.Add(new Run() { Text = giftCombo.GiftName, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFA82BE")) });
                textBlock.Inlines.Add(new Run() { Text = Application.Current.Resources["Combo"].ToString(), Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDC8C32")) });
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

                textBlock.Inlines.Add(new Run() { Text = Application.Current.Resources["JoinedIn"].ToString(), Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCBDAF7")) });

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

                textBlock.Inlines.Add(new Run() { Text = Application.Current.Resources["JoinedIn"].ToString(), Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCBDAF7")) });

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

                textBlock.Inlines.Add(new Run() { Text = Application.Current.Resources["Banned"].ToString(), Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDC4646")) });

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

                textBlock.Inlines.Add(new Run() { Text = Application.Current.Resources["Bought"].ToString(), Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCBDAF7")) });
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
                    Tag = Application.Current.Resources["Error"].ToString()
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
                titleflag = TitleFlag.DRAGMOVE;
                this.ResizeMode = ResizeMode.NoResize;
                try
                {
                    this.DragMove();
                    
                }
                catch (InvalidOperationException)
                {

                }
                this.ResizeMode = ResizeMode.CanResize;
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
                ListCapacitySettingBox.Text = ConfigManager.CurrentConfig.ListCapacity.ToString();
                HistoryCapacitySettingBox.Text = ConfigManager.CurrentConfig.HistoryCapacity.ToString();
                TimeoutSettingBox.Text = (ConfigManager.CurrentConfig.Timeout / 1000).ToString();
                RetryIntervalSettingBox.Text = (ConfigManager.CurrentConfig.RetryInterval / 1000).ToString();
                IntegrationTimeSettingBox.Text = (ConfigManager.CurrentConfig.IntegrationTime / 1000).ToString();
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

        private void ClearHistoryBtn_Click(object sender, RoutedEventArgs e)
        {
            DanmakuBox.Items.Clear();
            GiftBox.Items.Clear();
            ConfigManager.CurrentStatus.RecievedItems.Clear();
        }

        private void ConfirmSettingBtn_Click(object sender, RoutedEventArgs e)
        {
            ConfirmSetting();
        }

        private void ConfirmSetting()
        {
            ConfigManager.CurrentConfig.ListCapacity = uint.Parse(ListCapacitySettingBox.Text);
            ConfigManager.CurrentConfig.HistoryCapacity = uint.Parse(HistoryCapacitySettingBox.Text);
            ConfigManager.CurrentConfig.Timeout = int.Parse(TimeoutSettingBox.Text) * 1000;
            ConfigManager.CurrentConfig.RetryInterval = int.Parse(RetryIntervalSettingBox.Text) * 1000;
            ConfigManager.CurrentConfig.IntegrationTime = int.Parse(IntegrationTimeSettingBox.Text) * 1000;
            HideSetting();
            while (ConfigManager.CurrentStatus.RecievedItems.Count > ConfigManager.CurrentConfig.HistoryCapacity)
                ConfigManager.CurrentStatus.RecievedItems.RemoveAt(0);
            while (DanmakuBox.Items.Count > ConfigManager.CurrentConfig.ListCapacity)
                DanmakuBox.Items.RemoveAt(0);
            while (GiftBox.Items.Count > ConfigManager.CurrentConfig.ListCapacity)
                GiftBox.Items.RemoveAt(0);

            ConfigManager.SaveConfig();
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
            else if (sender == IntegrationTimeSettingBox && e.Key == Key.Enter)
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
            ConfigManager.CurrentConfig.HasPosition = true;
            ConfigManager.CurrentConfig.Left = this.Left;
            ConfigManager.CurrentConfig.Top = this.Top;
            ConfigManager.CurrentConfig.Width = this.Width;
            ConfigManager.CurrentConfig.Height = this.Height;
                
            ((Storyboard)Resources["HideWindow"]).Completed += delegate
            {
                new Thread(delegate ()
                {
                    Thread.Sleep(0);
                    proformanceMonitor.StopMonitoring();
                    ConfigManager.SaveConfig();
                    ConfigManager.SaveStatus();
                    if (ConfigManager.CurrentStatus.IsConnected)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            Disconnect();
                        });
                    }
                    
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

        

        private void ApplyConfig(ConfigManager.Config config)
        {
            if (config.HasPosition)
            {
                this.Top = config.Top;
                this.Left = config.Left;
                this.Height = config.Height;
                this.Width = config.Width;
            }

            //this.ListCapacity = config.ListCapacity;
            //this.HistoryCapacity = config.HistoryCapacity;
            //this.Timeout = config.Timeout;
            //this.RetryInterval = config.RetryInterval;
            //this.IntegrationTime = config.IntegrationTime;
        }

        

        private void ApplyStatue(ConfigManager.Status status)
        {
            //IsConnected = status.IsConnected;
            Dispatcher.Invoke(new Action(() =>
            {
                RoomIdBox.Text = status.RoomId;
            }));
            foreach (BiliLiveJsonParser.Item i in status.RecievedItems)
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
                    ConnectBtn.Content = Application.Current.Resources["Connect"].ToString();
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
