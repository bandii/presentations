using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PactNet;
using PactNet.Infrastructure.Outputters;
using PactNet.Output.Xunit;
using PactNet.Verifier;
using Provider.Orders;
using Xunit;
using Xunit.Abstractions;

namespace Provider.Tests
{
    public class ProviderTests : IDisposable
    {
        private static readonly Uri ProviderUri = new("http://localhost:5000");

        private static readonly JsonSerializerSettings Options = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        private readonly IHost server;
        private readonly PactVerifier verifier;

        public ProviderTests(ITestOutputHelper output)
        {
            /* Note: You can't use the Microsoft.AspNetCore.Mvc.Testing library to host your API for provider tests.
             If your tests are using TestServer or WebApplicationFactory then these are running the API
             with a special in-memory test server instead of running on a real TCP socket.
            */
            this.server = Host.CreateDefaultBuilder()
                              .ConfigureWebHostDefaults(webBuilder =>
                              {
                                  webBuilder.UseUrls(ProviderUri.ToString());
                                  webBuilder.UseStartup<TestStartup>();
                              })
                              .Build();

            this.server.Start();
            
            this.verifier = new PactVerifier("Orders API", new PactVerifierConfig
                                                           {
                                                               LogLevel = PactLogLevel.Debug,
                                                               Outputters = new List<IOutput>
                                                                            {
                                                                                new XunitOutput(output)
                                                                            }
                                                           });
        }

        public void Dispose()
        {
            this.server.Dispose();
            this.verifier.Dispose();
        }

        [Fact]
        public void Verify_API()
        {
            string pactPath = Path.Combine("..",
                                           "..",
                                           "..",
                                           "..",
                                           "Consumer.Tests",
                                           "pacts",
                                           "Fulfilment API-Orders API.json");

            this.verifier
                .WithHttpEndpoint(ProviderUri)
                .WithFileSource(new FileInfo(pactPath))
                .WithProviderStateUrl(new Uri(ProviderUri, "/provider-states"))
                .Verify();
        }

        [Fact]
        public void Verify_Messaging()
        {
            string pactPath = Path.Combine("..",
                                           "..",
                                           "..",
                                           "..",
                                           "Consumer.Tests",
                                           "pacts",
                                           "Fulfilment Messaging-Orders messaging.json");

            this.verifier
                .WithHttpEndpoint(ProviderUri)
                .WithMessages(scenarios =>
                {
                    // You need to use the same description as the consumer's..
                    scenarios.Add("an event indicating that an order has been created",
                                  () => new OrderCreatedEvent(2));
                }, Options)
                .WithFileSource(new FileInfo(pactPath))
                .WithProviderStateUrl(new Uri(ProviderUri, "/provider-states"))
                .Verify();
        }
    }
}
