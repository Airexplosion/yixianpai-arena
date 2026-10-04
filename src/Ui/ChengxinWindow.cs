using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using Yx.ModSdk.Unity;
using YxArena.Game;

namespace YxArena.Views
{
    /// <summary>Edits native sword talents and their counters, never global card configs.</summary>
    public sealed class ChengxinWindow
    {
        sealed class BranchButton
        {
            readonly ChengxinWindow _owner;
            readonly int _row;
            public UiButton Button;
            public BranchButton(ChengxinWindow owner, int row) { _owner = owner; _row = row; }
            public void Click() { _owner._draft.Next(_row); _owner.Refresh(); }
        }
        readonly UiKit _ui;
        readonly ArenaSession _session;
        readonly DealHook _deal;
        readonly Action _changed;
        readonly BranchButton[] _branches = new BranchButton[4];
        readonly TextMeshProUGUI[] _effects = new TextMeshProUGUI[4];
        GameObject _root;
        UiInput _grinding;
        TextMeshProUGUI _message;
        ChengxinSetup _draft;
        int _side;
        int _character;

        public ChengxinWindow(UiKit ui, ArenaSession session, DealHook deal, Action changed)
        { _ui = ui; _session = session; _deal = deal; _changed = changed; }

        public void Open()
        {
            Close();
            if (!_session.InPlacement || !_deal.IsSwordOwner())
            { Ui.Toast(Loc.T("请在备战界面切换到陆剑心", "Select Lu Jianxin on the setup screen")); return; }
            _session.Autosave();
            _side = _session.EditingIndex;
            _character = _session.Editing.CharacterId;
            _draft = new ChengxinSetup(_session.Editing);
            var center = new Vector2(0.5f, 0.5f);
            _root = _ui.Panel("YxArenaChengxin", center, center, Vector2.zero, new Vector2(1020f, 814f), new Color(0.06f, 0.08f, 0.12f, 0.97f));
            if (_root == null) return;
            RectTransform parent = _root.transform.parent as RectTransform;
            if (parent != null && parent.rect.width > 20f && parent.rect.height > 20f)
            {
                float scale = Mathf.Min(1f, Mathf.Min((parent.rect.width - 20f) / 1020f, (parent.rect.height - 20f) / 814f));
                _root.transform.localScale = new Vector3(scale, scale, scale);
            }
            _ui.Label(_root.transform, "title", Loc.T("澄心剑 · ", "Chengxin Sword · ") + _session.Editing.Name, new Vector2(20f, -16f), new Vector2(570f, 40f), 25f);
            _ui.TextButton(_root.transform, "close", Loc.T("关闭", "Close"), new Vector2(892f, -14f), new Vector2(108f, 40f), Close);
            _ui.Label(_root.transform, "hint", Loc.T("点分支按钮循环选择，右侧显示当前选择的完整效果。修改后先应用，再发牌。", "Cycle a branch to see its full effect on the right. Apply changes before dealing."), new Vector2(20f, -68f), new Vector2(980f, 52f), 19f);
            _ui.Label(_root.transform, "grindLabel", Loc.T("磨剑次数", "Grinding count"), new Vector2(20f, -130f), new Vector2(160f, 40f), 22f);
            _grinding = _ui.NumberInput(_root.transform, "grinding", new Vector2(180f, -124f), new Vector2(130f, 40f), 6, OnInput);
            if (_grinding != null) _grinding.Show(_draft.Grinding.ToString(CultureInfo.InvariantCulture));
            Description("grindingEffect", Effect(ChengxinSetup.GrindingTalent), new Vector2(340f, -120f), new Vector2(660f, 90f));
            for (int i = 0; i < 4; i++)
            {
                float y = -216f - 125f * i;
                _ui.Label(_root.transform, "realm", ArenaConfig.LevelName(i + 2), new Vector2(20f, y - 5f), new Vector2(100f, 40f), 22f);
                var branch = new BranchButton(this, i);
                _branches[i] = branch;
                branch.Button = _ui.TextButton(_root.transform, "branch", "", new Vector2(130f, y), new Vector2(320f, 40f), branch.Click);
                _effects[i] = Description("branchEffect", "", new Vector2(470f, y), new Vector2(530f, 110f));
            }
            _message = _ui.Label(_root.transform, "message", "", new Vector2(20f, -718f), new Vector2(980f, 30f), 18f);
            _ui.TextButton(_root.transform, "apply", Loc.T("应用专属参数", "Apply sword settings"), new Vector2(20f, -760f), new Vector2(270f, 40f), Apply);
            _ui.TextButton(_root.transform, "deal", Loc.T("发一张澄心剑", "Deal Chengxin Sword"), new Vector2(310f, -760f), new Vector2(270f, 40f), Deal);
            Refresh();
        }

        void OnInput(string text) { }
        bool Valid()
        {
            if (_session.InPlacement && _session.EditingIndex == _side && _session.Editing.CharacterId == _character && _deal.IsSwordOwner()) return true;
            Close();
            return false;
        }
        void Apply()
        {
            if (!Valid()) return;
            if (!_draft.Apply(_session.Editing, _grinding == null ? "" : _grinding.Text))
            { Message(Loc.T("磨剑次数须为 0–999999 的整数", "Grinding count must be an integer 0–999999")); return; }
            _session.ReapplySettings();
            _changed();
            Message(Loc.T("已应用，已有的剑也按新仙命计算", "Applied; existing swords use the updated talents"));
        }
        void Deal() { if (Valid()) _deal.DealSword(); }
        void Message(string text) { if (_message != null) _message.text = text; }
        static string Effect(int talentId)
        {
            if (talentId == 0) return Loc.T("不追加该阶段的澄心剑效果。", "No sword effect added for this realm.");
            Proto.TalentConfig config = ConfigManager.GetTalentConfig(talentId);
            if (config == null) return Loc.T("无法读取该仙命的效果说明", "Couldn't read the talent effect");
            return ConfigExtension.ParseDescription(config, Proto.GameMode.PracticeMode, OpenManager.seasonMec);
        }
        TextMeshProUGUI Description(string name, string text, Vector2 position, Vector2 size)
        {
            TextMeshProUGUI label = _ui.Label(_root.transform, name, text, position, size, 18f);
            if (label != null)
            {
                label.enableWordWrapping = true;
                label.enableAutoSizing = true;
                label.fontSizeMin = 14f;
                label.fontSizeMax = 18f;
            }
            return label;
        }
        void Refresh()
        {
            for (int i = 0; i < _branches.Length; i++)
            {
                if (_branches[i] != null && _branches[i].Button != null)
                    _branches[i].Button.SetText(_draft.Selected[i] == 0 ? Loc.T("不选分支 ▶", "No branch ▶") : TranslateUtil.GetTalentTranslate(_draft.Selected[i]) + " ▶");
                if (_effects[i] != null) _effects[i].text = Effect(_draft.Selected[i]);
            }
        }
        public void Close()
        {
            if (_root != null) _ui.Destroy(_root);
            _root = null;
            _grinding = null;
            _message = null;
        }
    }
}
