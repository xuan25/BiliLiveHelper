using JsonUtil;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;

namespace BiliLiveHelper.Bili
{
    class BiliPackReader
    {
        public enum PackTypes
        {
            Unknow = -1,
            Popularity = 3,
            Command = 5,
            Heartbeat = 8
        }

        public interface IPack
        {
            PackTypes PackType { get; }
        }

        public class PopularityPack : IPack
        {
            public PackTypes PackType => PackTypes.Popularity;
            public uint Popularity { get; private set; }

            public PopularityPack(byte[] payload)
            {
                Popularity = BitConverter.ToUInt32(payload.Take(4).Reverse().ToArray(), 0);
            }
        }

        public class CommandPack : IPack
        {
            public PackTypes PackType => PackTypes.Command;
            public Json.Value Value { get; private set; }

            public CommandPack(byte[] payload)
            {
                string jstr = Encoding.UTF8.GetString(payload, 0, payload.Length);
                Value = Json.Parser.Parse(jstr);
            }
        }

        public class HeartbeatPack : IPack
        {
            public PackTypes PackType => PackTypes.Heartbeat;

            public HeartbeatPack(byte[] payload)
            {

            }
        }

        private enum DataTypes
        {
            Unknow = -1,
            Plain = 0,
            Bin = 1,
            Gz = 2
        }

        public static IPack[] ReadPack(Stream stream)
        {
            // Pack length (4)
            byte[] packLengthBuffer = new byte[4];
            stream.Read(packLengthBuffer, 0, packLengthBuffer.Length);
            int packLength = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(packLengthBuffer, 0));
            if (packLength < 16)
            {
                stream.Flush();
                // TODO : 包长度过短
                throw new Exception();
                return null;
            }

            // Header length (2)
            byte[] headerLengthBuffer = new byte[2];
            stream.Read(headerLengthBuffer, 0, headerLengthBuffer.Length);
            int headerLength = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(headerLengthBuffer, 0));
            if (headerLength != 16)
            {
                stream.Flush();
                // TODO : 头部长度异常
                throw new Exception();
                return null;
            }

            // Data type (2)
            byte[] dataTypeBuffer = new byte[2];
            stream.Read(dataTypeBuffer, 0, dataTypeBuffer.Length);
            int dataTypeCode = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(dataTypeBuffer, 0));
            DataTypes dataType;
            if(Enum.IsDefined(typeof(DataTypes), dataTypeCode)){
                dataType = (DataTypes)Enum.ToObject(typeof(DataTypes), dataTypeCode);
            }
            else
            {
                dataType = DataTypes.Unknow;
            }  
            

            // Read pack type (4)
            byte[] packTypeBuffer = new byte[4];
            stream.Read(packTypeBuffer, 0, packTypeBuffer.Length);
            int packTypeCode = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(packTypeBuffer, 0));
            PackTypes packType;
            if (Enum.IsDefined(typeof(PackTypes), packTypeCode))
            {
                packType = (PackTypes)Enum.ToObject(typeof(PackTypes), packTypeCode);
            }
            else
            {
                packType = PackTypes.Unknow;
            }

            // Read split (4)
            byte[] splitBuffer = new byte[4];
            stream.Read(splitBuffer, 0, splitBuffer.Length);

            // Read payload
            int payloadLength = packLength - headerLength;
            byte[] payloadBuffer = new byte[payloadLength];
            stream.Read(payloadBuffer, 0, payloadBuffer.Length);

            // Return
            switch (dataType)
            {
                case DataTypes.Plain:
                    switch (packType)
                    {
                        case PackTypes.Command:
                            return new CommandPack[] { new CommandPack(payloadBuffer) };
                        default:
                            // TODO : 未知包类型
                            throw new Exception();
                            return null;
                    }
                case DataTypes.Bin:
                    switch (packType)
                    {
                        case PackTypes.Popularity:
                            return new PopularityPack[] { new PopularityPack(payloadBuffer) };
                        case PackTypes.Heartbeat:
                            return new HeartbeatPack[] { new HeartbeatPack(payloadBuffer) };
                        default:
                            // TODO : 未知包类型
                            throw new Exception();
                            return null;
                    }
                case DataTypes.Gz:
                    MemoryStream memoryStream = new MemoryStream(payloadBuffer);
                    GZipStream gZipStream = new GZipStream(memoryStream, CompressionMode.Decompress);
                    MemoryStream memoryStream1 = new MemoryStream();
                    gZipStream.CopyTo(memoryStream1);
                    memoryStream1.Position = 0;
                    List<IPack> packs = new List<IPack>();
                    while (memoryStream1.Position < memoryStream1.Length - 1)
                    {
                        IPack[] pack = ReadPack(memoryStream1);
                        packs.AddRange(pack);
                    }
                    return packs.ToArray();
                default:
                    // TODO : 未知数据类型
                    throw new Exception();
                    return null;
            }

        }
    }
}
