using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace K4os.Xpovoc.Core
{
	public class DefaultJobHandler: NewScopeJobHandler
	{
		public DefaultJobHandler(IServiceProvider provider): base(provider) { }

		protected override Task Handle(
			CancellationToken token, IServiceProvider services, object payload)
		{
			var messageType = payload.GetType();
			var handlerType = GetHandlerType(messageType);
			var handler = services.GetRequiredService(handlerType);
			var handlerInvoker = GetHandlerInvoker(messageType);
			return handlerInvoker(handler, token, payload);
		}

		private static readonly ConcurrentDictionary<Type, Type> HandlerTypes =
			new ConcurrentDictionary<Type, Type>();

		private static Type GetHandlerType(Type messageType) =>
			HandlerTypes.GetOrAdd(messageType, NewHandlerType);

		private static Type NewHandlerType(Type messageType) =>
			typeof(IJobHandler<>).MakeGenericType(messageType);

		private delegate Task HandlerInvoker(
			object handler, CancellationToken token, object message);

		private static readonly ConcurrentDictionary<Type, HandlerInvoker> HandlerInvokers =
			new ConcurrentDictionary<Type, HandlerInvoker>();

		private static HandlerInvoker GetHandlerInvoker(Type messageType) =>
			HandlerInvokers.GetOrAdd(messageType, NewHandlerInvoker);
		
		public Task GenericHandlerInvoker<TMessage>(
			object handler, CancellationToken token, object message) =>
			((IJobHandler<TMessage>) handler).Handle(token, (TMessage) message);

		private static HandlerInvoker NewHandlerInvoker(Type messageType)
		{
			var handlerArg = Expression.Parameter(typeof(object));
			var tokenArg = Expression.Parameter(typeof(CancellationToken));
			var messageArg = Expression.Parameter(typeof(object));
			const BindingFlags bindingFlags = BindingFlags.NonPublic | BindingFlags.Static;
			var method = typeof(DefaultJobHandler)
				.GetMethod(nameof(GenericHandlerInvoker), bindingFlags)
				.Required(nameof(GenericHandlerInvoker))
				.MakeGenericMethod(messageType);
			var body = Expression.Call(
				method, handlerArg, tokenArg, messageArg);
			var lambda = Expression.Lambda<HandlerInvoker>(
				body, handlerArg, tokenArg, messageArg);
			return lambda.Compile();
		}
	}
}
