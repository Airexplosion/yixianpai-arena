namespace YxArena
{
    /// <summary>
    /// 界面文案的中英切换。入口类在 OnLoad 里按游戏语言设一次 <see cref="En"/>，
    /// 没有 ModContext 的类（纯 UI / 静态帮助方法）经 <see cref="T"/> 取当前语言的文案。
    /// 默认中文（测试进程里 En 保持 false，断言的中文原样成立）。
    /// </summary>
    public static class Loc
    {
        /// <summary>游戏当前是英文界面。由入口类的 OnLoad 从 ctx.Lang 设一次。</summary>
        public static bool En;

        /// <summary>英文界面返回 en，否则返回 zh。</summary>
        public static string T(string zh, string en) { return En ? en : zh; }
    }
}
