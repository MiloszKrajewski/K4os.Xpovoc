# Χρόνος

It is actually Χρόνος (eng. Chronos) - personification of time in Greek mythology.

I've built it after trying to fix MySql driver for [Hangfire](https://www.hangfire.io/).
Don't get me wrong: Hangfire is an excellent piece of software (MySql driver not so much though), 
and Χρόνος provides less, but it provides exactly what I needed. 

It is lightweight scheduler, build with CQRS in mind.
The only thing it really does is: "remind me about this at tis time", that's it.

There are two interfaces:

```c#
interface IJobScheduler 
{
    Task<Guid> Schedule(DateTimeOffset time, object message);
}
```

and:

```c#
interface IJobHandler
{
    Task Handle(CancellationToken token, object message);
}
```

where implementation of `IJobScheduler` is provided by Χρόνος 
while `IJobHandler` needs to be provided by the user.

Simplistic implementation of `IJobScheduler` would be:

```c#
class SimplisticJobScheduler: IJobScheduler
{
    public SimplisticJobScheduler(IJobHandler handler) =>
        _handler = handler;

    public Task<Guid> Schedule(DateTimeOffset time, object message)
    {
        Task
            .Delay(time.Subtract(DateTimeOffset.Now))
            .ContinueWith(_ => _handler.Handle(CancellationToken.None, message));
    }
}
```

Job's done. 

What Χρόνος is providing is some: persistence, distributed processing and error handling.    

# K4os.Xpovoc

[![NuGet Stats](https://img.shields.io/nuget/v/K4os.Xpovoc.svg)](https://www.nuget.org/packages/K4os.Xpovoc)

# Usage

TBD

# Build

```shell
paket install
fake build
```
