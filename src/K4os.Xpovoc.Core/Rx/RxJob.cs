using System;

namespace K4os.Xpovoc.Rx
{
	internal class RxJob
	{
		public IDisposable Schedule { get; set; }
		public Guid Id { get; set; }
		public object Payload { get; set; }
		public int Attempt { get; set; }
	}
}
