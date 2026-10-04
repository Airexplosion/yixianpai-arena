namespace YxArena
{
	public sealed class StepController
	{
		private bool _armed;

		private bool _firstCardPending;

		public bool PauseAtStart;

		public void OnBattleStart()
		{
			_armed = false;
			_firstCardPending = PauseAtStart;
		}

		public bool RequestStep(bool paused)
		{
			_armed = true;
			_firstCardPending = false;
			return paused;
		}

		public bool ShouldPauseBeforeCard()
		{
			if (_firstCardPending)
			{
				_firstCardPending = false;
				return true;
			}
			if (!_armed)
			{
				return false;
			}
			_armed = false;
			return true;
		}
	}
}
