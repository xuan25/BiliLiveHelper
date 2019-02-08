﻿using System;
using System.Text.RegularExpressions;

namespace BiliLiveHelper
{
    class BiliLiveJsonParser
    {
        [Serializable]
        public class Item
        {
            public enum Types { DANMU_MSG, SEND_GIFT, SPECIAL_GIFT, USER_TOAST_MSG, GUARD_MSG, GUARD_BUY, GUARD_LOTTERY_START, WELCOME, WELCOME_GUARD, ENTRY_EFFECT, SYS_MSG, ROOM_BLOCK_MSG, COMBO_SEND, COMBO_END, ROOM_RANK, TV_START, NOTICE_MSG }
            public Types Type;
            public object Content;

            public Item(Types type, object content)
            {
                Type = type;
                Content = content;
            }
        }

        [Serializable]
        public class User
        {
            public uint Id;
            public string Name;

            public User(string id, string name)
            {
                Id = uint.Parse(id);
                Name = name;
            }
        }

        [Serializable]
        public class Danmaku
        {
            public User Sender;
            public string Content;
            public uint Type;

            public Danmaku(User sender, string content, string type)
            {
                Sender = sender;
                Content = content;
                Type = uint.Parse(type);
            }
        }

        [Serializable]
        public class GiftCombo
        {
            public User Sender;
            public string GiftName;
            public uint Number;

            public GiftCombo(User sender, string giftName, string number)
            {
                Sender = sender;
                GiftName = giftName;
                Number = uint.Parse(number);
            }
        }

        [Serializable]
        public class Gift
        {
            public User Sender;
            public string GiftName;
            public uint Number;

            public Gift(User sender, string giftName, string number)
            {
                Sender = sender;
                GiftName = giftName;
                Number = uint.Parse(number);
            }
        }

        [Serializable]
        public class Welcome
        {
            public User User;

            public Welcome(User user)
            {
                User = user;
            }
        }

        [Serializable]
        public class WelcomeGuard
        {
            public User User;

            public WelcomeGuard(User user)
            {
                User = user;
            }
        }

        [Serializable]
        public class RoomBlock
        {
            public User User;
            public uint Operator;

            public RoomBlock(User user, string ope)
            {
                User = user;
                Operator = uint.Parse(ope);
            }
        }

        [Serializable]
        public class GuardBuy
        {
            public User User;
            public string GiftName;

            public GuardBuy(User user, string giftName)
            {
                User = user;
                GiftName = giftName;
            }
        }

        public static Item Parse(string json)
        {
            Match match = Regex.Match(json, "^{\"cmd\":\"(?<Type>.+?)\",(?<Info>.+)}$");
            if (match.Success)
            {
                Item.Types type;
                try
                {
                    type = (Item.Types)Enum.Parse(typeof(Item.Types), match.Groups["Type"].Value);
                }
                catch (Exception)
                {
                    return null;
                }
                object content = null;
                switch (type)
                {
                    case Item.Types.DANMU_MSG:
                        match = Regex.Match(match.Groups["Info"].Value, "^\"info\":\\[\\[[0-9]+,[0-9]+,[0-9]+,[0-9]+,[0-9]+,-?[0-9]+,[0-9]+,\"[0-9a-z]+\",[0-9]+,(?<Type>[0-9]+),[0-9]+\\],\"(?<Content>.*?)\",\\[(?<Id>[0-9]+),\"(?<Name>.+?)\".*?].*?\\]$");
                        if (match.Success)
                            content = new Danmaku(new User(match.Groups["Id"].Value, Regex.Unescape(match.Groups["Name"].Value)), Regex.Unescape(match.Groups["Content"].Value), match.Groups["Type"].Value);
                        break;
                    case Item.Types.SEND_GIFT:
                        match = Regex.Match(match.Groups["Info"].Value, "^\"data\":{\"giftName\":\"(?<GiftName>.*?)\",\"num\":(?<Number>[0-9]+),\"uname\":\"(?<Username>.*?)\",\"face\":\".*?\",\"guard_level\":[0-9]+,\"rcost\":[0-9]+,\"uid\":(?<UserId>[0-9]+).*}$");
                        if (match.Success)
                            content = new Gift(new User(match.Groups["UserId"].Value, Regex.Unescape(match.Groups["Username"].Value)), Regex.Unescape(match.Groups["GiftName"].Value), match.Groups["Number"].Value);
                        break;
                    case Item.Types.COMBO_END:
                        match = Regex.Match(match.Groups["Info"].Value, "^\"data\":{\"uname\":\"(?<Username>.*?)\",\"r_uname\":\".*?\",\"combo_num\":(?<Number>[0-9]+),\"price\":[0-9]+,\"gift_name\":\"(?<GiftName>.*?)\",\"gift_id\":[0-9]+,\"start_time\":[0-9]+,\"end_time\":[0-9]+,\"guard_level\":[0-9]+}$");
                        if (match.Success)
                            content = new GiftCombo(new User("0", Regex.Unescape(match.Groups["Username"].Value)), Regex.Unescape(match.Groups["GiftName"].Value), match.Groups["Number"].Value);
                        break;
                    case Item.Types.WELCOME:
                        match = Regex.Match(match.Groups["Info"].Value, "^\"data\":{\"uid\":(?<UserId>[0-9]+),\"uname\":\"(?<Username>.*?)\",(\"is_admin\":(true|false)|\"isadmin\":0),\"s?vip\":[0-9]+}(,\"roomid\":[0-9]+)?$");
                        if (match.Success)
                            content = new Welcome(new User(match.Groups["UserId"].Value, Regex.Unescape(match.Groups["Username"].Value)));
                        break;
                    case Item.Types.WELCOME_GUARD:
                        match = Regex.Match(match.Groups["Info"].Value, "^\"data\":{\"uid\":(?<UserId>[0-9]+),\"username\":\"(?<Username>.*?)\",\"guard_level\":[0-9]+(,\"water_god\":[0-9]+)?}(,\"roomid\":[0-9]+)?$");
                        if (match.Success)
                            content = new WelcomeGuard(new User(match.Groups["UserId"].Value, Regex.Unescape(match.Groups["Username"].Value)));
                        break;
                    case Item.Types.ROOM_BLOCK_MSG:
                        match = Regex.Match(match.Groups["Info"].Value, "^\"uid\":\"?[0-9]+\"?,\"uname\":\".*?\",\"data\":{\"uid\":\"?(?<UserId>[0-9]+)\"?,\"uname\":\"(?<Username>.*?)\",\"operator\":(?<Operator>[0-9]+)},\"roomid\":[0-9]+$");
                        if (match.Success)
                            content = new RoomBlock(new User(match.Groups["UserId"].Value, Regex.Unescape(match.Groups["Username"].Value)), match.Groups["Operator"].Value);
                        break;
                    case Item.Types.GUARD_BUY:
                        match = Regex.Match(match.Groups["Info"].Value, "^\"data\":{\"uid\":(?<UserId>[0-9]+),\"username\":\"(?<Username>.*?)\",\"guard_level\":[0-9]+,\"num\":[0-9]+,\"price\":[0-9]+,\"gift_id\":[0-9]+,\"gift_name\":\"(?<GiftName>.*?)\",\"start_time\":[0-9]+,\"end_time\":[0-9]+}$");
                        if (match.Success)
                            content = new GuardBuy(new User(match.Groups["UserId"].Value, Regex.Unescape(match.Groups["Username"].Value)), match.Groups["GiftName"].Value);
                        break;
                }
                return new Item(type, content);
            }
            else
                return null;
        }
    }
}
