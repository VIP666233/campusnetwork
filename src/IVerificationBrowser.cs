using System;
using System.Threading;
using System.Threading.Tasks;

namespace CampusNet
{
	internal interface IVerificationBrowser : IDisposable
	{
		Task OpenAsync(string url, CancellationToken token);

		Task<VerificationPage> InspectAsync(CancellationToken token);

		Task CloseAsync();
	}
}
