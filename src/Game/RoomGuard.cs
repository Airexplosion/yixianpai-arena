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
            if (client == null) return "没有连接对象（GameClient）";
            if (client.isInRoom) return "你在一个房间 / 对局里，练习场不工作";
            if (client.needReconnectToRoom) return "有待重连的对局，练习场不工作";
            return null;
        }
    }
}
