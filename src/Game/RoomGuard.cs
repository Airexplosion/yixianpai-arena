namespace YxArena.Game
{
    /// <summary>
    /// 房间闸：练习场只在【不在任何服务器房间 / 对局里】时工作。每个会动游戏状态的入口先问它。
    /// </summary>
    public static class RoomGuard
    {
        /// <summary>不在房间里返回 null；否则返回拒绝的原因。</summary>
        public static string Reason()
        {
            GameClient client = GameClientUtil.client;
            if (client == null) return Loc.T("没有连接对象（GameClient）", "No connection object (GameClient)");
            if (client.isInRoom) return Loc.T("你在一个房间 / 对局里，练习场不工作", "You're in a room / match; the arena is disabled");
            if (client.needReconnectToRoom) return Loc.T("有待重连的对局，练习场不工作", "A match is pending reconnect; the arena is disabled");
            return null;
        }
    }
}
