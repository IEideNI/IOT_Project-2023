using System;
using Microsoft.Azure.Devices.Client;
using System.Text;
using Newtonsoft.Json;
using System.Net.Mime;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Shared;
using Opc.UaFx;
using Opc.UaFx.Client;



namespace Device
{
    public class Device1
    {
        private readonly DeviceClient deviceClient;
        public Device1(DeviceClient deviceClient)
        {
            this.deviceClient = deviceClient;
        }
        #region Sendmsg
        public async Task SendMessages(int nrOfMessages = 1, int delay = 0)
        {
            Console.WriteLine("Getting Data...");
            var client = new OpcClient("opc.tcp://localhost:4840/");
            client.Connect();

            var PodStats = new OpcReadNode("ns=2;s=Device 1/ProductionStatus");



            var data = new
            {
                ProductionStatus = client.ReadNode("ns=2;s=Device 1/ProductionStatus").Value,
                ProductionRate = client.ReadNode("ns=2;s=Device 1/ProductionRate").Value,
                WorkorderId = client.ReadNode("ns=2;s=Device 1/WorkorderId").Value,
                Temperature = client.ReadNode("ns=2;s=Device 1/Temperature").Value,
                GoodCount = client.ReadNode("ns=2;s=Device 1/GoodCount").Value,
                BadCount = client.ReadNode("ns=2;s=Device 1/BadCount").Value,
                DeviceError = client.ReadNode("ns=2;s=Device 1/DeviceError").Value,
            };
            Console.WriteLine("Data Collected");
            Console.WriteLine($"Device sending message to IoTHUB ..\n");


            var DataString = JsonConvert.SerializeObject(data);

            Message eventMessage = new Message(Encoding.UTF8.GetBytes(DataString));
            eventMessage.ContentType = MediaTypeNames.Application.Json;
            eventMessage.ContentEncoding = "utf-8";
            Console.WriteLine($"\t{DateTime.Now.ToLocalTime()}> Sending message ,Data: [{DataString}]");

            await deviceClient.SendEventAsync(eventMessage);

            Console.WriteLine("Message Send");
        }

        #endregion
        private async Task On2cdMessageRecievedAsync(Message reciecedMessage, object _)
        {
            Console.WriteLine($"\t{DateTime.Now}> C2D message callback - message recieved with id={reciecedMessage.MessageId}");
            PrintMessages(reciecedMessage);
            await deviceClient.CompleteAsync(reciecedMessage);
            Console.WriteLine($"\t{DateTime.Now}> Completed C2D message with ID={reciecedMessage.MessageId}");
            reciecedMessage.Dispose();

        }

        public async Task InitializerHandlers()
        {
            await deviceClient.SetReceiveMessageHandlerAsync(On2cdMessageRecievedAsync, deviceClient);
            await deviceClient.SetMethodDefaultHandlerAsync(DefaultServiceHandler, deviceClient);
            await deviceClient.SetMethodHandlerAsync("SendMessages", SendMessagesHandler, deviceClient);
            await deviceClient.SetDesiredPropertyUpdateCallbackAsync(OnDesirePropertyChanged, deviceClient);
            await deviceClient.SetMethodHandlerAsync("ChangeProdRateUP", UpdateProductionRateup, deviceClient);
            await deviceClient.SetMethodHandlerAsync("ChangeProdRateDOWN", UpdateProductionRatedown, deviceClient);
            await deviceClient.SetMethodHandlerAsync("EmergencyStop", EmergencyStop, deviceClient);
            await deviceClient.SetMethodHandlerAsync("ClearErrors", ResetErrors, deviceClient);
        }


        private void PrintMessages(Message recievedMessage)
        {
            string messageData = Encoding.ASCII.GetString(recievedMessage.GetBytes());
            Console.WriteLine($"\t\tRecieved message: {messageData}");
            int propCount = 0;
            foreach (var prop in recievedMessage.Properties)
            {
                Console.WriteLine($"\t\tProperty[{propCount++}>Key={prop.Key}:Value={prop.Value}");
            }
        }

        private async Task<MethodResponse> SendMessagesHandler(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine("Method Requested: ");
            Console.WriteLine(methodRequest);

            await SendMessages();
            return new MethodResponse(0);
        }

        private async Task<MethodResponse> DefaultServiceHandler(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine("Method Requested: ");
            Console.WriteLine(methodRequest);

            await Task.Delay(1000);
            return new MethodResponse(0);
        }

        public async Task UpdateTwinAsync()
        {
            var twin = await deviceClient.GetTwinAsync();
            Console.WriteLine($"\tInitial twin value recived: \n{JsonConvert.SerializeObject(twin, Formatting.Indented)} ");

            var reportedProperties = new TwinCollection();
            reportedProperties["DateTimeLastAppLaunch"] = DateTime.Now;

            await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
        }
        private async Task OnDesirePropertyChanged(TwinCollection desiredProperties, object _)
        {
            Console.WriteLine($"\t Desired property change: \n\t {JsonConvert.SerializeObject(desiredProperties)}");
            Console.WriteLine("\tSending current time as reported property");
            TwinCollection reportedProperties = new TwinCollection();
            reportedProperties["DateTimeLastDesiredPropertyRecived"] = DateTime.Now;

            await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);

        }
        private async Task<MethodResponse> UpdateProductionRateup(MethodRequest methodRequest, object userContext)
        {
            var client = new OpcClient("opc.tcp://localhost:4840/");
            client.Connect();

            var ProdRate = new OpcReadNode("ns=2;s=Device 1/ProductionRate");
            var tempProdRateVal = client.ReadNode(ProdRate);
            int FinalProdRateChange = ((int)(tempProdRateVal.As<float>() + 10));
            client.WriteNode("ns=2;s=Device 1/ProductionRate", FinalProdRateChange);

            client.Disconnect();
            return new MethodResponse(0);
        }

        private async Task<MethodResponse> UpdateProductionRatedown(MethodRequest methodRequest, object userContext)
        {
            var client = new OpcClient("opc.tcp://localhost:4840/");
            client.Connect();

            var ProdRate = new OpcReadNode("ns=2;s=Device 1/ProductionRate");
            var tempProdRateVal = client.ReadNode(ProdRate);
            int FinalProdRateChange = ((int)(tempProdRateVal.As<float>() * 0.9f));
            client.WriteNode("ns=2;s=Device 1/ProductionRate", FinalProdRateChange);

            client.Disconnect();
            return new MethodResponse(0);
        }

        private async Task<MethodResponse> EmergencyStop(MethodRequest methodRequest, object userContext)
        {
            var client = new OpcClient("opc.tcp://localhost:4840/");
            client.Connect();

            client.CallMethod("ns=2;s=Device 1", "ns=2;s=Device 1/EmergencyStop");
            //var test = new OpcCallMethod("Device1", "ns=2;s=Device 1/EmergencyStop");

            client.Disconnect();
            Console.WriteLine("STOP!!!!!!");
            return new MethodResponse(0);
        }

        private async Task<MethodResponse> ResetErrors(MethodRequest methodRequest, object userContext)
        {
            var client = new OpcClient("opc.tcp://localhost:4840/");
            client.Connect();

            client.CallMethod("ns=2;s=Device 1", "ns=2;s=Device 1/ResetErrorStatus");

            client.Disconnect();
            Console.WriteLine("Errors Reseted =)");
            return new MethodResponse(0);
        }
    }
}
