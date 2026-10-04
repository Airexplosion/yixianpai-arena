namespace YxArena.Game
{
	public static class RoomGuard
	{
		public static string Reason()
		{
			GameClient client = GameClientUtil.client;
			if (client == null)
			{
				return Loc.T("没有连接对象（GameClient）", "No connection object (GameClient)");
			}
			if (client.isInRoom)
			{
				return Loc.T("你在一个房间 / 对局里，练习场不工作", "You're in a room / match; the arena is disabled");
			}
			if (client.needReconnectToRoom)
			{
				return Loc.T("有待重连的对局，练习场不工作", "A match is pending reconnect; the arena is disabled");
			}
			return null;
		}
	}
}
