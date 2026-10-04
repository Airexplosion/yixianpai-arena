namespace YxArena
{
	public sealed class OncePerOwner
	{
		private object[] _owners = new object[16];

		private int[] _ids = new int[16];

		private int _count;

		public void Reset()
		{
			for (int i = 0; i < _count; i++)
			{
				_owners[i] = null;
			}
			_count = 0;
		}

		public bool FirstTime(object owner, int id)
		{
			for (int i = 0; i < _count; i++)
			{
				if (_ids[i] == id && _owners[i] == owner)
				{
					return false;
				}
			}
			if (_count == _owners.Length)
			{
				Grow();
			}
			_owners[_count] = owner;
			_ids[_count] = id;
			_count++;
			return true;
		}

		public void Forget(object owner)
		{
			int num = 0;
			for (int i = 0; i < _count; i++)
			{
				if (_owners[i] != owner)
				{
					_owners[num] = _owners[i];
					_ids[num] = _ids[i];
					num++;
				}
			}
			for (int j = num; j < _count; j++)
			{
				_owners[j] = null;
			}
			_count = num;
		}

		private void Grow()
		{
			object[] array = new object[_owners.Length * 2];
			int[] array2 = new int[_ids.Length * 2];
			for (int i = 0; i < _count; i++)
			{
				array[i] = _owners[i];
				array2[i] = _ids[i];
			}
			_owners = array;
			_ids = array2;
		}
	}
}
