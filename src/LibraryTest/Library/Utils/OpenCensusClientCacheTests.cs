
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Microsoft.LocalForwarder.Library.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Opencensus.Proto.Agent.Common.V1;

namespace Microsoft.LocalForwarder.LibraryTest.Library.Utils
{
    [TestClass]
    public class OpenCensusClientCacheTests
    {
        [TestMethod]
        public void OpenCensusClientCache_AddsAndGets()
        {
            var cache = new OpenCensusClientCache<string, Node>();

            var node0 = CreateNode(0);
            var node1 = CreateNode(1);
            var node2 = CreateNode(2);
            cache.AddOrUpdate("client0", node0);
            cache.AddOrUpdate("client1", node1);
            cache.AddOrUpdate("client2", node2);

            Assert.IsTrue(cache.TryGet("client0", out var actualNode0));
            Assert.AreEqual(node0, actualNode0);

            Assert.IsTrue(cache.TryGet("client1", out var actualNode1));
            Assert.AreEqual(node1, actualNode1);

            Assert.IsTrue(cache.TryGet("client2", out var actualNode2));
            Assert.AreEqual(node2, actualNode2);
        }

        [TestMethod]
        public void OpenCensusClientCache_AddsAndUpdates()
        {
            var cache = new OpenCensusClientCache<string, Node>();

            var node0 = CreateNode(0);
            var node1 = CreateNode(1);

            cache.AddOrUpdate("client0", node0);
            cache.AddOrUpdate("client1", node1);
            cache.AddOrUpdate("client0", node1);

            Assert.IsTrue(cache.TryGet("client0", out var actualNode0));
            Assert.AreEqual(node1, actualNode0);

            Assert.IsTrue(cache.TryGet("client1", out var actualNode1));
            Assert.AreEqual(node1, actualNode1);
        }

        [TestMethod]
        public void OpenCensusClientCache_GetNotExisting()
        {
            var cache = new OpenCensusClientCache<string, Node>();

            Assert.IsFalse(cache.TryGet("client0", out var node));
            Assert.IsNull(node);
        }

        [TestMethod]
        public async Task OpenCensusClientCache_HandlesAddOverflow()
        {
            var cache = new OpenCensusClientCache<string, Node>(10, TimeSpan.FromSeconds(1));

            var nodes = Enumerable.Range(0, 10).Select(i => (i.ToString(), CreateNode((uint)i))).ToArray();
            foreach (var node in nodes)
            {
                cache.AddOrUpdate(node.Item1, node.Item2);
            }


            await Task.Delay(2000);

            var newNode = CreateNode(42);
            cache.AddOrUpdate("new", newNode);

            foreach (var node in nodes)
            {
                Assert.IsFalse(cache.TryGet(node.Item1, out _));
            }

            Assert.IsTrue(cache.TryGet("new", out var actualNewNode));
            Assert.AreEqual(newNode, actualNewNode);
        }

        [TestMethod]
        public async Task OpenCensusClientCache_DoesNotRemoveItemsBeforeOverflow()
        {
            var cache = new OpenCensusClientCache<string, Node>(10, TimeSpan.FromSeconds(1));

            var nodes = Enumerable.Range(0, 10).Select(i => (i.ToString(), CreateNode((uint)i))).ToArray();
            foreach (var node in nodes)
            {
                cache.AddOrUpdate(node.Item1, node.Item2);
            }

            await Task.Delay(2000);

            foreach (var node in nodes)
            {
                Assert.IsTrue(cache.TryGet(node.Item1, out _));
            }
        }

        [TestMethod]
        public async Task OpenCensusClientCache_TryGetUpdatesTimestamp()
        {
            var cache = new OpenCensusClientCache<string, Node>(10, TimeSpan.FromSeconds(1));

            var nodes = Enumerable.Range(0, 10).Select(i => (i.ToString(), CreateNode((uint)i))).ToArray();
            foreach (var node in nodes)
            {
                cache.AddOrUpdate(node.Item1, node.Item2);
            }


            await Task.Delay(2000);
            Assert.IsTrue(cache.TryGet(nodes[0].Item1, out _));

            cache.AddOrUpdate("new", CreateNode(42));

            for (int i = 1; i < nodes.Length; i ++)
            {
                // spin a bit to avoid flakiness
                Assert.IsFalse(cache.TryGet(nodes[i].Item1, out _));
            }
            Assert.IsTrue(cache.TryGet("new", out _));
        }

        private Node CreateNode(uint pid)
        {
            return new Node
            {
                Identifier = new ProcessIdentifier
                {
                    Pid = pid,
                    HostName = "host",
                    StartTimestamp = Timestamp.FromDateTime(DateTime.UtcNow)
                },
                LibraryInfo = new LibraryInfo
                {
                    Language = LibraryInfo.Types.Language.Cpp,
                    CoreLibraryVersion = "0"
                },
                ServiceInfo = new ServiceInfo
                {
                    Name = "tests"
                }
            };
        }
    }
}
