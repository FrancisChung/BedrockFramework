using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bedrock.Framework;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MQTTnet.Adapter;
using MQTTnet.AspNetCore;
using MQTTnet.Packets;
using MQTTnet.Protocol;

namespace ServerApplication
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder()
                           .ConfigureServices((context, services) =>
                           {
                               // Frameworks
                               services.AddSignalRCore();
                           })
                           .ConfigureServer(options =>
                           {
                               options.ListenWebSocket(
                                   new Uri("https://localhost:5003"),
                                   builder => builder.UseConnectionHandler<EchoServerApplication>());

                               options.ListenHttp2(
                                   new Uri("https://localhost:5004"),
                                   builder => builder.UseConnectionHandler<EchoServerApplication>());

                               options.ListenSocket(
                                   new IPEndPoint(IPAddress.Loopback, 5005),
                                   builder => builder.UseConnectionHandler<EchoServerApplication>());

                               // This is a transport based on the AzureSignalR protocol, it gives you a full duplex mutliplexed connection over the 
                               // the internet
                               // Put your azure SignalR connection string in configuration

                               //var connectionString = context.Configuration["AzureSignalR:ConnectionString"];
                               //options.ListenAzureSignalR(connectionString, "myhub",
                               //    builder => builder.UseConnectionHandler<EchoServerApplication>());
                               // SignalR on TCP
                               options.Listen(IPAddress.Loopback, 5006, builder => builder.UseHub<Chat>());

                               // HTTP/1.1 server
                               options.Listen(IPAddress.Loopback, 5007, builder => builder.UseHttpServer(new HttpApplication()));

                               // MQTT application
                               options.Listen(IPAddress.Loopback, 5008, builder => builder.UseConnectionHandler<MqttApplication>());
                           })
                           .Build();

            await host.RunAsync();
        }

        public class HttpApplication : IHttpApplication
        {
            public async Task ProcessRequests(IAsyncEnumerable<IHttpContext> requests)
            {
                var responseData = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 11\r\n\r\nHello ");

                await foreach (var context in requests)
                {
                    await context.Output.WriteAsync(responseData);

                    await Task.Yield();

                    await context.Output.WriteAsync(Encoding.ASCII.GetBytes("World"));
                }
            }
        }

        public class MqttApplication : MqttConnectionHandler
        {
            public MqttApplication()
            {
                ClientHandler = OnClientConnectedAsync;
            }

            private async Task OnClientConnectedAsync(IMqttChannelAdapter adapter)
            {
                while (true)
                {
                    var packet = await adapter.ReceivePacketAsync(Timeout.InfiniteTimeSpan, default);

                    switch (packet)
                    {
                        case MqttConnectPacket connectPacket:
                            await adapter.SendPacketAsync(new MqttConnAckPacket
                            {
                                ReturnCode = MqttConnectReturnCode.ConnectionAccepted,
                                ReasonCode = MqttConnectReasonCode.Success,
                                IsSessionPresent = false
                            }, Timeout.InfiniteTimeSpan,
                            default);
                            break;
                        case MqttDisconnectPacket disconnectPacket:
                            break;
                        case MqttAuthPacket mqttAuthPacket:
                            break;
                        case MqttConnAckPacket connAckPacket:
                            break;
                        case MqttPublishPacket mqttPublishPacket:
                            break;
                        case MqttSubscribePacket mqttSubscribePacket:
                            var ack = new MqttSubAckPacket
                            {
                                PacketIdentifier = mqttSubscribePacket.PacketIdentifier,
                                ReturnCodes = new List<MqttSubscribeReturnCode> { MqttSubscribeReturnCode.SuccessMaximumQoS0 }
                            };
                            ack.ReasonCodes.Add(MqttSubscribeReasonCode.GrantedQoS0);

                            await adapter.SendPacketAsync(ack, Timeout.InfiniteTimeSpan, default);
                            break;
                        default:
                            break;
                    }
                }
            }
        }
    }
}