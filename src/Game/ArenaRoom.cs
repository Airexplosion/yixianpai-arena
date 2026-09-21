using System;
using System.Collections.Generic;
using Proto;
using UnityEngine;
using Yx.ModSdk;
using Yx.ModSdk.Hooks;
using Yx.ModSdk.Unity;
using Yx.Shared;

namespace YxArena.Game
{
    /// <summary>
    /// 进场前的「选英雄」界面：直接借用游戏自己的单人房间（SinglePlayerRoomPanel）——大立绘、「更换角色」抽屉
    /// （RoomCharacterSelectionPart，含皮肤）、角色旁的一排仙命（TalentItem）、右下的开始按钮。
    ///
    /// 这个面板必须有一个「模式分页」撑着（它的 currentGameMode 取自正在显示的分页），借的是原初秘境的 RogueModePart：
    ///   RogueModePart.OnShow/0               前置：练习场模式下跳过（原逻辑会同步秘境存档、可能弹「继续存档」），分页本身做成透明不可点
    ///   SinglePlayerRoomPanel.OnGameButtonClick/0  前置：练习场模式下跳过（原逻辑是向服务器开一局秘境），改成进练习场
    ///   TalentItem.OnClick/1                 前置：点的是这个界面里角色旁的仙命 → 打开全仙命表换掉这一格；其余照常（看说明）。
    ///                                        不用游戏自己的仙命选择框：它只列「当前角色专属 / 当前门派 / 通用」，还按境界过滤
    /// 不在练习场模式（玩家正常进原初秘境）时三个处理器第一句就放行。
    ///
    /// 已知的副作用：游戏的角色抽屉会把「上次用的角色」记进本地设置（记在秘境名下），关闭时还原；
    /// 在抽屉里点「确定」会像平时一样把选的皮肤设为该角色的装备皮肤（这是游戏自己的界面自己的行为）。
    /// </summary>
    public sealed class ArenaRoom
    {
        readonly ModContext _ctx;
        readonly ArenaSession _session;
        readonly Action _enter;
        readonly Action<int, RectTransform> _pickTalent;
        SinglePlayerRoomPanel _panel;
        RogueModePart _part;
        bool _open;
        bool _hooked;
        bool _seenShown;
        int _side;
        int _savedLastCharacter;

        /// <param name="pickTalent">点了角色旁第几个仙命图标（0 起）和那个图标（给选择框当定位锚点）：外面打开全仙命选择框。</param>
        public ArenaRoom(ModContext ctx, ArenaSession session, Action enter, Action<int, RectTransform> pickTalent)
        {
            _pickTalent = pickTalent;
            _ctx = ctx;
            _session = session;
            _enter = enter;
        }

        public bool Available { get { return _hooked; } }

        public bool IsOpen { get { return _open; } }

        public int Side { get { return _side; } }

        public void Install()
        {
            HookGroup room = _ctx.Hooks.Group("选英雄界面");
            room.Prefix("RogueModePart", "OnShow", 0, OnRoguePartShow);
            room.Prefix("SinglePlayerRoomPanel", "OnGameButtonClick", 0, OnStartClick);
            _hooked = room.Complete;
            if (!_hooked)
            {
                room.CancelAll();
                _ctx.Log.Warn(_ctx.T("选英雄界面的钩子不全（", "Hero-select hooks incomplete (") + room.Missing + _ctx.T("），练习场直接用上次的角色进场", "); the arena enters with the last-used character"));
                return;
            }
            if (_ctx.Hooks.TryPrefix("TalentItem", "OnClick", 1, OnTalentClick) == null)
                _ctx.Log.Warn(_ctx.T("仙命图标的点击钩子没挂上：选英雄界面上的仙命只能用角色自带的", "Talent icon click hook not installed: talents on the hero-select screen are limited to the character's innate ones"));
        }

        // ── 打开 / 关闭 ────────────────────────────────────────────────────

        public void Open()
        {
            if (!_hooked || _open) return;
            LobbyPanel lobby = ILRPanelBase.FindILRPanel<LobbyPanel>();
            if (lobby == null) return;
            _panel = lobby.FindILRSubPanelRuntime<SinglePlayerRoomPanel>();
            if (_panel == null) { Ui.Toast(_ctx.T("没能打开选英雄界面", "Couldn't open the hero-select screen")); return; }
            _savedLastCharacter = SettingsManager.GetLastCharacterId(GameMode.RogueMode);
            _open = true;          // 先立旗：ShowPart 里就会走到 RogueModePart.OnShow 的钩子
            _seenShown = false;
            _side = 0;
            try
            {
                // 和游戏自己的入口一样：从模式选择窗口进来的，就让它在我们显示期间让位、关掉之后回来。
                UISubPanelBase entrance = lobby.panel != null ? lobby.panel.FindSubPanel("GameEntrancePanel") : null;
                if (entrance != null && entrance.isShow) _panel.SetFromPanel(lobby.FindILRSubPanel<GameEntrancePanel>());
                _panel.ShowPart<RogueModePart>();
                _part = _panel.FindPart<RogueModePart>();
                SetPartVisible(false);
                ShowSide();
            }
            catch (Exception)
            {
                Closed();
                throw;
            }
        }

        /// <summary>借来的秘境分页：透明、不挡点击（它的返回按钮也点不到了，用 ESC 或我们自己的返回）。</summary>
        void SetPartVisible(bool visible)
        {
            if (_part == null || _part.transform == null) return;
            GameObject go = _part.transform.gameObject;
            CanvasGroup group = go.GetComponent(typeof(CanvasGroup)) as CanvasGroup;
            if (group == null) group = go.AddComponent(typeof(CanvasGroup)) as CanvasGroup;
            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
            group.interactable = visible;
        }

        public void Close()
        {
            if (!_open) return;
            try { if (_panel != null && _panel.panel != null && _panel.panel.isShow) _panel.Hide(); }
            catch (Exception e) { _ctx.Log.Warn(_ctx.T("关闭选英雄界面出错：", "Error closing the hero-select screen: ") + e.Message); }
            Closed();
        }

        void Closed()
        {
            _open = false;
            try
            {
                SetPartVisible(true);
                // 抽屉把「上次用的角色」记在了秘境名下：放回去，别影响玩家真去打秘境时的默认角色。
                if (_savedLastCharacter != 0) SettingsManager.SetLastCharacterId(_savedLastCharacter, GameMode.RogueMode);
            }
            catch (Exception e) { _ctx.Log.Warn(_ctx.T("还原选英雄界面出错：", "Error restoring the hero-select screen: ") + e.Message); }
            _session.Persist();
        }

        // ── 每隔一会儿：界面 → 设置，设置 → 仙命图标 ─────────────────────────────

        public void Tick()
        {
            if (!_open || _panel == null) return;
            try
            {
                bool shown = _panel.panel != null && _panel.panel.isShow;
                if (shown) _seenShown = true;
                else if (_seenShown) { Closed(); return; }      // 玩家按了 ESC / 返回
                if (!shown) return;
                SyncFromRoom();
                PaintTalents();
            }
            catch (Exception e)
            {
                _ctx.Log.Error(_ctx.T("选英雄界面同步出错，已关闭", "Hero-select sync failed; closed"), e);
                Close();
            }
        }

        public static int[] InnateTalents(int characterId)
        {
            CharacterConfig config = ConfigManager.GetCharacterConfig(characterId);
            if (config == null || config.talents == null) return new int[0];
            var ids = new int[config.talents.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = config.talents[i];
            return ids;
        }

        /// <summary>抽屉里选的角色 / 皮肤读回当前这一方。换了角色就带上新角色自带的仙命。</summary>
        void SyncFromRoom()
        {
            RoomCharacterSelectionPart selection = _panel.characterSelectionPart;
            if (selection == null || selection.isRandom || selection.characterId <= 0) return;
            ArenaSide side = _session.SideAt(_side);
            if (side.CharacterId != selection.characterId || !side.HasAnyTalent())
                side.SetCharacter(selection.characterId, selection.skinNumber, selection.skinColor, InnateTalents(selection.characterId));
            else
            {
                side.SkinNumber = selection.skinNumber;
                side.SkinColor = selection.skinColor;
            }
        }

        /// <summary>把界面切到某一方的角色上。</summary>
        void ShowSide()
        {
            ArenaSide side = _session.SideAt(_side);
            if (!side.HasAnyTalent()) side.SetCharacter(side.CharacterId, side.SkinNumber, side.SkinColor, InnateTalents(side.CharacterId));
            _panel.characterSelectionPart.SetCharacterInfo(side.CharacterId, side.SkinNumber, side.SkinColor, true);
        }

        public void SwitchSide()
        {
            if (!_open) return;
            SyncFromRoom();
            _side = 1 - _side;
            ShowSide();
            _session.Persist();
        }

        /// <summary>角色旁那一排仙命图标：第 i 个就是第 i 个仙命槽。游戏在换角色时会把它们刷回角色自带的，所以隔一会儿对一下。</summary>
        void PaintTalents()
        {
            Transform container = TalentContainer();
            if (container == null) return;
            ArenaSide side = _session.SideAt(_side);
            for (int i = 0; i < container.childCount && i < ArenaSide.TalentSlots; i++)
            {
                TalentItem item = ItemAt(container, i);
                if (item == null || side.Talents[i] == 0) continue;
                if (item.talentId != side.Talents[i]) item.talentId = side.Talents[i];
            }
        }

        Transform TalentContainer()
        {
            RoomCharacterItem character = _panel.roomCharItem;
            if (character == null) return null;
            RectTransform talents = character.FindComponent<RectTransform>("Talents");
            return talents;
        }

        static TalentItem ItemAt(Transform container, int index)
        {
            Transform child = container.GetChild(index);
            ILRComponentBridge bridge = child.GetComponent(typeof(ILRComponentBridge)) as ILRComponentBridge;
            return bridge != null ? bridge.GetILRObject<TalentItem>() : null;
        }

        // ── 钩子 ──────────────────────────────────────────────────────────

        bool OnRoguePartShow(HookContext h)
        {
            if (!_open) return true;
            h.Skip(null);
            return false;
        }

        bool OnStartClick(HookContext h)
        {
            if (!_open) return true;
            h.Skip(null);
            try
            {
                SyncFromRoom();
                Close();
                _enter();
            }
            catch (Exception e) { _ctx.Log.Error(_ctx.T("从选英雄界面进场出错", "Entering from the hero-select screen failed"), e); }
            return false;
        }

        bool OnTalentClick(HookContext h)
        {
            if (!_open || _panel == null) return true;
            TalentItem item = h.Instance as TalentItem;
            Transform container = TalentContainer();
            if (item == null || container == null || item.transform.parent != container) return true;
            int slot = item.transform.GetSiblingIndex();
            if (slot < 0 || slot >= ArenaSide.TalentSlots) return true;
            h.Skip(null);
            try { _pickTalent(slot, item.transform as RectTransform); }
            catch (Exception e) { _ctx.Log.Error(_ctx.T("打开全仙命表出错", "Opening the all-talents list failed"), e); }
            return false;
        }
    }
}
