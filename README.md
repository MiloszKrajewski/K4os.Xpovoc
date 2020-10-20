# Χρόνος ![icon](doc/icon-32.png)

It is actually Χρόνος (eng. Chronos) - personification of time in Greek mythology.

I've built it after trying to fix MySql driver for [Hangfire](https://www.hangfire.io/).
Don't get me wrong: Hangfire is an excellent piece of software (MySql driver not so much though), 
and Χρόνος provides less, but it provides exactly what I needed. 

It is lightweight scheduler, build with CQRS in mind.
The only thing it really does is: "send this message back to me when the time comes", that's it.

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

## IJobScheduler

`IJobScheduler` is meant to schedule a message at a given time. 
You can imagine that very naive implementation of `IJobScheduler` would be: 

```c#
class NaiveJobScheduler: IJobScheduler
{
    public SimplisticJobScheduler(IJobHandler handler) =>
        _handler = handler;

    public Task<Guid> Schedule(DateTimeOffset time, object message)
    {
        var delay = time.Subtract(DateTimeOffset.Now);
        Task.Delay(delay).ContinueWith(_ => _handler.Handle(CancellationToken.None, message));
        return Task.FromResult(Guid.NewGuid());
    }
}
```

making sure message gets delivered at given time.

## IJobHandler

Handler needs to be implemented by the user. It is responsible for handling messages.
Χρόνος does not care at all how you implement this handler.

The simplistic approach would be to have massive switch statement:

```c#
internal class SimplisticJobHandler: IJobHandler
{
    public Task Handle(CancellationToken token, object payload)
    {
        switch (payload) 
        {
            case CommandA a:
                await HandleCommandA(a, token);
                return;
            case CommandB b:
                await HandleCommandB(b, token);
                return;
            //...
            default:
                throw new ArgumentException(
                    $"No handler for command {payload.GetType().Name}");
        };
    }
}
```

This is, of course, pretty ineffective so I would recommend some message routing, using, 
for example, (well established) [MediatR](https://github.com/jbogard/MediatR) or, 
another of my toys, [RoutR](https://github.com/MiloszKrajewski/K4os.RoutR).

MediatR integration is already implemented, but it if you are curious how it is done you can check 
[here](https://github.com/MiloszKrajewski/K4os.Xpovoc/blob/master/src/K4os.Xpovoc.MediatR/MediatorJobHandler.cs).

So what Χρόνος actually does? Χρόνος is providing three things: persistence, distributed processing and retries.

# K4os.Xpovoc

| Name | Nuget | Description |
|:-|:-:|:-|
| `K4os.Xpovoc.Abstractions` | [![NuGet Stats](https://img.shields.io/nuget/v/K4os.Xpovoc.Abstractions.svg)](https://www.nuget.org/packages/K4os.Xpovoc.Abstractions) | Interfaces. Everything you need to *use* Χρόνος |
| `K4os.Xpovoc.Core` | [![NuGet Stats](https://img.shields.io/nuget/v/K4os.Xpovoc.Core.svg)](https://www.nuget.org/packages/K4os.Xpovoc.Core) | Implementation of core components, like `JobScheduler`, default binary job serializer (`[Serializable]`) and in-memory scheduler using [Rx](https://github.com/dotnet/reactive)  |
| `K4os.Xpovoc.MySql` | [![NuGet Stats](https://img.shields.io/nuget/v/K4os.Xpovoc.MySql.svg)](https://www.nuget.org/packages/K4os.Xpovoc.MySql) | `JobStorage` implementation for MySql |
| `K4os.Xpovoc.PgSql` | [![NuGet Stats](https://img.shields.io/nuget/v/K4os.Xpovoc.PgSql.svg)](https://www.nuget.org/packages/K4os.Xpovoc.PgSql) | `JobStorage` implementation for Postgres |
| `K4os.Xpovoc.MsSql` | [![NuGet Stats](https://img.shields.io/nuget/v/K4os.Xpovoc.MsSql.svg)](https://www.nuget.org/packages/K4os.Xpovoc.MsSql) | `JobStorage` implementation for SQL Server |
| `K4os.Xpovoc.SqLite` | [![NuGet Stats](https://img.shields.io/nuget/v/K4os.Xpovoc.SqLite.svg)](https://www.nuget.org/packages/K4os.Xpovoc.SqLite) | `JobStorage` implementation for Sqlite |
| `K4os.Xpovoc.Json` | [![NuGet Stats](https://img.shields.io/nuget/v/K4os.Xpovoc.Json.svg)](https://www.nuget.org/packages/K4os.Xpovoc.Json) | Json serialization for database storage using [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json) |
| `K4os.Xpovoc.MediatR` | [![NuGet Stats](https://img.shields.io/nuget/v/K4os.Xpovoc.MediatR.svg)](https://www.nuget.org/packages/K4os.Xpovoc.MediatR) | [MediatR](https://github.com/jbogard/MediatR) integration |

NOTE: I am a fan of not dragging too many dependencies (because they introduce risk of version mismatch) but some of them I consider "a standard":
* Core: uses [Reactive Extensions](https://github.com/dotnet/reactive) because Rx I'm in denial and I don't believe it is not part of the system
* MySql, PgSql, MsSql, and SqLite: all depend on [Dapper](https://github.com/StackExchange/Dapper) because dapper is good and should be part of `IDbConnection` extensions
* MySql: depends on [Polly](https://github.com/App-vNext/Polly) as Polly should be used
* Json: depends on [NewtonSoft.Json](https://github.com/JamesNK/Newtonsoft.Json) (of course)

Everything depends on interfaces though which are defined in `Abstractions` so you can reimplement any component yourself using whatever technology you like.

# Setup

One of the principles behind Χρόνος design was modularity. This gives a lot of freedom how things are 
implemented but also means some complexity setting it up.

## IJobHandler (TL;DR)

`IJobHandler` is just an interface with one method to implement, so you can always roll your own, 
but if you are reading this you probably don't want to. You just want to get going as quickly as 
possible.

There are two quick options for now: using internal very simple "message broker" `SimpleJobHandler` 
or integrate with [MediatR](https://github.com/jbogard/MediatR).

### SimpleJobHandler

Register broker itself:
```c#
collection.AddSingleton<IJobHandler, SimpleJobHandler>();
```
and then register handlers:
```c#
collection.AddScoped<IJobHandler<MessageA>>, MyMessageAHandler>();
collection.AddScoped<IJobHandler<MessageB>>, MyMessageBHandler>();
collection.AddScoped<IJobHandler<MessageC>>, MyMessageCHandler>();
//...
```

Please note, that `IJobHandler` is registered as singleton. Changing it won't change anything 
as it will be resolved only once.

### MediatR

Register `MediatR` adapter as `IJobHandler`:
```c#
collection.AddSingleton<IJobHandler, MediatorJobHandler>();
```

From now on, just keep registering `MediatR` handlers:

```c#
collection.AddScoped<IRequestHandler<MessageA>, MyMessageAHandler>();
collection.AddScoped<IRequestHandler<MessageB>, MyMessageBHandler>();
collection.AddScoped<IRequestHandler<MessageC>, MyMessageCHandler>();
//...
```

Please note, that `IJobHandler` is registered as singleton. Changing it won't change anything 
as it will be resolved only once.

## IJobHandler

Ok, but maybe you want to write your own handler. Maybe you want to integrate it with 
[Akka.NET](https://github.com/akkadotnet/akka.net), [Orleans](https://github.com/dotnet/orleans), or
[Brighter](https://github.com/BrighterCommand/Brighter). It is not hard but requires some 
understanding.

Χρόνος is not opinionated how you handle messages, all you need to do is to implement this interface:

```c#
interface IJobHandler
{
    Task Handle(CancellationToken token, object message);
}
```

but actually it comes with one caveat: it will be a singleton. It gets injected 
(once, through constructor) into `IJobScheduler` which is a singleton itself (because you want 
it to work all the time) therefore it becomes a singleton (regardless of your container 
configuration).

If you want to make your handler a factory of handlers you can. If you want your handler to be
created in new scope, you can. In such case I would recommend taking a look at 
[NewScopeJobHandler](src/K4os.Xpovoc.Core/NewScopeJobHandler.cs).
You can either inherit from it, or just replicate. The important part is:

```c#
async Task IJobHandler.Handle(CancellationToken token, object payload)
{
    using (var scope = _provider.CreateScope())
        await Handle(token, scope.ServiceProvider, payload);
}
```

You can do whatever you want from here: connect to Orleans cluster and send a message (to grain 
which Id is in your message), send message to some Akka actor which will take from here, dispatch it 
with Brighter, or put the message on some RabbitMQ queue.

Or... you just can have massing `switch` statement... Χρόνος does not care 
(the Gods of Software do though).

The other option of simple approach is to put a handler on a message itself, so you can:

```c#
async Task Handle(CancellationToken token, object payload)
{
    switch (payload) 
    {
        case ICanHandleMyself m: return m.Handle(token);
        default: throw new ArgumentException("It cannot handle itself");
    }
}
```

Throw `IServiceProvider` in new scope to the mix and you can have pretty compelling example 
of encapsulation.

## IJobScheduler (TL;DR)

The other important component of this system is `IJobSceduler` it is responsible for both storing, 
monitoring and and retrieving tasks to be executed. You probably want to persist your jobs in database, 
so you need to configure `DbJobScheduler`:

```c#
collection.AddSingleton<IJobScheduler, DbJobScheduler>();
```

...and that's it (for now, at least).

## IJobScheduler

Actually, Χρόνος comes with two separate implementations: `RxJobScheduler` and `DbJobScheduler`.

### RxJobScheduler

This is a very simple wrapper around [Reactive Extensions](https://github.com/dotnet/reactive) 
`IScheduler`. It uses provided or default scheduler to schedule jobs which allows for some
blend of Rx and CQRS. It also enables testing with simulated time.
Please note, this is in memory only scheduler.

### DbJobScheduler

This is the "real" scheduler. It requires `IJobHandler` (of course) but also requires storage 
mechanism. All the persistence is delegated to `IDbJobStorage` (read below). Some aspects, the ones
not related to database, can be configured using `ISchedulerConfig` (yet, it is not required).

## IDbJobStorage

The third component is actual storage. Currently Χρόνος comes with in 5 flavours:

* MySQL
* PostgreSQL
* Microsoft SQL Server
* SqLite
* Memory

I guess both SqLite and Memory are just toys, MySQL is the one which is really used, while both
PostgreSQL and Microsoft SQL Server, well... they do pass integration tests but I don't know if
they are used in production anywhere.

## IDbJobStorage (TL;DR)

Just pick the flavour you want and configure it:

```c#
collection.AddSingleton<IDbJobStorage, MySqlJobStorage>();
collection.AddSingleton<IMySqlJobStorageConfig>(
    new MySqlJobStorageConfig { ConnectionString = "..." });
```

## IJobSerializer (TL;DR)

All DB storage providers implemented by me may take `IJobSerializer` which is responsible
from serialization, to and from database. The default one is based on `BinaryFormatter` which
is known for being... not the best. I used it as default though because it does not require and
configuration, it just works (well, except for `[Serializable]` attribute, I guess).

## IJobSerializer

You can use any serialization mechanism you want though. All you need is to implement this interface:

```c#
public interface IJobSerializer
{
    string Serialize(object job);
    object Deserialize(string payload);
}
```

Why string, you might ask? Well, there was a decision to make do I store as byte blob which 
would be (much?) faster but will be inherently unreadable or store as string so I can use 
JSON for serialization which allows querying jobs if I really need to 
(at least for MySQL and PostgreSQL). I flipped the coin.
Actually, it could be solved with two fields... but it wasn't.

Anyway, you can use any serializer you want: [protobuf](https://github.com/protobuf-net/protobuf-net),
[Hyperion](https://github.com/hyperion-project/hyperion.ng)... I mean I don't know, whatever works for 
you.

Two things are worth noting:

* If you have binary serializer `Convert.ToBase64String` is you friend
* As jobs can be stored for very long time (scheduled something for next year?) you should check if 
your serialization mechanism is polymorphic and version tolerant, you don't want your old job to 
become unreadable after few sprints.

### JsonJobSerializer

**689,587,822 total downloads** is not a mistake. 
[Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json) is absolutely great and fits Χρόνος 
very well. It is human readable, polymorphic and version tolerant, all those things have very
high value in this case. It is not a fastest one, but it is not used for messaging but for persistence.
You can quickly configure it:

```c#
collection.AddSingleton<IJobSerializer>(
    new JsonJobSerializer(
        new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto }));
```

Please note, setting `TypeNameHandling` to `TypeNameHandling.Auto` makes it polymorphic.

There is a slight problem though, as `Newtonsoft.Json` is very version tolerant, 
polymorphism in `Newtonsoft.Json` is not very version tolerant (when you start refactoring, 
moving and renaming your jobs).

I don't want to get into details here, but there are two ways to resolve it:

* DON'T refactor, move nor rename your jobs
* Use [K4os.Json.KnownTypes](https://github.com/MiloszKrajewski/K4os.Json.KnownTypes)

# Usage

Ok, so we've seen it all.
Let me give you some minimal yet complete examples.
All database setup looks roughly the same, one of the differences is that both MySQL and SqLite 
do not have schemas (well, MySQL kind of does, but not exactly) so they use table name prefix 
(if you want to use it), while both MsSQL and PostgreSQL can use schema for encapsulation.   

## Configuration

There's nothing new here, but let's put all I said before together, for example:

MySQL, using `SimpleJobHandler` and `DefaultJobSerializer`:

```c#
collection.AddSingleton<IJobHandler, SimpleJobHandler>();
collection.AddSingleton<IJobScheduler, DbJobScheduler>();
collection.AddSingleton<IDbJobStorage, MySqlJobStorage>();
collection.AddSingleton<IMySqlJobStorageConfig>(
    new MySqlJobStorageConfig { ConnectionString = "<connection string>" });
```

PostgreSQL, using `MediatR` and `JsonJobSerializer`:

```c#
collection.AddSingleton<IJobHandler, MediatorJobHandler>();
collection.AddSingleton<IJobScheduler, DbJobScheduler>();
collection.AddSingleton<IDbJobStorage, PgSqlJobStorage>();
collection.AddSingleton<IPgSqlJobStorageConfig>(
    new PgSqlJobStorageConfig { ConnectionString = "<connection string>" });
collection.AddSingleton<IJobSerializer>(
    new JsonJobSerializer(
        new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto }));
```

## Making it tick

You did all the things above, and it just does not work. THere is still one thing you didn't do.
You did not start it. It is relatively obscure because there is no obvious `Start` method, but
there's no need for one, as it would do nothing. All you need to do is to request `IJobScheduler`
from service provider.

It may be different depending on what are you running: Console app, Web app, ASP.NET Core MVC, 
I have no idea. When you need to is to call:

```c#
provider.GetRequiredService<IJobScheduler>();
```

as early as possible (some `Startup` method or something).

## Usage

There is one thing which I missed. How to actually schedule a job?
Well, that's simple, just grab `IJobScheduler` for DI container and use the only method it has:

```c#
await scheduler.Schedule(
    DateTimeOffset.Now.AddDays(666),
    new TriggerApocalypse());
```

this message will get persisted in database and delivered back to you when the time comes 
(as long as are able to deserialize it then).

# Build

```shell
paket install
fake build
```
