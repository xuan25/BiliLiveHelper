using System;
using System.IO;
using System.Text;

namespace BiliLiveHelper.Bili
{
    class BiliPackWriter
    {
        private static byte[] ToBE(byte[] b)
        {
            if (BitConverter.IsLittleEndian)
                Array.Reverse(b);
            return b;
        }

        public enum MessageType { CONNECT = 7, HEARTBEAT = 2 };

        public static void SendMessage(Stream stream, int messageType, string message)
        {
            byte[] messageArray = Encoding.UTF8.GetBytes(message);
            int dataLength = messageArray.Length + 16;

            MemoryStream buffer = new MemoryStream(dataLength);
            // Data length (4)
            buffer.Write(ToBE(BitConverter.GetBytes(dataLength)), 0, 4);
            // Header length and Data type (4)
            buffer.Write(new byte[] { 0x00, 0x10, 0x00, 0x01 }, 0, 4);
            // Message type (4)
            buffer.Write(ToBE(BitConverter.GetBytes(messageType)), 0, 4);
            // Split (4)
            buffer.Write(ToBE(BitConverter.GetBytes(1)), 0, 4);
            // Message
            buffer.Write(messageArray, 0, messageArray.Length);

            stream.Write(buffer.GetBuffer(), 0, dataLength);
            stream.Flush();
        }
    }
}
