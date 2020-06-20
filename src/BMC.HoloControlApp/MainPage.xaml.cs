using BMC.HoloControlApp.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace BMC.HoloControlApp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        MqttService iot;
        public MainPage()
        {
            this.InitializeComponent();
            Setup();
        }

        void Setup()
        {
            BtnExit.Click += (a, b) => { if (this.Frame.CanGoBack) this.Frame.GoBack(); else this.Frame.Navigate(typeof(MainPage)); };
            iot = new MqttService();
            List1.ItemsSource = DeviceData.GetAllDevices();
            BtnFind.Click += (a, b) => {
                Progress1.Visibility = Visibility.Visible;
                List1.ItemsSource = DeviceData.FilterDevice(TxtSearch.Text);
                Progress1.Visibility = Visibility.Collapsed;
            };

        }

        private void Control_Device(object sender, RoutedEventArgs e)
        {
            var btn = (sender as Button);
            var item = btn.DataContext as DeviceData;
            if (item != null)
            {
                var state = btn.Tag.ToString() == "On" ? true : false;
                SwitchDevice(state, item.IP);
            }
        }
        private async void SwitchDevice(bool State, string IP)
        {
            if (State)
            {
                //string DeviceID = $"Device{btn.CommandArgument}IP";
                string URL = $"http://{IP}/cm?cmnd=Power%20On";
                await iot.InvokeMethod("BMCSecurityBot", "OpenURL", new string[] { URL });
            }
            else
            {
                //string DeviceID = $"Device{btn.CommandArgument}IP";
                string URL = $"http://{IP}/cm?cmnd=Power%20Off";
                await iot.InvokeMethod("BMCSecurityBot", "OpenURL", new string[] { URL });
            }
        }
    }

    public class DeviceData
    {
        public string IP { get; set; }
        public string Name { get; set; }
        public int ID { get; set; }

        public static ObservableCollection<DeviceData> GetAllDevices()
        {
            return new ObservableCollection<DeviceData>()
            {
                new DeviceData (){ ID=1, Name="Toilet Lamp", IP="192.168.1.27" },
                new DeviceData (){ ID=2, Name="Printer Room Lamp", IP="192.168.1.25" },
                new DeviceData (){ ID=3, Name="Living Room Lamp", IP="192.168.1.28" },
                new DeviceData (){ ID=4, Name="Prayer Room Fan", IP="192.168.1.31" },
                new DeviceData (){ ID=5, Name="Printer Fan", IP="192.168.1.32" },
                new DeviceData (){ ID=6, Name="Kitchen Fan", IP="192.168.1.33" },
                 //new DeviceData (){ ID=7, Name="Prayer Room", IP="192.168.1.35" },
                new DeviceData (){ ID=8, Name="Guest Room Lamp", IP="192.168.1.26" },
                new DeviceData (){ ID=7, Name="Front Room Fan", IP="192.168.1.36" },
                new DeviceData (){ ID=8, Name="Prayer Room Lamp", IP="192.168.1.29" },
                new DeviceData (){ ID=10, Name="Door Lock 1", IP="192.168.1.40" },
                new DeviceData (){ ID=11, Name="Door Lock 2", IP="192.168.1.42" },
            };
        }
        public static List<DeviceData> FilterDevice(string Keyword)
        {
            var datas = from x in GetAllDevices()
                        where x.Name.Contains(Keyword, StringComparison.InvariantCultureIgnoreCase)
                        select x;
            return datas.ToList();
        }

    }
    public class MqttService
    {
        public MqttService()
        {
            SetupMqtt();
        }
        MqttClient MqttClient;
        const string DataTopic = "bmc/homeautomation/data";
        const string ControlTopic = "bmc/homeautomation/control";
        public void PublishMessage(string Message)
        {
            MqttClient.Publish(DataTopic, Encoding.UTF8.GetBytes(Message), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
        }
        public void SendCommand(string Message, string Topic)
        {
            MqttClient.Publish(Topic, Encoding.UTF8.GetBytes(Message), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
        }
        void SetupMqtt()
        {
            string IPBrokerAddress = APPCONTANTS.MQTT_SERVER;
            string ClientUser = APPCONTANTS.MQTT_USER;
            string ClientPass = APPCONTANTS.MQTT_PASS;

            MqttClient = new MqttClient(IPBrokerAddress);

            // register a callback-function (we have to implement, see below) which is called by the library when a message was received
            MqttClient.MqttMsgPublishReceived += client_MqttMsgPublishReceived;

            // use a unique id as client id, each time we start the application
            var clientId = "bmc-car-app";//Guid.NewGuid().ToString();

            MqttClient.Connect(clientId, ClientUser, ClientPass);
            Console.WriteLine("MQTT is connected");
        } // this code runs when a message was received
        void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            string ReceivedMessage = Encoding.UTF8.GetString(e.Message);
            if (e.Topic == ControlTopic)
            {
                Console.WriteLine(ReceivedMessage);
            }
        }
        // Invoke the direct method on the device, passing the payload
        public Task InvokeMethod(string DeviceId, string ActionName, params string[] Params)
        {
            return Task.Factory.StartNew(() =>
            {
                var action = new DeviceAction() { ActionName = ActionName, Params = Params };
                SendCommand(JsonConvert.SerializeObject(action), ControlTopic);
            });
            //Console.WriteLine("Response status: {0}, payload:", response.Status);
            //Console.WriteLine(response.GetPayloadAsJson());
        }

        public Task InvokeMethod2(string Topic, string ActionName, params string[] Params)
        {
            return Task.Factory.StartNew(() =>
            {
                var action = new DeviceAction() { ActionName = ActionName, Params = Params };
                SendCommand(JsonConvert.SerializeObject(action), Topic);
            });

            //Console.WriteLine("Response status: {0}, payload:", response.Status);
            //Console.WriteLine(response.GetPayloadAsJson());
        }

    }
    public class DeviceAction
    {
        public string ActionName { get; set; }
        public string[] Params { get; set; }
    }

}
