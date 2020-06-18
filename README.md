# Χρόνος ![icon](doc/icon-32.png)

It is actually Χρόνος (eng. Chronos) - personification of time in Greek mythology.

I've built it after trying to fix MySql driver for [Hangfire](https://www.hangfire.io/).
Don't get me wrong: Hangfire is an excellent piece of software (MySql driver not so much though), 
and Χρόνος provides less, but it provides exactly what I needed. 

It is lightweight scheduler, build with CQRS in mind.
The only thing it really does is: "send this message to me at this time", that's it.

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
        Task.Delay(time.Subtract(DateTimeOffset.Now));
            .ContinueWith(_ => _handler.Handle(CancellationToken.None, message));
        return Task.FromResult(Guid.NewGuid());
    }
}
```

Job's done. 

So why Χρόνος exists then? Χρόνος is providing: persistence, distributed processing and retries.

# K4os.Xpovoc

| Name | Nuget | Description |
|:-|:-:|:-|
| `K4os.Xpovoc.Abstractions` | [![NuGet Stats](https://img.shields.io/nuget/v/K4os.Xpovoc.Abstractions.svg)](https://www.nuget.org/packages/K4os.Xpovoc.Abstractions) | Interfaces. Everything you need to use Χρόνος |
| `K4os.Xpovoc.Core` | [![NuGet Stats](https://img.shields.io/nuget/v/K4os.Xpovoc.Core.svg)](https://www.nuget.org/packages/K4os.Xpovoc.Core) | Implementation of core components, like `JobScheduler` |
| `K4os.Xpovoc.MySql` | [![NuGet Stats](https://img.shields.io/nuget/v/K4os.Xpovoc.MySql.svg)](https://www.nuget.org/packages/K4os.Xpovoc.MySql) | `JobStorage` implementation for MySql |
| `K4os.Xpovoc.PgSql` | [![NuGet Stats](https://img.shields.io/nuget/v/K4os.Xpovoc.PgSql.svg)](https://www.nuget.org/packages/K4os.Xpovoc.PgSql) | `JobStorage` implementation for Postgres |
| `K4os.Xpovoc.MsSql` | [![NuGet Stats](https://img.shields.io/nuget/v/K4os.Xpovoc.MsSql.svg)](https://www.nuget.org/packages/K4os.Xpovoc.MsSql) | `JobStorage` implementation for SQL Server |
| `K4os.Xpovoc.SqLite` | [![NuGet Stats](https://img.shields.io/nuget/v/K4os.Xpovoc.SqLite.svg)](https://www.nuget.org/packages/K4os.Xpovoc.SqLite) | `JobStorage` implementation for Sqlite |
| `K4os.Xpovoc.Json` | [![NuGet Stats](https://img.shields.io/nuget/v/K4os.Xpovoc.Json.svg)](https://www.nuget.org/packages/K4os.Xpovoc.Json) | Json serialization for database storage using [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json) |
| `K4os.Xpovoc.MediatR` | [![NuGet Stats](https://img.shields.io/nuget/v/K4os.Xpovoc.MediatR.svg)](https://www.nuget.org/packages/K4os.Xpovoc.MediatR) | [MediatR](https://github.com/jbogard/MediatR) integration |

# Usage

TBD

# Build

```shell
paket install
fake build
```
