using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EasyHttp.Http;
using Newtonsoft.Json;
using ToastNotifications;
using WebSocketSharp;

namespace SlackWindowsTray
{
    // Responsible for connecting to Slack RTM, maintaining the connection and creating events for appropriate listeners
    class RtmConnection
    {
        private WebSocket _webSocket = null;

        public static readonly RtmConnection Instance = new RtmConnection();
        private RtmConnection()
        {
        }

        public void Start()
        {
            Task.Factory.StartNew(ConnectRtm);
        }

        private void ConnectRtm()
        {
            var rtmInfo = SlackApi.Instance.Get("rtm.start");
            InitialConnect(rtmInfo.url.Value);
        }

        private void InitialConnect(string url)
        {
            _webSocket = new WebSocket(url);
            _webSocket.OnMessage += OnSocketMessage;
            _webSocket.OnClose += OnSocketClose;
            _webSocket.Connect();
        }

        void OnSocketClose(object sender, CloseEventArgs e)
        {
            if (_webSocket == null)
            {
                throw new ApplicationException("_webSocket can't be null");
            }

            Log.Write("WARN: RTM websocket closed. Reconnecting in a few seconds...");
            Thread.Sleep(5000);
            _webSocket.Connect();
        }

        private void OnSocketMessage(object sender, MessageEventArgs e)
        {
            dynamic message = JsonConvert.DeserializeObject(e.Data);
            if (message.type == "message")
            {
                Log.Write("Recieved message: " + e.Data);

                RtmIncomingMessages.Instance.OnMessage(message);
            }
        }
    }
}