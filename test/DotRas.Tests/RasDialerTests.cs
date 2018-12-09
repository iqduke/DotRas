﻿using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DotRas.Internal.Abstractions.Primitives;
using Moq;
using NUnit.Framework;
using DotRas.Internal.Abstractions.Services;

namespace DotRas.Tests
{
    [TestFixture]
    public class RasDialerTests
    {
        [Test]
        public void CanInstantiateTheDialer()
        {
            var target = new RasDialer();
            Assert.IsNotNull(target);
        }

        [Test]
        public void DialTheConnectionSynchronouslyWithAnExistingCancellationToken()
        {
            var connection = new Mock<RasConnection>();
            var cancellationToken = new CancellationToken();

            var api = new Mock<IRasDial>();
            api.Setup(o => o.DialAsync(It.IsAny<RasDialContext>())).Returns<RasDialContext>(c =>
            {
                Assert.AreEqual(cancellationToken, c.CancellationToken);

                return Task.FromResult(connection.Object);
            }).Verifiable();

            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(o => o.VerifyFileExists("PATH")).Returns(true);

            var target = new RasDialer(api.Object, fileSystem.Object)
            {
                EntryName = "ENTRY",
                PhoneBookPath = "PATH"
            };

            var result = target.Dial(cancellationToken);

            Assert.IsNotNull(result);
            api.Verify();
        }

        [Test]
        public void DialsTheConnectionSynchronouslyWithoutACancellationToken()
        {
            var connection = new Mock<RasConnection>();

            var api = new Mock<IRasDial>();
            api.Setup(o => o.DialAsync(It.IsAny<RasDialContext>())).Returns<RasDialContext>(c =>
            {
                Assert.AreEqual(CancellationToken.None, c.CancellationToken);
                
                return Task.FromResult(connection.Object);
            }).Verifiable();

            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(o => o.VerifyFileExists("PATH")).Returns(true);

            var target = new RasDialer(api.Object, fileSystem.Object)
            {
                EntryName = "ENTRY",
                PhoneBookPath = "PATH"
            };

            var result = target.Dial();

            Assert.IsNotNull(result);
            api.Verify();
        }

        [Test]
        public async Task DialsTheConnectionAsyncWithoutACancellationToken()
        {
            var result = new Mock<RasConnection>();

            var api = new Mock<IRasDial>();
            api.Setup(o => o.DialAsync(It.IsAny<RasDialContext>())).Returns<RasDialContext>(c =>
            {
                Assert.AreEqual(CancellationToken.None, c.CancellationToken);

                return Task.FromResult(result.Object);
            }).Verifiable();

            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(o => o.VerifyFileExists("PATH")).Returns(true);

            var target = new RasDialer(api.Object, fileSystem.Object)
            {
                EntryName = "ENTRY",
                PhoneBookPath = "PATH"
            };

            await target.DialAsync();

            api.Verify();
        }

        [Test]
        public async Task BuildsTheContextCorrectly()
        {
            var cancellationToken = CancellationToken.None;
            var credentials = new NetworkCredential("USERNAME", "PASSWORD", "DOMAIN");
            var result = new Mock<RasConnection>();

            var api = new Mock<IRasDial>();
            api.Setup(o => o.DialAsync(It.IsAny<RasDialContext>())).Returns<RasDialContext>(c =>
            {
                Assert.AreEqual(cancellationToken, c.CancellationToken);
                Assert.AreEqual(credentials, c.Credentials);
                Assert.AreEqual("ENTRY", c.EntryName);
                Assert.AreEqual("PATH", c.PhoneBookPath);

                return Task.FromResult(result.Object);
            });

            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(o => o.VerifyFileExists("PATH")).Returns(true);

            var target = new RasDialer(api.Object, fileSystem.Object)
            {
                Credentials = credentials,
                EntryName = "ENTRY",
                PhoneBookPath = "PATH"
            };

            var connection = await target.DialAsync(cancellationToken);
            Assert.AreSame(result.Object, connection);
        }

        [Test]
        public async Task ThrowsAnExceptionWhenTheEventArgsIsNull()
        {
            var executed = false;
            var result = new Mock<RasConnection>();

            var api = new Mock<IRasDial>();
            api.Setup(o => o.DialAsync(It.IsAny<RasDialContext>())).Returns<RasDialContext>(c =>
            {
                Assert.IsNotNull(c.OnStateChangedCallback);
                Assert.Throws<ArgumentNullException>(() => c.OnStateChangedCallback(null));

                executed = true;
                return Task.FromResult(result.Object);
            });

            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(o => o.VerifyFileExists("PATH")).Returns(true);

            var target = new RasDialer(api.Object, fileSystem.Object)
            {
                EntryName = "ENTRY",
                PhoneBookPath = "PATH"
            };

            await target.DialAsync();

            Assert.True(executed);
        }

        [Test]
        public async Task RaisesTheEventFromTheOnStateChangedCallback()
        {
            var e = new StateChangedEventArgs(RasConnectionState.OpenPort);
            var result = new Mock<RasConnection>();

            var api = new Mock<IRasDial>();
            api.Setup(o => o.DialAsync(It.IsAny<RasDialContext>())).Returns<RasDialContext>(c =>
            {
                Assert.IsNotNull(c.OnStateChangedCallback);
                c.OnStateChangedCallback(e);                

                return Task.FromResult(result.Object);
            });

            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(o => o.VerifyFileExists("PATH")).Returns(true);

            var raised = false;

            var target = new RasDialer(api.Object, fileSystem.Object)
            {
                EntryName = "ENTRY",
                PhoneBookPath = "PATH"
            };

            target.StateChanged += (sender, args) =>
            {
                Assert.AreEqual(e, args);
                raised = true;
            };

            await target.DialAsync();

            Assert.True(raised);
        }

        [Test]
        public void DisposesTheApiAsExpected()
        {
            var api = new Mock<IRasDial>();
            var disposable = api.As<IDisposable>();

            var fileSystem = new Mock<IFileSystem>();

            var target = new RasDialer(api.Object, fileSystem.Object);
            target.Dispose();

            disposable.Verify(o => o.Dispose(), Times.Once);
        }

        [Test]
        public void ThrowsAnExceptionWhenTheEntryNameHasNotBeenSet()
        {
            var api = new Mock<IRasDial>();           
            var fileSystem = new Mock<IFileSystem>();

            var target = new RasDialer(api.Object, fileSystem.Object)
            {
                EntryName = null,
                PhoneBookPath = "PATH"
            };

            Assert.Throws<RasDialerConfigurationException>(() => target.Dial());
        }

        [Test]
        public void ThrowsAnExceptionWhenThePhoneBookPathHasNotBeenSet()
        {
            var api = new Mock<IRasDial>();
            var fileSystem = new Mock<IFileSystem>();

            var target = new RasDialer(api.Object, fileSystem.Object)
            {
                EntryName = "ENTRY",
                PhoneBookPath = null
            };

            Assert.Throws<RasDialerConfigurationException>(() => target.Dial());
        }

        [Test]
        public void ThrowsAnExceptionWhenThePhoneBookPathDoesNotExist()
        {
            var api = new Mock<IRasDial>();
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(o => o.VerifyFileExists("PATH")).Returns(false).Verifiable();

            var target = new RasDialer(api.Object, fileSystem.Object)
            {
                EntryName = "ENTRY",
                PhoneBookPath = "PATH"
            };

            Assert.Throws<RasDialerConfigurationException>(() => target.Dial());

            fileSystem.Verify();
        }
    }
}