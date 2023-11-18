using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable once CheckNamespace

namespace System;

internal static class Extensions
{
	public static T Required<T>(
		this T? subject, string? name = null) where T: class =>
		subject ?? throw new ArgumentNullException(name ?? "<unknown>");

	public static void TryDispose(this object subject)
	{
		if (subject is IDisposable disposable) disposable.Dispose();
	}

	public static int Compare<T>(T subject, T limit) =>
		Comparer<T>.Default.Compare(subject, limit);

	public static TimeSpan Times(this TimeSpan subject, double scale) =>
		TimeSpan.FromSeconds(subject.TotalSeconds * scale);

	public static T ClampBetween<T>(this T subject, T min, T max) =>
		subject.NotLessThan(min).NotMoreThan(max);

	public static T NotLessThan<T>(this T subject, T limit) =>
		Compare(subject, limit) < 0 ? limit : subject;

	public static T NotMoreThan<T>(this T subject, T limit) =>
		Compare(subject, limit) > 0 ? limit : subject;

	public static string? NotBlank(this string? text, string? defaultValue = null) =>
		string.IsNullOrWhiteSpace(text) ? defaultValue : text;

	public static R PipeTo<T, R>(this T subject, Func<T, R> func) =>
		func(subject);

	public static void PipeTo<T>(this T subject, Action<T> func) =>
		func(subject);

	public static T TapWith<T>(this T subject, Action<T> func)
	{
		func(subject);
		return subject;
	}

	public static DateTime ToUtc(this DateTime timestamp) =>
		timestamp.Kind switch {
			DateTimeKind.Utc => timestamp,
			DateTimeKind.Unspecified => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc),
			_ => timestamp.ToUniversalTime(),
		};

	public static T SharedNotNull<T>(this T? subject) where T: class, new() =>
		subject ?? System.SharedNotNull<T>.Instance;

	public static IEnumerable<T> NotNull<T>(this IEnumerable<T>? subject) =>
		subject ?? Array.Empty<T>();

	public static T[] NotNull<T>(this T[]? subject) =>
		subject ?? Array.Empty<T>();

	public static T[] EnsureArray<T>(this IEnumerable<T>? subject) =>
		subject switch { null => Array.Empty<T>(), T[] a => a, _ => subject.ToArray() };

	public static void AddIfNotNull<K, V>(
		this IDictionary<K, V> dictionary, K key, V? value) where V: class
	{
		if (value is not null) 
			dictionary.Add(key, value);
	}
	
	public static V? TryGetOrDefault<K, V>(
		this IDictionary<K, V> dictionary, K key, V? fallback = default) => 
		dictionary.TryGetValue(key, out var value) ? value : fallback;
	
	public static IEnumerable<T> WhereNotNull<T>(
		this IEnumerable<T?> sequence) => 
		sequence.Where(x => x is not null)!;

	public static IEnumerable<R> SelectNotNull<T, R>(
		this IEnumerable<T> sequence, Func<T, R?> map) =>
		sequence.Select(map).Where(x => x is not null)!;
	
	public static void Await(this Task task) => task.GetAwaiter().GetResult();
	public static T Await<T>(this Task<T> task) => task.GetAwaiter().GetResult();
	public static void Forget(this Task task) => 
		task.ContinueWith(_ => _.Exception, TaskContinuationOptions.OnlyOnFaulted);

}

internal class SharedNotNull<T> where T: class, new()
{
	public static readonly T Instance = new();
}