namespace YxArena
{
    /// <summary>
    /// 步进的状态机。「要打下一张牌了」由战斗钩子告诉它（CardActionBase.CheckCardCost 的前置）；
    /// 真正的暂停 / 继续由调用方去点游戏自己的播放按钮。
    /// </summary>
    public sealed class StepController
    {
        bool _armed;
        bool _firstCardPending;

        /// <summary>开着时，每场战斗在第一张牌之前先停下。</summary>
        public bool PauseAtStart;

        public void OnBattleStart()
        {
            _armed = false;
            _firstCardPending = PauseAtStart;
        }

        /// <summary>按了「步进」。返回是否需要放行（当前在暂停）；下一张牌之前会再停下。</summary>
        public bool RequestStep(bool paused)
        {
            _armed = true;
            _firstCardPending = false;
            return paused;
        }

        /// <summary>要打下一张牌了：现在该不该停。</summary>
        public bool ShouldPauseBeforeCard()
        {
            if (_firstCardPending)
            {
                _firstCardPending = false;
                return true;
            }
            if (!_armed) return false;
            _armed = false;
            return true;
        }
    }
}
