using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Foundation;
using Microsoft.WindowsAzure.MobileServices;
using Microsoft.WindowsAzure.MobileServices.TestFramework;
using MonoTouch.Dialog;
using UIKit;

namespace Microsoft.WindowsAzure.Mobile.iOS.Test
{
    class LoginViewController
        : DialogViewController
    {
        public LoginViewController()
            : base(UITableViewStyle.Grouped, null)
        {
            var defaults = NSUserDefaults.StandardUserDefaults;
            string mobileServiceUri = defaults.StringForKey(MobileServiceUriKey);
            string tags = defaults.StringForKey(TagsKey);
            string runId = defaults.StringForKey(RunIdKey);
            string runtimeVersion = defaults.StringForKey(RuntimeVersionKey);

            this.uriEntry = new AccessibleEntryElement(null, "Mobile Service URI", mobileServiceUri, accessibilityId: MobileServiceUriKey);
            this.tagsEntry = new AccessibleEntryElement(null, "Tags", tags, accessibilityId: TagsKey);

            this.runIdEntry = new AccessibleEntryElement(null, "Run Id", runId, accessibilityId: RunIdKey);
            this.runtimeVersionEntry = new AccessibleEntryElement(null, "Runtime version", runtimeVersion, accessibilityId: RuntimeVersionKey);

            Root = new RootElement("C# Client Library Tests") {
                new Section("Login") {
                    this.uriEntry,
                    this.tagsEntry
                },

                new Section("Report Results"){
                    this.runIdEntry,
                    this.runtimeVersionEntry
                },

                new Section {
                   new AccessibleStringElement ("E2E Tests", RunTests, accessibilityId: "RunTests")                
                },

                new Section{
                    new StringElement("Login and Refresh with Microsoft Account", HandleLoginErrorsAsync(LoginAndRefreshWithMicrosoftAccount)),
                    new StringElement("Login with Facebook", HandleLoginErrorsAsync(LoginFacebook)),
                    new StringElement("Login with Twitter", HandleLoginErrorsAsync(LoginTwitter)),
                    new StringElement("Login and Refresh with Google", HandleLoginErrorsAsync(LoginAndRefreshWithGoogle)),
                    new StringElement("Login and Refresh with AAD", HandleLoginErrorsAsync(LoginAndRefreshWithAAD))
                }
            };
        }

        private const string MobileServiceUriKey = "MobileServiceUri";
        private const string TagsKey = "Tags";
        private const string RunIdKey = "RunId";
        private const string RuntimeVersionKey = "RuntimeVersion";

        private const string uriScheme = "zumoe2etestapp";

        private readonly EntryElement uriEntry;
        private readonly EntryElement tagsEntry;
        private readonly EntryElement runIdEntry;
        private readonly EntryElement runtimeVersionEntry;

        private void RunTests()
        {
            var defaults = NSUserDefaults.StandardUserDefaults;
            defaults.SetString(this.uriEntry.Value, MobileServiceUriKey);
            defaults.SetString(this.tagsEntry.Value, TagsKey);
            defaults.SetString(this.runIdEntry.Value, RunIdKey);
            defaults.SetString(this.runtimeVersionEntry.Value, RuntimeVersionKey);

            AppDelegate.Harness.SetAutoConfig(new TestConfig()
            {
                MobileServiceRuntimeUrl = this.uriEntry.Value,
                TagExpression = this.tagsEntry.Value,
                RuntimeVersion = this.runtimeVersionEntry.Value
            });
            AppDelegate.Harness.Settings.ManualMode = string.IsNullOrWhiteSpace(this.runtimeVersionEntry.Value);

            if (!string.IsNullOrEmpty(AppDelegate.Harness.Settings.TagExpression))
            {
                AppDelegate.Harness.Settings.TagExpression += " - notXamarin - notXamarin_iOS";
            }
            else
            {
                AppDelegate.Harness.Settings.TagExpression = "!notXamarin - notXamarin_iOS";
            }

            NavigationController.PushViewController(new HarnessViewController(), true);
        }

        private Action HandleLoginErrorsAsync(Func<Task> login) => async () =>
        {
            try
            {
                await login();
            }
            catch (Exception e)
            {
                var alert = new UIAlertView("Error", e.Message, null, "OK");
                alert.Show();
            }
        };

        private Task LoginFacebook() => Login(MobileServiceAuthenticationProvider.Facebook);

        private Task LoginTwitter() => Login(MobileServiceAuthenticationProvider.Twitter);

        private async Task Login(MobileServiceAuthenticationProvider provider)
        {
            var client = new MobileServiceClient(this.uriEntry.Value);
            AppDelegate.ResumeWithURL = url => url.Scheme == uriScheme && client.ResumeWithURL(url);
            var user = await client.LoginAsync(this, provider, uriScheme);
            var alert = new UIAlertView("Welcome", provider.ToString() + " Login succeeded. Your userId is: " + user.UserId, null, "OK");
            alert.Show();
        }

        private async Task LoginAndRefreshWithMicrosoftAccount()
        {
            var client = new MobileServiceClient(this.uriEntry.Value);
            AppDelegate.ResumeWithURL = url => url.Scheme == uriScheme && client.ResumeWithURL(url);
            MobileServiceUser user = await client.LoginAsync(this, MobileServiceAuthenticationProvider.MicrosoftAccount, uriScheme);
            string authToken = user.MobileServiceAuthenticationToken;

            MobileServiceUser refreshedUser = await client.RefreshUserAsync();

            Assert.AreEqual(user.UserId, refreshedUser.UserId);
            Assert.AreNotEqual(authToken, refreshedUser.MobileServiceAuthenticationToken);

            var alert = new UIAlertView("Welcome", "Microsoft Account Login and Refresh User succeeded. Your userId is: " + user.UserId, null, "OK");
            alert.Show();
        }

        private async Task LoginAndRefreshWithAAD()
        {
            var client = new MobileServiceClient(this.uriEntry.Value);
            AppDelegate.ResumeWithURL = url => url.Scheme == uriScheme && client.ResumeWithURL(url);
            MobileServiceUser user = await client.LoginAsync(this, MobileServiceAuthenticationProvider.WindowsAzureActiveDirectory, uriScheme,
                new Dictionary<string, string>()
                {
                    { "response_type", "code id_token" }
                });
            string authToken = user.MobileServiceAuthenticationToken;

            MobileServiceUser refreshedUser = await client.RefreshUserAsync();

            Assert.AreEqual(user.UserId, refreshedUser.UserId);
            Assert.AreNotEqual(authToken, refreshedUser.MobileServiceAuthenticationToken);

            var alert = new UIAlertView("Welcome", "AAD Login and Refresh User succeeded. Your userId is: " + user.UserId, null, "OK");
            alert.Show();
        }

        private async Task LoginAndRefreshWithGoogle()
        {
            var client = new MobileServiceClient(this.uriEntry.Value);
            AppDelegate.ResumeWithURL = url => url.Scheme == uriScheme && client.ResumeWithURL(url);
            MobileServiceUser user = await client.LoginAsync(this, MobileServiceAuthenticationProvider.Google, uriScheme,
                new Dictionary<string, string>()
                {
                    { "access_type", "offline" }
                });
            string authToken = user.MobileServiceAuthenticationToken;

            MobileServiceUser refreshedUser = await client.RefreshUserAsync();

            Assert.AreEqual(user.UserId, refreshedUser.UserId);
            Assert.AreNotEqual(authToken, refreshedUser.MobileServiceAuthenticationToken);

            var alert = new UIAlertView("Welcome", "Google Login and Refresh User succeeded. Your userId is: " + user.UserId, null, "OK");
            alert.Show();
        }
    }
}