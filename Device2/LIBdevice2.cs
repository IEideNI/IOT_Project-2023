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
        string OPCstring = File.ReadAllText(@"ConnectionOpcUa.txt");
        string DeviceName = File.ReadAllText(@"DeviceName.txt");
        public Device1(DeviceClient deviceClient)
        {
            this.deviceClient = deviceClient;
        }
        #region Sendmsg
        public async Task SendMessages(int nrOfMessages = 1, int delay = 0)
        {
            Console.WriteLine("Getting Data...");
            var client = new OpcClient(OPCstring);
            client.Connect();

            var PodStats = new OpcReadNode($"ns=2;s={DeviceName}/ProductionStatus");

            var data = new
            {
                ProductionStatus = client.ReadNode($"ns=2;s={DeviceName}/ProductionStatus").Value,
                WorkorderId = client.ReadNode($"ns=2;s={DeviceName}/WorkorderId").Value,
                Temperature = client.ReadNode($"ns=2;s={DeviceName}/Temperature").Value,
                GoodCount = client.ReadNode($"ns=2;s={DeviceName}/GoodCount").Value,
                BadCount = client.ReadNode($"ns=2;s={DeviceName}/BadCount").Value,
            };



            var GoodCountCalculateNode = new OpcReadNode($"ns=2;s={DeviceName}/GoodCount");
            var BadCountCalculateNode = new OpcReadNode($"ns=2;s={DeviceName}/BadCount");
            int GoodCountCalculate = client.ReadNode(GoodCountCalculateNode).As<int>();
            int BadCountCalculate = client.ReadNode(GoodCountCalculateNode).As<int>();
            if (GoodCountCalculate + BadCountCalculate > 100)
            {
                if ((GoodCountCalculate + BadCountCalculate) * 0.85f > GoodCountCalculate)
                {
                    Console.WriteLine("Poor Production, Reducing Production Rate =()");
                    await Task.Delay(1000);
                    var ProdRate = new OpcReadNode($"ns=2;s={DeviceName}/ProductionRate");
                    var tempProdRateVal = client.ReadNode(ProdRate);
                    int FinalProdRateChange = ((tempProdRateVal.As<int>() - 10));
                    client.WriteNode($"ns=2;s={DeviceName}/ProductionRate", FinalProdRateChange);
                }
            }
            var ProductionRate = new OpcReadNode($"ns=2;s={DeviceName}/ProductionRate");
            var DeviceError = new OpcReadNode($"ns=2;s={DeviceName}/DeviceError");
            int ProductionRateNode = client.ReadNode(ProductionRate).As<int>();
            int DeviceErrorNode = client.ReadNode(DeviceError).As<int>();
            if (DeviceErrorNode == 14)
            {
                client.CallMethod($"ns=2;s={DeviceName}", $"ns=2;s={DeviceName}/EmergencyStop");
                Console.WriteLine("DEVICE STOPPED BY READING TOO MANY ERRORS!");
            }

            await UpdateTwinData(ProductionRateNode, DeviceErrorNode);
            Console.WriteLine("Data Collected");
            Console.WriteLine($"Device sending message to IoTHUB ..\n");
            var DataString = JsonConvert.SerializeObject(data);

            Message eventMessage = new Message(Encoding.UTF8.GetBytes(DataString));
            eventMessage.ContentType = MediaTypeNames.Application.Json;
            eventMessage.ContentEncoding = "utf-8";
            Console.WriteLine($"\t{DateTime.Now.ToLocalTime()}> Sending message ,Data: [{DataString}]");

            await deviceClient.SendEventAsync(eventMessage);
            client.Disconnect();
            Console.WriteLine("Message Send");
        }

        public async Task TimerSendingMessages()
        {
            var client = new OpcClient(OPCstring);
            client.Connect();

            var ProductionStatus = new OpcReadNode($"ns=2;s={DeviceName}/ProductionStatus");
            int RetValues = client.ReadNode(ProductionStatus).As<int>();
            var DeviceError = new OpcReadNode($"ns=2;s={DeviceName}/DeviceError");
            int DeviceErrorNode = client.ReadNode(DeviceError).As<int>();

            client.Disconnect();
            if (RetValues == 1)
            {
                await SendMessages(1, 1);
            }
            else
            {
                if (DeviceErrorNode == 0)
                {
                    Console.WriteLine("Device Offline -> Not Sending Data");
                }
                else
                {
                    string DeviceErrorString = "";
                    if (DeviceErrorNode - 8 >= 0)
                    {
                        DeviceErrorNode = DeviceErrorNode - 8;
                        DeviceErrorString = DeviceErrorString + "Unknown Error ,";
                    }
                    if (DeviceErrorNode - 4 >= 0)
                    {
                        DeviceErrorNode = DeviceErrorNode - 4;
                        DeviceErrorString = DeviceErrorString + "Sensor Failure ,";
                    }
                    if (DeviceErrorNode - 2 >= 0)
                    {
                        DeviceErrorNode = DeviceErrorNode - 2;
                        DeviceErrorString = DeviceErrorString + "Power Failure ,";
                    }
                    if (DeviceErrorNode - 1 >= 0)
                    {
                        DeviceErrorNode = DeviceErrorNode - 1;
                        DeviceErrorString = DeviceErrorString + "Emergency Stop ,";
                    }
                    Console.WriteLine("Device OFFLINE -> Errors: " + DeviceErrorString + " -> NOT SENDING DATA");

                }
            }
        }

        private async Task UpdateTwinData(int ProductionRate, int DeviceError)
        {
            string DeviceErrorString = "";
            if (DeviceError - 8 >= 0)
            {
                DeviceError = DeviceError - 8;
                DeviceErrorString = DeviceErrorString + "Unknown Error ,";
            }
            if (DeviceError - 4 >= 0)
            {
                DeviceError = DeviceError - 4;
                DeviceErrorString = DeviceErrorString + "Sensor Failure ,";
            }
            if (DeviceError - 2 >= 0)
            {
                DeviceError = DeviceError - 2;
                DeviceErrorString = DeviceErrorString + "Power Failure ,";
            }
            if (DeviceError - 1 >= 0)
            {
                DeviceError = DeviceError - 1;
                DeviceErrorString = DeviceErrorString + "Emergency Stop ,";
            }

            var twin = await deviceClient.GetTwinAsync();
            var reportedProperties = new TwinCollection();

            string ReportedErrorStatus = twin.Properties.Reported["ErrorStatus"];
            int ReportedProductionRate = twin.Properties.Reported["ProductionRate"];

            if (!ReportedErrorStatus.Equals(DeviceErrorString))
            {
                reportedProperties["ErrorStatus"] = DeviceErrorString;
                await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
            }
            if (ReportedProductionRate != ProductionRate)
            {
                reportedProperties["ProductionRate"] = ProductionRate;
                await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
            }


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
            reportedProperties["ErrorStatus"] = String.Empty;
            reportedProperties["ProductionRate"] = 0;

            await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
        }
        private async Task OnDesirePropertyChanged(TwinCollection desiredProperties, object _)
        {
            Console.WriteLine($"\t Desired property change: \n\t {JsonConvert.SerializeObject(desiredProperties)}");
            Console.WriteLine("\tSending current time as reported property");
            TwinCollection reportedProperties = new TwinCollection();
            reportedProperties["ProductionRate"] = desiredProperties["ProductionRate"];

            var client = new OpcClient(OPCstring);
            client.Connect();
            int FinalProdRateChange = desiredProperties["ProductionRate"];
            Console.WriteLine(FinalProdRateChange);
            client.WriteNode($"ns=2;s={DeviceName}/ProductionRate", FinalProdRateChange);

            client.Disconnect();

            await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);

        }
        private async Task<MethodResponse> UpdateProductionRateup(MethodRequest methodRequest, object userContext)
        {
            var client = new OpcClient(OPCstring);
            client.Connect();
            await Task.Delay(1000);
            var ProdRate = new OpcReadNode($"ns=2;s={DeviceName}/ProductionRate");
            var tempProdRateVal = client.ReadNode(ProdRate);
            int FinalProdRateChange = ((int)(tempProdRateVal.As<float>() + 10));
            client.WriteNode($"ns=2;s={DeviceName}/ProductionRate", FinalProdRateChange);

            client.Disconnect();
            return new MethodResponse(0);
        }

        private async Task<MethodResponse> UpdateProductionRatedown(MethodRequest methodRequest, object userContext)
        {
            var client = new OpcClient(OPCstring);
            client.Connect();
            await Task.Delay(1000);
            var ProdRate = new OpcReadNode($"ns=2;s={DeviceName}/ProductionRate");
            var tempProdRateVal = client.ReadNode(ProdRate);
            int FinalProdRateChange = ((tempProdRateVal.As<int>() - 10));
            client.WriteNode($"ns=2;s={DeviceName}/ProductionRate", FinalProdRateChange);

            client.Disconnect();
            return new MethodResponse(0);
        }

        private async Task<MethodResponse> EmergencyStop(MethodRequest methodRequest, object userContext)
        {
            var client = new OpcClient(OPCstring);
            client.Connect();
            await Task.Delay(1000);
            client.CallMethod($"ns=2;s={DeviceName}", $"ns=2;s={DeviceName}/EmergencyStop");
            //var test = new OpcCallMethod("Device1", "ns=2;s={DeviceName}/EmergencyStop");

            client.Disconnect();
            Console.WriteLine("STOP!!!!!!");
            return new MethodResponse(0);
        }

        private async Task<MethodResponse> ResetErrors(MethodRequest methodRequest, object userContext)
        {
            var client = new OpcClient(OPCstring);
            client.Connect();
            await Task.Delay(1000);
            client.CallMethod($"ns=2;s={DeviceName}", $"ns=2;s={DeviceName}/ResetErrorStatus");

            client.Disconnect();
            Console.WriteLine("Errors Reseted =)");
            return new MethodResponse(0);
        }
    }
}
