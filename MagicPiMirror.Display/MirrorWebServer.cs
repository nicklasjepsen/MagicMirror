using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Devkoes.Restup.WebServer;
using Devkoes.Restup.WebServer.Attributes;
using Devkoes.Restup.WebServer.Models.Schemas;

namespace SystemOut.MagicPiMirror
{
    [RestController(InstanceCreationType.Singleton)]
    public class MirrorWebServer
    {
        public async Task InitializeWebServer()
        {
            var webserver = new RestWebServer(80);
            webserver.RegisterController<MirrorWebServer>();

            await webserver.StartServerAsync();
        }

        [UriFormat("/values/{key}/{value}")]
        public GetResponse SetValue(string key, string value)
        {
            value = WebUtility.UrlDecode(value);
            ApplicationDataController.SetValue(key, value);
            WebServerEventProxy.Instance.Invoke(this, new ValueChangedEventArg(key, value));
            return new GetResponse(GetResponse.ResponseStatus.OK, $"Value for {key} set to {value}.");
        }
    }

    public class WebServerEventProxy
    {
        public event ThresholdReachedEventHandler ValueChanged;
        public delegate void ThresholdReachedEventHandler(object sender, ValueChangedEventArg e);

        private static WebServerEventProxy instance;
        public static WebServerEventProxy Instance => instance ?? (instance = new WebServerEventProxy());
        private WebServerEventProxy(){}

        public void Invoke(object sender, ValueChangedEventArg e)
        {
            ValueChanged?.Invoke(this, e);
        }
    }

    public class ValueChangedEventArg : EventArgs
    {
        public string Key { get; private set; }
        public string Value { get; private set; }

        public ValueChangedEventArg(string key, string value)
        {
            Key = key;
            Value = value;
        }
    }


}
