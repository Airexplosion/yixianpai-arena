using System;
using Yx.ModSdk;
using Yx.Shared;

namespace YxArena.Game
{
    /// <summary>
    /// 备战界面的「准备」按钮 = 开打。原版的 ReadyCountdownButton.Click() 会向服务器发「准备」请求
    /// （练习场里没有房间，游戏自己在本地就拒了，但不该让它走到那一步）；练习场里跳过原逻辑，直接开打。
    /// </summary>
    public sealed class ReadyHook
    {
        readonly ModContext _ctx;
        readonly OfflineHooks _hooks;
        readonly Action _fight;

        public ReadyHook(ModContext ctx, OfflineHooks hooks, Action fight)
        {
            _ctx = ctx;
            _hooks = hooks;
            _fight = fight;
        }

        public void Install()
        {
            // 没挂上的话点「准备」会走到发包那一步，所以也算进「离线保护」那一组里（OfflineHooks 要先 Install）。
            _hooks.Required.Prefix("ReadyCountdownButton", "Click", 0, OnReadyClick);
        }

        bool OnReadyClick(HookContext h)
        {
            if (!ArenaSession.Active) return true;
            h.Skip(null);
            _fight();
            return false;
        }
    }
}
