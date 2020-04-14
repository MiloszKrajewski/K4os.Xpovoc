using System;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;
using MediatR;

namespace K4os.Xpovoc.MediatR
{
	public class JobHandlerMediatorAdapter: IJobHandler
	{
		private readonly IMediator _mediator;

		public JobHandlerMediatorAdapter(IMediator mediator)
		{
			_mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
		}

		public Task Handle(CancellationToken token, object payload)
		{
			if (payload is INotification)
			{
				return _mediator.Publish(payload, token);
			}
			else if (payload is IRequest)
			{
				return _mediator.Send(payload, token);
			}
			else
			{
				throw new ArgumentException(
					string.Format(
						"{0} is neither {1} nor {2} so it cannot by handled by MediatR",
						payload.GetType().Name,
						nameof(IRequest),
						nameof(INotification)));
			}
		}
	}
}
