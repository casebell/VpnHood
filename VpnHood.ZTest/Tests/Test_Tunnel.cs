﻿using System;
using System.Net.Http;
using System.Net.NetworkInformation;
using VpnHood.Server;
using VpnHood.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net.Sockets;
using System.Threading;

namespace VpnHood.Test
{

    [TestClass]
    public class Test_Tunnel
    {
        public int ReconnectDelay { get; private set; }

        private static void Test_Icmp(Ping ping = null)
        {
            var pingReply = TestHelper.SendPing(ping);
            Assert.AreEqual(IPStatus.Success, pingReply.Status);
        }

        private static void Test_Udp(UdpClient udpClient = null)
        {
            var hostEntry = TestHelper.SendUdp(udpClient);
            Assert.IsNotNull(hostEntry);
            Assert.IsTrue(hostEntry.AddressList.Length > 0);
        }

        [TestMethod]
        public void Proxy_tunnel()
        {
            // Create Server
            using var server = TestHelper.CreateServer();
            var token = TestHelper.CreateAccessItem(server).Token;
            Assert.AreEqual(ServerState.Started, server.State);

            // Create Client
            using var client = TestHelper.CreateClient(token: token);
            Assert.AreEqual(ClientState.Connected, client.State);

            // Get session
            var serverSession = server.SessionManager.FindSessionByClientId(client.ClientId);
            Assert.IsNotNull(serverSession, "Could not find session in server!");

            var oldClientSentByteCount = client.SentByteCount;
            var oldClientReceivedByteCount = client.ReceivedByteCount;
            var oldServerSentByteCount = serverSession.Tunnel.SentByteCount;
            var oldServerReceivedByteCount = serverSession.Tunnel.ReceivedByteCount;

            // ************
            // *** TEST ***: TCP (TLS) by quad9
            using var httpClient = new HttpClient();
            var result = httpClient.GetStringAsync("https://www.quad9.net/").Result;
            Assert.IsTrue(result.Length > 2);

            // check there is send data
            Assert.IsTrue(client.SentByteCount > oldClientSentByteCount + 100, "Not enough data has been sent through the client!");
            Assert.IsTrue(client.ReceivedByteCount > oldClientReceivedByteCount + 2000, "Not enough data has been sent through the client!");
            Assert.IsTrue(serverSession.Tunnel.SentByteCount > oldServerSentByteCount + 2000, "Not enough data has been sent through the client!");
            Assert.IsTrue(serverSession.Tunnel.ReceivedByteCount > oldServerReceivedByteCount + 100, "Not enough data has been sent through the client!");

            // ************
            // *** TEST ***: UDP
            oldClientSentByteCount = client.SentByteCount;
            oldClientReceivedByteCount = client.ReceivedByteCount;
            oldServerSentByteCount = serverSession.Tunnel.SentByteCount;
            oldServerReceivedByteCount = serverSession.Tunnel.ReceivedByteCount;

            Test_Udp();

            Assert.IsTrue(client.SentByteCount > oldClientSentByteCount + 10, "Not enough data has been sent through the client!");
            Assert.IsTrue(client.ReceivedByteCount > oldClientReceivedByteCount + 10, "Not enough data has been sent through the client!");
            Assert.IsTrue(serverSession.Tunnel.SentByteCount > oldServerSentByteCount + 10, "Not enough data has been sent through the client!");
            Assert.IsTrue(serverSession.Tunnel.ReceivedByteCount > oldServerReceivedByteCount + 10, "Not enough data has been sent through the client!");

            // ************
            // *** TEST ***: Icmp
            oldClientSentByteCount = client.SentByteCount;
            oldClientReceivedByteCount = client.ReceivedByteCount;
            oldServerSentByteCount = serverSession.Tunnel.SentByteCount;
            oldServerReceivedByteCount = serverSession.Tunnel.ReceivedByteCount;

            Test_Icmp();

            Assert.IsTrue(client.SentByteCount > oldClientSentByteCount + 100, "Not enough data has been sent through the client!");
            Assert.IsTrue(client.ReceivedByteCount > oldClientReceivedByteCount + 100, "Not enough data has been sent through the client!");
            Assert.IsTrue(serverSession.Tunnel.SentByteCount > oldServerSentByteCount + 100, "Not enough data has been sent through the client!");
            Assert.IsTrue(serverSession.Tunnel.ReceivedByteCount > oldServerReceivedByteCount + 100, "Not enough data has been sent through the client!");
        }

        [TestMethod]
        public void Client_must_despose_after_device_closed()
        {
            using var server = TestHelper.CreateServer();
            var token = TestHelper.CreateAccessItem(server).Token;

            using var packetCapture = TestHelper.CreatePacketCapture();
            using var client = TestHelper.CreateClient(token: token, packetCapture: packetCapture);

            packetCapture.StopCapture();
            Assert.AreEqual(ClientState.Disposed, client.State);
        }

        [TestMethod]
        public void Datagram_channel_after_client_reconnection()
        {
            //create a shared udp client among connection
            // make sure using same local port to test Nat properly
            using var udpClient = new UdpClient();
            using var ping = new Ping();

            using var server = TestHelper.CreateServer();
            var token = TestHelper.CreateAccessItem(server).Token;

            // create client
            using var client1 = TestHelper.CreateClient(token: token);

            // test Icmp & Udp
            Test_Icmp(ping);
            Test_Udp(udpClient);

            // create client
            using var client2 = TestHelper.CreateClient(token: token);

            // test Icmp & Udp
            Test_Icmp(ping);
            Test_Udp(udpClient);
        }


        [TestMethod]
        public void Reconnect()
        {
            using var httpClient = new HttpClient();

            // creae server
            using var server = TestHelper.CreateServer();
            var token = TestHelper.CreateAccessItem(server).Token;

            // ************
            // *** TEST ***: Reconnect after disconnection (1st time)
            using var client = TestHelper.CreateClient(token: token, options: new() { MaxReconnectCount = 1, ReconnectDelay = 0 });
            Assert.AreEqual(ClientState.Connected, client.State); // checkpoint
            server.SessionManager.FindSessionByClientId(client.ClientId).Dispose();

            try { httpClient.GetStringAsync("https://www.quad9.net/").Wait(); } catch { }
            TestHelper.WaitForClientState(client, ClientState.Connected);
            Assert.AreEqual(ClientState.Connected, client.State);
            Assert.AreEqual(1, client.ReconnectCount);

            // ************
            // *** TEST ***: dispose after second try (2st time)
            Assert.AreEqual(ClientState.Connected, client.State); // checkpoint
            server.SessionManager.FindSessionByClientId(client.ClientId).Dispose();

            try { httpClient.GetStringAsync("https://www.quad9.net/").Wait(); } catch { }
            TestHelper.WaitForClientState(client, ClientState.Disposed);
            Assert.AreEqual(ClientState.Disposed, client.State);
            Assert.AreEqual(1, client.ReconnectCount);
        }

        [TestMethod]
        public void Reconnect_is_not_expected_for_first_attempt()
        {
            // creae server
            using var server = TestHelper.CreateServer();
            var token = TestHelper.CreateAccessItem(server).Token;

            token.TokenId = Guid.NewGuid();
            using VpnHoodClient client = TestHelper.CreateClient(token: token, autoConnect: false, options: new() { MaxReconnectCount = 3, ReconnectDelay = 0 });
            try
            {
                client.Connect().Wait();
                Assert.Fail("Exception expected! Should not reconnect");
            }
            catch { }
            Assert.AreEqual(0, client.ReconnectCount, "Reconnect is not expected for first try");
        }

        [TestMethod]
        public void Disconnect_if_session_expired()
        {
            // create server
            using var server = TestHelper.CreateServer();
            var token = TestHelper.CreateAccessItem(server).Token;
            var accessServer = server.AccessServer;

            // connect
            using VpnHoodClient client = TestHelper.CreateClient(token: token, options: new() { MaxReconnectCount = 0 });
            Assert.AreEqual(ClientState.Connected, client.State);

            // restart server
            server.Dispose();
            using var server2 = TestHelper.CreateServer(accessServer, server.TcpHostEndPoint);
            try { TestHelper.SendHttpGet(); }
            catch { }

            TestHelper.WaitForClientState(client, ClientState.Disposed, 5000);
            Assert.AreEqual(ClientState.Disposed, client.State);
        }
    }
}
