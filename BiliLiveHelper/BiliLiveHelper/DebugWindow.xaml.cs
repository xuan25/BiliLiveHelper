using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace BiliLiveHelper
{
    /// <summary>
    /// DebugWindow.xaml 的交互逻辑
    /// </summary>
    public partial class DebugWindow : Window
    {
        public delegate void MessageSentDel(string msg);
        public event MessageSentDel MessageSent;
        Thread sendingThread;

        public DebugWindow(string initLog)
        {
            InitializeComponent();
            LogBox.Text = initLog;
        }

        public void AppendLog(string log)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                if (AutoScrollChk.IsChecked == true)
                    LogBox.ScrollToEnd();
                LogBox.AppendText(log + "\r\n");
            }));
        }

        StringBuilder stringBuilder;
        private void SendBtn_Click(object sender, RoutedEventArgs e)
        {
            SendBtn.IsEnabled = false;
            CancelBtn.IsEnabled = true;
            SendBox.Text += "\r\n";
            SendBox.IsReadOnly = true;
            stringBuilder = new StringBuilder(SendBox.Text);
            int.TryParse(IntervalBox.Text, out int interval);
            if (sendingThread != null)
                sendingThread.Abort();
            sendingThread = new Thread(delegate ()
            {
                while (true)
                {
                    int lineBreakIndex = stringBuilder.ToString().IndexOf('\n');
                    if (lineBreakIndex == -1)
                    {
                        Dispatcher.Invoke(new Action(() =>
                        {
                            SendBtn.IsEnabled = true;
                            CancelBtn.IsEnabled = false;
                            SendBox.IsReadOnly = false;
                            SendBox.Clear();
                        }));
                        break;
                    }
                    string firstLine = stringBuilder.ToString().Substring(0, lineBreakIndex).Trim();
                    stringBuilder.Remove(0, lineBreakIndex + 1);
                    if (firstLine != string.Empty)
                        MessageSent?.Invoke(firstLine);
                    Thread.Sleep(interval);
                }
            });
            sendingThread.Start();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sendingThread != null)
                sendingThread.Abort();
            SendBtn.IsEnabled = true;
            CancelBtn.IsEnabled = false;
            SendBox.IsReadOnly = false;
            SendBox.Text = stringBuilder.ToString();
        }

        private void SendWindow_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
        }

        private void SendWindow_PreviewDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) != null)
            {
                e.Handled = true;
                string filename = ((Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();
                StreamReader streamReader = new StreamReader(filename);
                SendBox.AppendText(streamReader.ReadToEnd());
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (sendingThread != null)
                sendingThread.Abort();
        }
    }
}
