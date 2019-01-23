// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MobileServices.TestFramework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Test
{
    [Tag("push")]
    public class PushFunctional : FunctionalTestBase
    {
        readonly IPushTestUtility pushTestUtility;

        public PushFunctional()
        {
            this.pushTestUtility = TestPlatform.Instance.PushTestUtility;
        }

        [AsyncTestMethod]
        public async Task InitialDeleteRegistrationsAsync()
        {
            string registrationId = this.pushTestUtility.GetPushHandle();
            Dictionary<string, string> channelUriParam = new Dictionary<string, string>()
            {
                {"channelUri", registrationId}
            };
            await this.GetClient().InvokeApiAsync("deleteRegistrationsForChannel", HttpMethod.Delete, channelUriParam);
        }

        [AsyncTestMethod]
        public async Task RegisterAsync()
        {
            string registrationId = this.pushTestUtility.GetPushHandle();
            var push = this.GetClient().GetPush();
            await push.RegisterAsync(registrationId);
            Dictionary<string, string> parameters = new Dictionary<string, string>()
            {
                {"channelUri", registrationId}
            };
            await VerifyRegistration(parameters, push);
        }

        [AsyncTestMethod]
        public async Task LoginRegisterAsync()
        {
            MobileServiceUser user = await Utilities.GetDummyUser(this.GetClient());
            this.GetClient().CurrentUser = user;
            string registrationId = this.pushTestUtility.GetPushHandle();
            var push = this.GetClient().GetPush();
            await push.RegisterAsync(registrationId);

            Dictionary<string, string> parameters = new Dictionary<string, string>()
            {
                {"channelUri", registrationId}
            };
            await VerifyRegistration(parameters, push);
        }

        [AsyncTestMethod]
        public async Task UnregisterAsync()
        {
            var push = this.GetClient().GetPush();
            await push.UnregisterAsync();
            await this.GetClient().InvokeApiAsync("verifyUnregisterInstallationResult", HttpMethod.Get, null);
        }

        [AsyncTestMethod]
        public async Task RegisterAsyncTemplatesAndOverride()
        {
            string registrationId = this.pushTestUtility.GetPushHandle();
            JObject templates = GetTemplates("bar");
            JObject expectedTemplates = GetTemplates("testGcmTemplate");
            var push = this.GetClient().GetPush();
            try
            {
                await push.RegisterAsync(registrationId, templates);
                var parameters = new Dictionary<string, string>()
                {
                    {"channelUri", registrationId},
                    {"templates", JsonConvert.SerializeObject(expectedTemplates)}
                };
                await this.GetClient().InvokeApiAsync("verifyRegisterInstallationResult", HttpMethod.Get, parameters);

                await push.RegisterAsync(registrationId);
                parameters = new Dictionary<string, string>()
                {
                    {"channelUri", registrationId},
                };
                await this.GetClient().InvokeApiAsync("verifyRegisterInstallationResult", HttpMethod.Get, parameters);
            }
            finally
            {
                push.UnregisterAsync().Wait();
            }

            await this.GetClient().LogoutAsync();
        }

        [AsyncTestMethod]
        public async Task RegisterAsyncTemplatesWithTemplateBodyJson()
        {
            string registrationId = this.pushTestUtility.GetPushHandle();
            JObject templates = GetTemplates("bar", true);
            JObject expectedTemplates = GetTemplates("testGcmTemplate");
            var push = this.GetClient().GetPush();
            try
            {
                await push.RegisterAsync(registrationId, templates);
                var parameters = new Dictionary<string, string>()
                {
                    {"channelUri", registrationId},
                    {"templates", JsonConvert.SerializeObject(expectedTemplates)}
                };
                await this.GetClient().InvokeApiAsync("verifyRegisterInstallationResult", HttpMethod.Get, parameters);
            }
            finally
            {
                push.UnregisterAsync().Wait();
            }
        }

        [AsyncTestMethod]
        public async Task PushGcmTest()
        {
            var push = this.GetClient().GetPush();
            var pushTestUtility = this.pushTestUtility as PushTestUtility;
            string registrationId = pushTestUtility.GetPushHandle();

            //Build push payload
            JObject body = new JObject();
            body["method"] = "send";
            body["type"] = "gcm";
            body["payload"] = "{\"data\":{\"message\":\"Notification Hub test notification\"}}";
            body["token"] = "dummy";

            try
            {
                // Register for Push
                await push.RegisterAsync(registrationId);

                // Invoke API to send push & Wait for push receive
                var send = this.GetClient().InvokeApiAsync("push", body);
                var receive = this.WaitForPush(TimeSpan.FromSeconds(20));
                Task.WaitAll(new Task[] { send, receive }, TimeSpan.FromSeconds(25));

                if (receive.Status != TaskStatus.RanToCompletion)
                {
                    Assert.Fail("Error, push not received on the timeout allowed");
                }
                else
                {
                    Log("Push notification received:");
                    Assert.AreEqual("Notification Hub test notification", receive.Result);
                }
            }
            finally
            {
                push.UnregisterAsync().Wait();
            }
        }

        private static JObject GetTemplates(string tag, bool templateBodyJson = false)
        {
            JObject msg = new JObject();
            msg["msg"] = "$(message)";
            JObject data = new JObject();
            data["data"] = msg;
            JObject templateBody = new JObject();
            templateBody["body"] = data.ToString();
            if (templateBodyJson)
            {
                templateBody["body"] = data;
            }
            if (tag != null)
            {
                JArray tags = new JArray() { tag };
                templateBody["tags"] = tags;
            }
            JObject templates = new JObject();
            templates["testGcmTemplate"] = templateBody;
            return templates;
        }

        private async Task VerifyRegistration(Dictionary<string, string> parameters, Push push)
        {
            try
            {
                await this.GetClient().InvokeApiAsync("verifyRegisterInstallationResult", HttpMethod.Get, parameters);
            }
            finally
            {
                push.UnregisterAsync().Wait();
            }

            await this.GetClient().LogoutAsync();
        }

        private async Task<string> WaitForPush(TimeSpan maximumWait)
        {
            DateTime start = DateTime.UtcNow;
            while (DateTime.UtcNow.Subtract(start) < maximumWait)
            {
                if (GcmService.PushesReceived.Count > 0)
                {
                    return GcmService.PushesReceived.Dequeue();
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }

            return null;
        }
    }
}