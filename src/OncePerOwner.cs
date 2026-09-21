namespace YxArena
{
    /// <summary>
    /// 「这个对象上这个 id 是不是第一次出现」。练习场允许同一个仙命占几个槽，而游戏战斗界面给仙命图标建表时拿仙命 id 当键
    /// （BattleCharacterUI.AddTalentBuff），同一个 id 加第二次就抛异常、整场战斗起不来——所以第二次起跳过（图标只显示一个，
    /// 数据里仍然是几份）。游戏清空那张表（ResetBuffItem）时 Forget，每场战斗开始时 Reset。纯逻辑；用自己的数组，不用值类型参数的 CLR 泛型集合。
    /// </summary>
    public sealed class OncePerOwner
    {
        object[] _owners = new object[16];
        int[] _ids = new int[16];
        int _count;

        public void Reset()
        {
            for (int i = 0; i < _count; i++) _owners[i] = null;
            _count = 0;
        }

        public bool FirstTime(object owner, int id)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_ids[i] == id && object.ReferenceEquals(_owners[i], owner)) return false;
            }
            if (_count == _owners.Length) Grow();
            _owners[_count] = owner;
            _ids[_count] = id;
            _count++;
            return true;
        }

        /// <summary>这个对象的记录全部忘掉（它自己的表清空了）。</summary>
        public void Forget(object owner)
        {
            int kept = 0;
            for (int i = 0; i < _count; i++)
            {
                if (object.ReferenceEquals(_owners[i], owner)) continue;
                _owners[kept] = _owners[i];
                _ids[kept] = _ids[i];
                kept++;
            }
            for (int i = kept; i < _count; i++) _owners[i] = null;
            _count = kept;
        }

        void Grow()
        {
            var owners = new object[_owners.Length * 2];
            var ids = new int[_ids.Length * 2];
            for (int i = 0; i < _count; i++)
            {
                owners[i] = _owners[i];
                ids[i] = _ids[i];
            }
            _owners = owners;
            _ids = ids;
        }
    }
}
