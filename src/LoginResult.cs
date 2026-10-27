namespace CampusNet
{
	public sealed class LoginResult
	{
		public string PortalUrl;

		public bool Confirmed;

		public bool Failed;

		public bool Submitted;

		public string Message;

		public string RedirectUrl;

		public bool Completed
		{
			get
			{
				if (!Confirmed)
				{
					if (Submitted)
					{
						return !Failed;
					}
					return false;
				}
				return true;
			}
		}
	}
}
