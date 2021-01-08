using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using H.Socket.IO.EventsArgs;
using H.WebSockets.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace H.Socket.IO.IntegrationTests
{
    internal class BaseTests
    {
        public static async Task BaseTestAsync(
            Func<SocketIoClient, Task> func,
            CancellationToken cancellationToken = default, 
            params string[] additionalEvents)
        {
#if NETCOREAPP3_1 || NETCOREAPP3_0 || NET5_0
            await using var client = new SocketIoClient();
#else
            using var client = new SocketIoClient();
#endif

            client.Connected += (_, args) => Console.WriteLine($"Connected: {args.Namespace}");
            client.Disconnected += (_, args) => Console.WriteLine($"Disconnected. Reason: {args.Reason}, Status: {args.Status:G}");
            client.EventReceived += (_, args) => Console.WriteLine($"EventReceived: Namespace: {args.Namespace}, Value: {args.Value}, IsHandled: {args.IsHandled}");
            client.HandledEventReceived += (_, args) => Console.WriteLine($"HandledEventReceived: Namespace: {args.Namespace}, Value: {args.Value}");
            client.UnhandledEventReceived += (_, args) => Console.WriteLine($"UnhandledEventReceived: Namespace: {args.Namespace}, Value: {args.Value}");
            client.ErrorReceived += (_, args) => Console.WriteLine($"ErrorReceived: Namespace: {args.Namespace}, Value: {args.Value}");
            client.ExceptionOccurred += (_, args) => Console.WriteLine($"ExceptionOccurred: {args.Value}");

            var results = await client.WaitAllEventsAsync<EventArgs>(
                async () => await func(client), 
                cancellationToken, 
                new [] { nameof(client.Connected), nameof(client.Disconnected) }
                    .Concat(additionalEvents)
                    .ToArray());

            Console.WriteLine();
            Console.WriteLine($"WebSocket State: {client.EngineIoClient.WebSocketClient.Socket.State}");
            Console.WriteLine($"WebSocket CloseStatus: {client.EngineIoClient.WebSocketClient.Socket.CloseStatus}");
            Console.WriteLine($"WebSocket CloseStatusDescription: {client.EngineIoClient.WebSocketClient.Socket.CloseStatusDescription}");

            foreach (var pair in results)
            {
                Assert.IsNotNull(pair.Value, $"Client event(\"{pair.Key}\") did not happen");
            }
        }

        public static void EnableDebug(SocketIoClient client)
        {
            client.EngineIoClient.Opened += (_, args) => Console.WriteLine($"EngineIoClient.Opened. Sid: {args.Value?.Sid}");
            client.EngineIoClient.Closed += (_, args) => Console.WriteLine($"EngineIoClient.Closed. Reason: {args.Reason}, Status: {args.Status:G}");
            client.EngineIoClient.Upgraded += (_, args) => Console.WriteLine($"EngineIoClient.Upgraded: {args.Value}");
            client.EngineIoClient.ExceptionOccurred += (_, args) => Console.WriteLine($"EngineIoClient.ExceptionOccurred: {args.Value}");
            client.EngineIoClient.MessageReceived += (_, args) => Console.WriteLine($"EngineIoClient.MessageReceived: {args.Value}");
            client.EngineIoClient.NoopReceived += (_, args) => Console.WriteLine($"EngineIoClient.NoopReceived: {args.Value}");
            client.EngineIoClient.PingReceived += (_, args) => Console.WriteLine($"EngineIoClient.PingReceived: {args.Value}");
            client.EngineIoClient.PongReceived += (_, args) => Console.WriteLine($"EngineIoClient.PongReceived: {args.Value}");
            client.EngineIoClient.PingSent += (_, args) => Console.WriteLine($"EngineIoClient.PingSent: {args.Value}");

            client.EngineIoClient.WebSocketClient.Connected += (_, _) => Console.WriteLine("WebSocketClient.Connected");
            client.EngineIoClient.WebSocketClient.Disconnected += (_, args) => Console.WriteLine($"WebSocketClient.Disconnected. Reason: {args.Reason}, Status: {args.Status:G}");
            client.EngineIoClient.WebSocketClient.TextReceived += (_, args) => Console.WriteLine($"WebSocketClient.TextReceived: {args.Value}");
            client.EngineIoClient.WebSocketClient.ExceptionOccurred += (_, args) => Console.WriteLine($"WebSocketClient.ExceptionOccurred: {args.Value}");
            client.EngineIoClient.WebSocketClient.BytesReceived += (_, args) => Console.WriteLine($"WebSocketClient.BytesReceived: {args.Value.Count}");
        }

        public class ChatMessage
        {
            public string? Username { get; set; }
            public string? Message { get; set; }
            public long NumUsers { get; set; }
        }

        public static async Task ConnectToChatBaseTestAsync(string url, CancellationToken cancellationToken = default)
        {
            await BaseTestAsync(async client =>
            {
                client.On("login", () =>
                {
                    Console.WriteLine("You are logged in.");
                });
                client.On("login", json =>
                {
                    Console.WriteLine($"You are logged in. Json: \"{json}\"");
                });
                client.On<ChatMessage>("login", message =>
                {
                    Console.WriteLine($"You are logged in. Total number of users: {message.NumUsers}");
                });
                client.On<ChatMessage>("user joined", message =>
                {
                    Console.WriteLine($"User joined: {message.Username}. Total number of users: {message.NumUsers}");
                });
                client.On<ChatMessage>("user left", message =>
                {
                    Console.WriteLine($"User left: {message.Username}. Total number of users: {message.NumUsers}");
                });
                client.On<ChatMessage>("typing", message =>
                {
                    Console.WriteLine($"User typing: {message.Username}");
                });
                client.On<ChatMessage>("stop typing", message =>
                {
                    Console.WriteLine($"User stop typing: {message.Username}");
                });
                client.On<ChatMessage>("new message", message =>
                {
                    Console.WriteLine($"New message from user \"{message.Username}\": {message.Message}");
                });

                await client.ConnectAsync(new Uri(url), cancellationToken);

                var args = await client.WaitEventOrErrorAsync(async () =>
                {
                    await client.Emit("add user", "C# H.Socket.IO Test User", cancellationToken: cancellationToken);
                }, cancellationToken);
                switch (args)
                {
                    case SocketIoEventEventArgs eventArgs:
                        Console.WriteLine($"WaitEventOrErrorAsync: Event received: {eventArgs}");
                        break;

                    case SocketIoErrorEventArgs errorArgs:
                        Assert.Fail($"WaitEventOrErrorAsync: Error received after add user: {errorArgs}");
                        break;

                    case null:
                        Assert.Fail("WaitEventOrErrorAsync: No event received after add user");
                        break;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);

                await client.Emit("typing", cancellationToken: cancellationToken);
                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
                await client.Emit("new message", "hello", cancellationToken: cancellationToken);
                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
                await client.Emit("stop typing", cancellationToken: cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

                await client.DisconnectAsync(cancellationToken);
            }, cancellationToken, nameof(SocketIoClient.EventReceived));
        }
    }
}
