using System;
using System.Collections.Generic;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable once CheckNamespace

namespace System
{
	internal static class Extensions
	{
		public static T Required<T>(this T subject, string name = null) where T: class =>
			subject ?? throw new ArgumentNullException(name ?? "<unknown>");

		public static void TryDispose(this object subject)
		{
			if (subject is IDisposable disposable) disposable.Dispose();
		}

		public static int Compare<T>(T subject, T limit) =>
			Comparer<T>.Default.Compare(subject, limit);

		public static TimeSpan Times(this TimeSpan subject, double scale) =>
			TimeSpan.FromSeconds(subject.TotalSeconds * scale);

		public static T NotLessThan<T>(this T subject, T limit) =>
			Compare(subject, limit) < 0 ? limit : subject;

		public static T NotMoreThan<T>(this T subject, T limit) =>
			Compare(subject, limit) > 0 ? limit : subject;

		public static R PipeTo<T, R>(this T subject, Func<T, R> func) =>
			func(subject);

		public static void PipeTo<T>(this T subject, Action<T> func) =>
			func(subject);

		public static T TapWith<T>(this T subject, Action<T> func)
		{
			func(subject);
			return subject;
		}
	}
}
