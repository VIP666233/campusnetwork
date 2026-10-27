using System;
using System.Linq;
using System.Windows.Forms;

namespace CampusNet
{
	public sealed class SettingsChanges
	{
		private string[] saved = new string[0];

		public void Accept(string[] values)
		{
			saved = (string[])values.Clone();
		}

		public bool HasChanges(string[] values)
		{
			return !saved.SequenceEqual(values);
		}

		public bool Confirm(string[] values, Func<DialogResult> ask, Func<bool> save)
		{
			if (!HasChanges(values))
			{
				return true;
			}
			switch (ask())
			{
			case DialogResult.Yes:
				if (!save())
				{
					return false;
				}
				Accept(values);
				return true;
			case DialogResult.No:
				return true;
			default:
				return false;
			}
		}
	}
}
