/*
 * Modified MIT License
 *
 * Copyright 2022 OneSignal
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * 1. The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * 2. All copies of substantial portions of the Software may only be used in connection
 * with services provided by OneSignal.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace OneSignalSDK {
    public sealed partial class OneSignalAndroid : OneSignal {
        public override event NotificationWillShowDelegate NotificationWillShow;
        public override event NotificationActionDelegate NotificationOpened;
        public override event InAppMessageLifecycleDelegate InAppMessageWillDisplay;
        public override event InAppMessageLifecycleDelegate InAppMessageDidDisplay;
        public override event InAppMessageLifecycleDelegate InAppMessageWillDismiss;
        public override event InAppMessageLifecycleDelegate InAppMessageDidDismiss;
        public override event InAppMessageActionDelegate InAppMessageTriggeredAction;
        public override event StateChangeDelegate<NotificationPermission> NotificationPermissionChanged;
        public override event StateChangeDelegate<PushSubscriptionState> PushSubscriptionStateChanged;
        public override event StateChangeDelegate<EmailSubscriptionState> EmailSubscriptionStateChanged;
        public override event StateChangeDelegate<SMSSubscriptionState> SMSSubscriptionStateChanged;

        public override NotificationPermission NotificationPermission
            => _stateNotificationPermission(_sdkClass.CallStatic<AndroidJavaObject>("getDeviceState"));

        public override PushSubscriptionState PushSubscriptionState {
            get {
                var deviceStateJO = _sdkClass.CallStatic<AndroidJavaObject>("getDeviceState");
                return new PushSubscriptionState {
                    userId         = deviceStateJO.Call<string>("getUserId"),
                    pushToken      = deviceStateJO.Call<string>("getPushToken"),
                    isSubscribed   = deviceStateJO.Call<bool>("isSubscribed"),
                    isPushDisabled = deviceStateJO.Call<bool>("isPushDisabled"),
                };
            }
        }
        
        public override EmailSubscriptionState EmailSubscriptionState {
            get {
                var deviceStateJO = _sdkClass.CallStatic<AndroidJavaObject>("getDeviceState");
                return new EmailSubscriptionState {
                    emailUserId  = deviceStateJO.Call<string>("getEmailUserId"),
                    emailAddress = deviceStateJO.Call<string>("getEmailAddress"),
                    isSubscribed = deviceStateJO.Call<bool>("isEmailSubscribed"),
                };
            }
        }
        
        public override SMSSubscriptionState SMSSubscriptionState {
            get {
                var deviceStateJO = _sdkClass.CallStatic<AndroidJavaObject>("getDeviceState");
                return new SMSSubscriptionState {
                    smsUserId    = deviceStateJO.Call<string>("getSMSUserId"),
                    smsNumber    = deviceStateJO.Call<string>("getSMSNumber"),
                    isSubscribed = deviceStateJO.Call<bool>("isSMSSubscribed"),
                };
            }
        }

        public override LogLevel LogLevel {
            get => _logLevel;
            set {
                _logLevel = value;
                _sdkClass.CallStatic("setLogLevel", (int) _logLevel, (int) _alertLevel);
            }
        }

        public override LogLevel AlertLevel {
            get => _alertLevel;
            set {
                _alertLevel = value;
                _sdkClass.CallStatic("setLogLevel", (int) _logLevel, (int) _alertLevel);
            }
        }
        
        public override bool PrivacyConsent {
            get => _sdkClass.CallStatic<bool>("userProvidedPrivacyConsent");
            set => _sdkClass.CallStatic("provideUserConsent", value);
        }

        public override bool RequiresPrivacyConsent {
            get => _sdkClass.CallStatic<bool>("requiresUserPrivacyConsent");
            set => _sdkClass.CallStatic("setRequiresUserPrivacyConsent", value);
        }

        public override void SetLaunchURLsInApp(bool launchInApp)
            => SDKDebug.Warn("This feature is only available for iOS.");

        public override void Initialize(string appId) {
            var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var activity    = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            _sdkClass.CallStatic("initWithContext", activity);
                        
            // states
            _sdkClass.CallStatic("addPermissionObserver", new OSPermissionObserver());
            _sdkClass.CallStatic("addSubscriptionObserver", new OSSubscriptionObserver());
            _sdkClass.CallStatic("addEmailSubscriptionObserver", new OSEmailSubscriptionObserver());
            _sdkClass.CallStatic("addSMSSubscriptionObserver", new OSSMSSubscriptionObserver());
            
            // notifications
            _sdkClass.CallStatic("setNotificationWillShowInForegroundHandler", new OSNotificationWillShowInForegroundHandler());
            _sdkClass.CallStatic("setNotificationOpenedHandler", new OSNotificationOpenedHandler());

            // iams
            _sdkClass.CallStatic("setInAppMessageClickHandler", new OSInAppMessageClickHandler());
            
            var wrapperHandler = new AndroidJavaObject(QualifiedIAMLifecycleClass, new OSInAppMessageLifecycleHandler());
            _sdkClass.CallStatic("setInAppMessageLifecycleHandler", wrapperHandler);
            
            _sdkClass.CallStatic("setAppId", appId);

            _completedInit(appId);
        }

        public override async Task<NotificationPermission> PromptForPushNotificationsWithUserResponse() {
            var proxy = new PromptForPushNotificationPermissionResponseHandler();
            _sdkClass.CallStatic("promptForPushNotifications", true, proxy);
            return await proxy ? NotificationPermission.Authorized : NotificationPermission.Denied;
        }

        public override void ClearOneSignalNotifications()
            => _sdkClass.CallStatic("clearOneSignalNotifications");

        public override bool PushEnabled {
            get {
                var deviceStateJO = _sdkClass.CallStatic<AndroidJavaObject>("getDeviceState");
                return !deviceStateJO.Call<bool>("isPushDisabled");
            }
            set => _sdkClass.CallStatic("disablePush", !value);
        }

        public override async Task<Dictionary<string, object>> PostNotification(Dictionary<string, object> options) {
            var proxy = new PostNotificationResponseHandler();
            _sdkClass.CallStatic("postNotification", options.ToJSONObject(), proxy);
            return await proxy;
        }

        public override void SetTrigger(string key, string value)
            => _sdkClass.CallStatic("addTrigger", key, value);

        public override void SetTriggers(Dictionary<string, string> triggers)
            => _sdkClass.CallStatic("addTriggers", triggers.ToMap());

        public override void RemoveTrigger(string key)
            => _sdkClass.CallStatic("removeTriggerForKey", key);

        public override void RemoveTriggers(params string[] keys)
            => _sdkClass.CallStatic("removeTriggersForKeys", Json.Serialize(keys));

        public override string GetTrigger(string key) {
            var triggerVal = _sdkClass.CallStatic<AndroidJavaObject>("getTriggerValueForKey", key);
            return triggerVal.Call<string>("toString");
        }

        public override Dictionary<string, string> GetTriggers()
            => _sdkClass.CallStatic<AndroidJavaObject>("getTriggers").MapToDictionary();

        public override bool InAppMessagesArePaused {
            get => _sdkClass.CallStatic<bool>("isInAppMessagingPaused");
            set => _sdkClass.CallStatic("pauseInAppMessages", value);
        }

        public override async Task<bool> SendTag(string key, object value) {
            _sdkClass.CallStatic("sendTag", key, value.ToString());
            return await Task.FromResult(true); // no callback currently available on Android
        }

        public override async Task<bool> SendTags(Dictionary<string, object> tags) {
            var proxy = new ChangeTagsUpdateHandler();
            _sdkClass.CallStatic("sendTags", tags.ToJSONObject(), proxy);
            return await proxy;
        }

        public override async Task<Dictionary<string, object>> GetTags() {
            var proxy = new OSGetTagsHandler();
            _sdkClass.CallStatic("getTags", proxy);
            return await proxy;
        }

        public override async Task<bool> DeleteTag(string key) {
            var proxy = new ChangeTagsUpdateHandler();
            _sdkClass.CallStatic("deleteTag", key, proxy);
            return await proxy;
        }

        public override async Task<bool> DeleteTags(params string[] keys) {
            var proxy = new ChangeTagsUpdateHandler();
            _sdkClass.CallStatic("deleteTags", keys.ToList(), proxy);
            return await proxy;
        }

        public override async Task<bool> SetExternalUserId(string externalId, string authHash = null) {
            var proxy = new OSExternalUserIdUpdateCompletionHandler();
            _sdkClass.CallStatic("setExternalUserId", externalId, authHash, proxy);
            return await proxy;
        }

        public override async Task<bool> SetEmail(string email, string authHash = null) {
            var proxy = new EmailUpdateHandler();
            _sdkClass.CallStatic("setEmail", email, authHash, proxy);
            return await proxy;
        }

        public override async Task<bool> SetSMSNumber(string smsNumber, string authHash = null) {
            var proxy = new OSSMSUpdateHandler();
            _sdkClass.CallStatic("setSMSNumber", smsNumber, authHash, proxy);
            return await proxy;
        }

        public override async Task<bool> RemoveExternalUserId() {
            var proxy = new OSExternalUserIdUpdateCompletionHandler();
            _sdkClass.CallStatic("removeExternalUserId", proxy);
            return await proxy;
        }

        public override async Task<bool> LogOutEmail() {
            var proxy = new EmailUpdateHandler();
            _sdkClass.CallStatic("logoutEmail", proxy);
            return await proxy;
        }

        public override async Task<bool> LogOutSMS() {
            var proxy = new OSSMSUpdateHandler();
            _sdkClass.CallStatic("logoutSMSNumber", proxy);
            return await proxy;
        }
        
        public override async Task<bool> SetLanguage(string languageCode) {
            var proxy = new OSSetLanguageCompletionHandler();
            _sdkClass.CallStatic("setLanguage", languageCode, proxy);
            return await proxy;
        }

        public override void PromptLocation()
            => _sdkClass.CallStatic("promptLocation");

        public override bool ShareLocation {
            get => _sdkClass.CallStatic<bool>("isLocationShared");
            set => _sdkClass.CallStatic("setLocationShared", value);
        }

        public override async Task<bool> SendOutcome(string name) {
            var proxy = new OutcomeCallback();
            _sdkClass.CallStatic("sendOutcome", name, proxy);
            return await proxy;
        }

        public override async Task<bool> SendUniqueOutcome(string name) {
            var proxy = new OutcomeCallback();
            _sdkClass.CallStatic("sendUniqueOutcome", name, proxy);
            return await proxy;
        }

        public override async Task<bool> SendOutcomeWithValue(string name, float value) {
            var proxy = new OutcomeCallback();
            _sdkClass.CallStatic("sendOutcomeWithValue", name, value, proxy);
            return await proxy;
        }
    }
}